using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;
using System.Diagnostics;

namespace TsekSheet.Application.Excel;

public sealed record ExcelWorkbookChoice(string Id, string Name, IReadOnlyList<string> Sheets);
public sealed record ExcelSectionChoice(string Id, string Label, string SheetName);
public sealed record ExcelInsertDisplay(string Workbook, string Sheet, string Range, IReadOnlyList<FormulaReview> ManualReview);

public sealed class ExcelReceiptInsertion(IExcelHost host, ISectionWriter writer, Func<string, WorkflowProfile>? profiles = null)
{
    private WorkflowProfile Profile(string id) => (profiles ?? WorkflowProfiles.Get)(id);
    private readonly Dictionary<string, (ReviewVersion Version, SectionInsertPreview Preview, string Workbook, bool Draft)> previews = [];
    private readonly HashSet<Guid> inserted = [];
    private readonly HashSet<(string Hash, int Page)> submittedSources = [];
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly List<ExcelInsertDisplay> changes = [];
    public IReadOnlyList<ExcelInsertDisplay> Changes { get { lock (changes) return changes.ToArray(); } }
    public Action<PreviewStageTiming>? PreviewTiming { get; set; }
    private void Measure(string stage, Stopwatch watch) => PreviewTiming?.Invoke(new(stage, watch.Elapsed.TotalMilliseconds));

    public async Task<string> OpenAsync(string path)
    {
        await commands.WaitAsync();
        try { return (await host.OpenAsync(path)).Id; }
        finally { commands.Release(); }
    }

    public async Task<IReadOnlyList<ExcelWorkbookChoice>> ListAsync(string? path = null, string? profileId = null)
    {
        await commands.WaitAsync();
        try
        {
            if (path is not null) await host.OpenAsync(path);
            var result = new List<ExcelWorkbookChoice>();
            var profile = profileId is null ? null : Profile(profileId);
            foreach (var book in await host.ListWorkbooksAsync())
            {
                var sheets = new List<string>();
                foreach (var sheet in book.Sheets)
                {
                    if (profile?.Layout is not { } layout) { sheets.Add(sheet); continue; }
                    var reader = host as IWorkbookReader ?? throw new InvalidOperationException("Excel readback is unavailable.");
                    var right = layout.SheetMatch.Anchors.Max(a => A1Formula.ColumnNumber(a.Column));
                    var header = await reader.ReadAsync(book.Id, sheet, new(layout.SheetMatch.HeaderRow, 1, 1, right));
                    if (ExcelLayoutMatching.Matches(header, layout.SheetMatch)) sheets.Add(sheet);
                }
                result.Add(new(book.Id, book.Name, sheets));
            }
            return result;
        }
        finally { commands.Release(); }
    }

    public IReadOnlyList<ExcelSectionChoice> Sections(string profileId)
    {
        var p = Profile(profileId);
        return p.Layout is { } layout ? layout.Sections.Select(s => new ExcelSectionChoice(s.Id, s.Label, "")).ToArray()
            : (p.ExcelSections ?? []).Select(s => new ExcelSectionChoice(s.Id, s.Label, s.SheetName)).ToArray();
    }

