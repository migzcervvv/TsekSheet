using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;
using System.Diagnostics;

namespace TsekSheet.Application.Excel;

public sealed record ExcelWorkbookChoice(string Id, string Name, IReadOnlyList<string> Sheets);
public sealed record ExcelSectionChoice(string Id, string Label, string SheetName);
public sealed record ExcelInsertDisplay(string Workbook, string Sheet, string Range, IReadOnlyList<FormulaReview> ManualReview);

public sealed class ExcelReceiptInsertion(IExcelHost host, ISectionWriter writer, Func<string, WorkflowProfile>? profiles = null,
    Func<string, WorkbookBinding?>? bindings = null)
{
    private WorkflowProfile Profile(string id) => (profiles ?? WorkflowProfiles.Get)(id);
    private WorkbookBinding? Binding(string profileId) => bindings is not null ? bindings(profileId)
        : profileId == "payables-rfp" ? WorkbookBindings.Payables : null;
    private sealed record PreviewPlan(ReviewVersion Version, string Workbook, bool Draft, string WorkbookId, string? BindingConfiguration);
    private readonly Dictionary<string, PreviewPlan> previews = [];
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
            var binding = profileId is null ? null : Binding(profileId);
            foreach (var book in await host.ListWorkbooksAsync())
            {
                if (binding is not null && writer is IBindingSectionWriter boundWriter && await boundWriter.HasBindingAsync(book.Id, binding))
                {
                    result.Add(new(book.Id, book.Name, book.Sheets.Where(s => binding.RequiredSheets.Contains(s)).ToArray()));
                    continue;
                }
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
        if (Binding(profileId) is { } binding)
            return binding.Targets.Select(t => new ExcelSectionChoice(t.Id, t.Label, "")).ToArray();
        return p.Layout is { } layout ? layout.Sections.Select(s => new ExcelSectionChoice(s.Id, s.Label, "")).ToArray()
            : (p.ExcelSections ?? []).Select(s => new ExcelSectionChoice(s.Id, s.Label, s.SheetName)).ToArray();
    }

    public async Task<SectionInsertPreview> PreviewAsync(DesktopReceiptReview receipt, ReviewVersion version, string workbookId, string sectionId, CancellationToken token, string? sheetName = null, bool draft = false, ReceiptQueue? queue = null)
    {
        var total = Stopwatch.StartNew();
        await commands.WaitAsync(token);
        try
        {
            Measure("application.queue", total);
            var sourceWatch = Stopwatch.StartNew();
            Task<SectionInsertPreview> Start() => draft
                ? receipt.WithDraftExcelAsync(version, Build, token)
                : receipt.WithVerifiedExcelAsync(version, Build, token);
            return await (queue is null ? Start() : queue.StartExcel(receipt, null, Start));
            async Task<SectionInsertPreview> Build(TypedReviewRecord record)
            {
                Measure("application.source", sourceWatch);
                var watch = Stopwatch.StartNew();
                if (inserted.Contains(version.ReviewId) || AlreadySubmitted(SourceKeys(record))) throw new FieldReviewException("ALREADY_INSERTED", "This receipt source was already inserted or submitted. Review the highlighted row in Excel.");
                var profile = Profile(version.ProfileId);
                if (WorkflowProfiles.Hash(profile) != version.ProfileHash) throw new FieldReviewException("STALE_PROFILE", "The local profile changed. Start a new review.");
                foreach (var oldToken in previews.Where(entry => entry.Value.Version.ReviewId == version.ReviewId).Select(entry => entry.Key).ToArray())
                    previews.Remove(oldToken);
                var book = (await host.ListWorkbooksAsync()).SingleOrDefault(w => w.Id == workbookId) ?? throw new InvalidOperationException("Choose an open Excel workbook.");
                var binding = Binding(profile.Id);
                var boundWriter = writer as IBindingSectionWriter;
                var useBinding = binding is not null && boundWriter is not null && await boundWriter.HasBindingAsync(workbookId, binding);
                Measure("application.identity", watch); watch.Restart();
                var document = DocumentTypes.ForProfile(profile);
                WorkbookTarget? target = null;
                ExcelSectionDefinition? legacySection = null;
                if (useBinding)
                {
                    if (document is null) throw new FieldReviewException("INVALID_BINDING", "This receipt has no installed document type for its workbook binding.");
                    WorkbookBindings.Validate(binding!, document);
                    target = binding!.Targets.SingleOrDefault(t => t.Id == sectionId)
                        ?? throw new FieldReviewException("UNKNOWN_SECTION", "Choose a target from this workbook binding.");
                    if (target.Type == "unsupported") throw new FieldReviewException("UNSUPPORTED_TARGET", "The selected target cannot accept entries.");
                    if (target.Sheet != sheetName) throw new FieldReviewException("UNKNOWN_SECTION", "Choose a target on the selected sheet.");
                    foreach (var required in target.RequiredFields)
                        if (!record.Fields.Any(f => f.Key == required && !string.IsNullOrWhiteSpace(f.LiteralText)))
                            throw new FieldReviewException("TARGET_FIELD_REQUIRED", $"Enter {document.Fields.Single(f => f.Key == required).Label} for {target.Label}.");
                }
                else legacySection = LegacyReceiptDestination.Resolve(profile, record, sectionId, sheetName);
                var columnMap = target?.ColumnMap ?? legacySection!.ColumnMap;
                if (useBinding && record.Fields.Any(f => !columnMap.ContainsKey(f.Key) && !string.IsNullOrWhiteSpace(f.LiteralText)))
                    throw new FieldReviewException("UNMAPPED_FIELD", "This target has no column for a supplied field. Choose a target that can retain every entered value.");
                var values = record.Fields.Where(f => useBinding ? columnMap.ContainsKey(f.Key) : f.Kind != "accountChoice").Select(field => new InsertValue(field.Key, profile.Fields.Single(f => f.Key == field.Key).Header,
                    A1Formula.ColumnNumber(columnMap[field.Key]), field.Kind switch
                    {
                        "decimal" or "money" => field.DecimalValue is decimal number ? (object)MapNumber(number) : null,
                        "date" => field.DateValue is DateOnly date ? MapDate(date) : null,
                        _ => field.LiteralText.Length == 0 ? null : field.LiteralText
                    }, field.LiteralText, field.Kind == "date")).ToArray();
                Measure("application.mapping", watch); watch.Restart();
                var preview = useBinding
                    ? await boundWriter!.PreviewBoundInsertAsync(workbookId, binding!, document!, sectionId, values)
                    : await writer.PreviewInsertAsync(workbookId, legacySection!, values);
                Measure("application.excelPreview", watch);
                if ((document?.AdvisoryIdentifier ?? profile.Layout?.AdvisoryIdentifier) is string identifier && columnMap.TryGetValue(identifier, out var identifierColumn))
                    preview = preview with { AdvisoryWarnings = await DuplicateWarningsAsync(book, target?.HeaderSignature ?? profile.Layout!.SheetMatch,
                        record, identifier, identifierColumn, profile.Fields.Single(f => f.Key == identifier).Header) };
                if (draft)
                {
                    if (preview.ManualReview.Count > 0) throw new FieldReviewException("AMBIGUOUS_TOTAL", "A subtotal formula needs manual review in Excel before Add Row is available.");
                    if (legacySection is not null) await RejectDuplicateAsync(workbookId, legacySection, profile, record);
                }
                receipt.RecheckExcelSource(token);
                if (previews.Count >= 64) previews.Clear();
                previews[preview.Token] = new(version, book.Name, draft, workbookId, useBinding ? WorkbookBindings.Configuration(binding!) : null);
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

    public async Task<ExcelInsertDisplay> InsertAsync(DesktopReceiptReview receipt, ReviewVersion version, string previewToken, CancellationToken token, bool confirm = false) =>
        (await InsertCoreAsync(receipt, version, previewToken, token, confirm, null, null)).Change;

    internal Task<(ExcelInsertDisplay Change, ReceiptQueueState? Queue)> InsertFromQueueAsync(
        DesktopReceiptReview receipt, ReviewVersion version, string previewToken, CancellationToken token, ReceiptQueue queue, string? queueId) =>
        InsertCoreAsync(receipt, version, previewToken, token, confirm: queueId is not null, queue, queueId);

    private async Task<(ExcelInsertDisplay Change, ReceiptQueueState? Queue)> InsertCoreAsync(
        DesktopReceiptReview receipt, ReviewVersion version, string previewToken, CancellationToken token, bool confirm, ReceiptQueue? queue, string? queueId)
    {
        await commands.WaitAsync(token);
        try
        {
            return await (queue is null ? Start() : queue.StartExcel(receipt, queueId, Start));

            Task<(ExcelInsertDisplay Change, ReceiptQueueState? Queue)> Start()
            {
                if (!previews.Remove(previewToken, out var plan) || plan.Version != version)
                    throw new FieldReviewException("STALE_INSERT_PREVIEW", "Request a fresh preview for the current receipt.");
                if (plan.Draft && !confirm)
                    throw new FieldReviewException("CONFIRMATION_REQUIRED", "Explicitly confirm the draft values and destination with Add Row.");
                if (WorkflowProfiles.Hash(Profile(version.ProfileId)) != version.ProfileHash)
                    throw new FieldReviewException("STALE_PROFILE", "The local profile changed. Start a new review.");
                var binding = Binding(version.ProfileId);
                if (plan.BindingConfiguration is not null && (binding is null || plan.BindingConfiguration != WorkbookBindings.Configuration(binding)))
                    throw new FieldReviewException("STALE_INSERT_PREVIEW", "The workbook binding changed. Run Check Row again.");
                return receipt.WithVerifiedExcelAsync(version, Write, token, confirm);

                async Task<(ExcelInsertDisplay Change, ReceiptQueueState? Queue)> Write(TypedReviewRecord record)
                {
                    if (plan.BindingConfiguration is null && binding is not null && writer is IBindingSectionWriter boundWriter
                        && await boundWriter.HasBindingAsync(plan.WorkbookId, binding))
                        throw new FieldReviewException("STALE_INSERT_PREVIEW", "The workbook binding changed. Run Check Row again.");
                    receipt.RecheckExcelSource(token);
                    var sources = SourceKeys(record);
                    if (sources.Any(submittedSources.Contains)) throw new FieldReviewException("ALREADY_INSERTED", "This receipt source was already submitted. Review Excel before retrying.");
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
                    return (display, queueId is null ? null : queue!.Complete(queueId, receipt));
                }
            }
        }
        finally { commands.Release(); }
    }

    private async Task<IReadOnlyList<string>> DuplicateWarningsAsync(ExcelWorkbook book, SheetMatch signature, TypedReviewRecord record, string key,
        string identifierColumn, string label)
    {
        var reader = host as IWorkbookReader ?? throw new InvalidOperationException("Excel readback is unavailable.");
        var identifier = record.Fields.Single(f => f.Key == key).LiteralText.Trim();
        var column = A1Formula.ColumnNumber(identifierColumn);
        var warnings = new List<string>();
        foreach (var sheet in book.Sheets)
        {
            var range = await reader.UsedRangeAsync(book.Id, sheet);
            var data = await reader.ReadAsync(book.Id, sheet, range);
            if (!ExcelLayoutMatching.Matches(data, signature)) continue;
            if (Enumerable.Range(range.Row, range.Rows).Any(row => row > signature.HeaderRow &&
                string.Equals(identifier, Convert.ToString(ExcelLayoutMatching.Value(data, row, column), System.Globalization.CultureInfo.InvariantCulture)?.Trim(), StringComparison.OrdinalIgnoreCase)))
                warnings.Add($"Possible duplicate {label} in {sheet}. Review Excel; insertion is still available.");
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

    private (string Hash, int Page)[] SourceKeys(TypedReviewRecord record)
    {
        var profile = Profile(record.Version.ProfileId);
        var packet = DocumentTypes.ForProfile(profile)?.OneRecordPerPacket ?? profile.Layout?.SourceScope == "document";
        return record.Fields.Where(f => f.Source is not null).Select(f => (f.Source!.DocumentSha256, packet ? 0 : f.Source.PageNumber)).Distinct().ToArray();
    }
}