    public async Task<SectionInsertPreview> PreviewAsync(DesktopReceiptReview receipt, ReviewVersion version, string workbookId, string sectionId, CancellationToken token, string? sheetName = null, bool draft = false)
    {
        var total = Stopwatch.StartNew();
        await commands.WaitAsync(token);
        try
        {
            Measure("application.queue", total);
            var sourceWatch = Stopwatch.StartNew();
            return await (draft ? receipt.WithDraftExcelAsync(version, Build, token) : receipt.WithVerifiedExcelAsync(version, Build, token));
            async Task<SectionInsertPreview> Build(TypedReviewRecord record)
            {
                Measure("application.source", sourceWatch);
                var watch = Stopwatch.StartNew();
                if (inserted.Contains(version.ReviewId) || AlreadySubmitted(SourceKeys(record))) throw new FieldReviewException("ALREADY_INSERTED", "This receipt source was already inserted or submitted. Review the highlighted row in Excel.");
                var profile = Profile(version.ProfileId);
                if (WorkflowProfiles.Hash(profile) != version.ProfileHash) throw new FieldReviewException("STALE_PROFILE", "The local profile changed. Start a new review.");
                var section = profile.Layout is { } layout
                    ? layout.Section(sectionId, sheetName ?? throw new FieldReviewException("SHEET_REQUIRED", "Choose a matching sheet."))
                    : profile.ExcelSections?.SingleOrDefault(s => s.Id == sectionId) ?? throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section from this receipt profile.");
                if (sheetName is not null && section.SheetName != sheetName) throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section on the selected sheet.");
                if (profile.Layout is { MappingMode: "account" } accountLayout)
                {
                    var choice = record.Fields.Single(f => f.Kind == "accountChoice").LiteralText;
                    var account = accountLayout.Accounts.SingleOrDefault(a => a.Column == choice)
                        ?? throw new FieldReviewException("INVALID_ACCOUNT", "Choose an account from this profile.");
                    var amount = accountLayout.Fields.Single(f => f.WritesTo == "selectedAccountColumn");
                    section = section with { ColumnMap = section.ColumnMap.Append(new KeyValuePair<string, string>(amount.Key, account.Column)).ToDictionary() };
                }
                var values = record.Fields.Where(f => f.Kind != "accountChoice").Select(field => new InsertValue(field.Key, profile.Fields.Single(f => f.Key == field.Key).Header,
                    A1Formula.ColumnNumber(section.ColumnMap[field.Key]), field.Kind switch
                    {
                        "decimal" or "money" => field.DecimalValue is decimal number ? (object)MapNumber(number) : null,
                        "date" => field.DateValue is DateOnly date ? MapDate(date) : null,
                        _ => field.LiteralText.Length == 0 ? null : field.LiteralText
                    }, field.LiteralText, field.Kind == "date")).ToArray();
                Measure("application.mapping", watch); watch.Restart();
                var book = (await host.ListWorkbooksAsync()).SingleOrDefault(w => w.Id == workbookId) ?? throw new InvalidOperationException("Choose an open Excel workbook.");
                Measure("application.identity", watch); watch.Restart();
                var preview = await writer.PreviewInsertAsync(workbookId, section, values);
                Measure("application.excelPreview", watch);
                if (profile.Layout?.AdvisoryIdentifier is string identifier)
                    preview = preview with { AdvisoryWarnings = await DuplicateWarningsAsync(workbookId, profile, record, identifier) };
                if (draft)
                {
                    if (preview.ManualReview.Count > 0) throw new FieldReviewException("AMBIGUOUS_TOTAL", "A subtotal formula needs manual review in Excel before Add Row is available.");
                    await RejectDuplicateAsync(workbookId, section, profile, record);
                }
                if (previews.Count >= 64) previews.Clear();
                previews[preview.Token] = (version, preview, book.Name, draft);
                return preview;
            }
        }
        finally { commands.Release(); Measure("application.total", total); }
    }

    internal static double MapNumber(decimal value)
    {
        var number = (double)value;
        var roundTrip = decimal.Parse(number.ToString("G15", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture);
        if (roundTrip != value) throw new FieldReviewException("EXCEL_NUMBER_PRECISION", "The verified number exceeds Excel's fifteen significant digits. Correct and verify the field before inserting.");
        return number;
    }

    private static double MapDate(DateOnly date)
    {
        if (date.Year < 1900) throw new FieldReviewException("EXCEL_DATE_RANGE", "Excel dates must be on or after 1900-01-01.");
        return date.ToDateTime(TimeOnly.MinValue).ToOADate();
    }

    public async Task<ExcelInsertDisplay> InsertAsync(DesktopReceiptReview receipt, ReviewVersion version, string previewToken, CancellationToken token, bool confirm = false)
    {
        await commands.WaitAsync(token);
        try
        {
            if (!previews.Remove(previewToken, out var plan) || plan.Version != version) throw new FieldReviewException("STALE_INSERT_PREVIEW", "Request a fresh preview for the current receipt.");
            if (plan.Draft && !confirm) throw new FieldReviewException("CONFIRMATION_REQUIRED", "Explicitly confirm the draft values and destination with Add Row.");
            if (confirm) version = receipt.ConfirmAll(version, token).Version!;
            return await receipt.WithVerifiedExcelAsync(version, async record =>
            {
                var sources = SourceKeys(record);
                if (AlreadySubmitted(sources)) throw new FieldReviewException("ALREADY_INSERTED", "This receipt source was already submitted. Review Excel before retrying.");
                if (!inserted.Add(version.ReviewId)) throw new FieldReviewException("ALREADY_INSERTED", "This receipt was already submitted. Review Excel before retrying.");
                submittedSources.UnionWith(sources);
                // Once submitted, a failed/unknown outcome cannot be retried as another revision.
                SectionInsertResult result;
                try { result = await writer.InsertAsync(previewToken); }
                catch (ExcelInsertRejectedException error)
                {
                    inserted.Remove(version.ReviewId);
                    foreach (var source in sources) submittedSources.Remove(source);
                    throw new FieldReviewException("STALE_INSERT_PREVIEW", error.Message);
                }
                var display = new ExcelInsertDisplay(plan.Workbook, result.Sheet, result.Range, result.ManualReview);
                lock (changes) { if (changes.Count >= 128) changes.RemoveAt(0); changes.Add(display); }
                return display;
            }, token);
        }
        finally { commands.Release(); }
    }

    private async Task<IReadOnlyList<string>> DuplicateWarningsAsync(string workbookId, WorkflowProfile profile, TypedReviewRecord record, string key)
    {
        var reader = host as IWorkbookReader ?? throw new InvalidOperationException("Excel readback is unavailable.");
        var identifier = record.Fields.Single(f => f.Key == key).LiteralText.Trim();
        var column = A1Formula.ColumnNumber(profile.Layout!.Fields.Single(f => f.Key == key).Column!);
        var warnings = new List<string>();
        var book = (await host.ListWorkbooksAsync()).Single(b => b.Id == workbookId);
        foreach (var sheet in book.Sheets)
        {
            var range = await reader.UsedRangeAsync(workbookId, sheet);
            var data = await reader.ReadAsync(workbookId, sheet, range);
            if (!ExcelLayoutMatching.Matches(data, profile.Layout.SheetMatch)) continue;
            if (Enumerable.Range(range.Row, range.Rows).Any(row => row > profile.Layout.SheetMatch.HeaderRow &&
                string.Equals(identifier, Convert.ToString(ExcelLayoutMatching.Value(data, row, column), System.Globalization.CultureInfo.InvariantCulture)?.Trim(), StringComparison.OrdinalIgnoreCase)))
                warnings.Add($"Possible duplicate {profile.Fields.Single(f => f.Key == key).Header} in {sheet}. Review Excel; insertion is still available.");
        }
        return warnings;
    }

    private async Task RejectDuplicateAsync(string workbookId, ExcelSectionDefinition section, WorkflowProfile profile, TypedReviewRecord record)
    {
        // Required fields and column order do not establish uniqueness. Layout profiles
        // do not declare a value-based duplicate policy; hashes/submission guards remain.
        if (profile.DuplicatePolicy != "entity-and-any-identifier")
        {
            PreviewTiming?.Invoke(new("duplicate.notConfigured", 0));
            return;
        }
        var watch = Stopwatch.StartNew();
        var reader = host as IWorkbookReader ?? throw new InvalidOperationException("Excel readback is unavailable.");
        var range = await reader.UsedRangeAsync(workbookId, section.SheetName);
        Measure("duplicate.usedRange", watch); watch.Restart();
        var data = await reader.ReadAsync(workbookId, section.SheetName, range);
        Measure("duplicate.readback", watch); watch.Restart();
        var boundary = ExcelLayoutMatching.Resolve(data, section);
        Measure("duplicate.layout", watch); watch.Restart();
        var identifiers = profile.IdentifierFields;
        var entityColumn = A1Formula.ColumnNumber(section.ColumnMap[profile.EntityField]);
        var entity = WorkflowProfiles.Entity(record.Fields.Single(f => f.Key == profile.EntityField).LiteralText, profile);
        for (var row = boundary.First; row < boundary.Subtotal; row++)
        {
            var existingEntity = Convert.ToString(ExcelLayoutMatching.Value(data, row, entityColumn), System.Globalization.CultureInfo.InvariantCulture) ?? "";
            if (WorkflowProfiles.Entity(existingEntity, profile) != entity) continue;
            foreach (var key in identifiers)
            {
                var value = record.Fields.Single(f => f.Key == key).LiteralText.Trim();
                if (value.Length == 0 || !section.ColumnMap.TryGetValue(key, out var column)) continue;
                var existing = Convert.ToString(ExcelLayoutMatching.Value(data, row, A1Formula.ColumnNumber(column)), System.Globalization.CultureInfo.InvariantCulture)?.Trim();
                if (StringComparer.OrdinalIgnoreCase.Equals(value, existing))
                    throw new FieldReviewException("DUPLICATE_RECORD", $"Duplicate {profile.Fields.Single(f => f.Key == key).Header} at row {row}. Review the existing Excel row.");
            }
        }
        Measure("duplicate.scan", watch);
    }

    private bool AlreadySubmitted((string Hash, int Page)[] sources) => sources.Any(source =>
        submittedSources.Contains(source) || submittedSources.Contains((source.Hash, 0)) ||
        source.Page == 0 && submittedSources.Any(previous => previous.Hash == source.Hash));

    private (string Hash, int Page)[] SourceKeys(TypedReviewRecord record) => record.Fields
        .Where(f => f.Source is not null).Select(f => (f.Source!.DocumentSha256, Profile(record.Version.ProfileId).Layout?.SourceScope == "document" ? 0 : f.Source.PageNumber)).Distinct().ToArray();
}
