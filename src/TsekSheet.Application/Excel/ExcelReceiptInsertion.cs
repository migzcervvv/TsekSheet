using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Excel;

public sealed record ExcelWorkbookChoice(string Id, string Name, IReadOnlyList<string> Sheets);
public sealed record ExcelSectionChoice(string Id, string Label, string SheetName);
public sealed record ExcelInsertDisplay(string Workbook, string Sheet, string Range, IReadOnlyList<FormulaReview> ManualReview);

public sealed class ExcelReceiptInsertion(IExcelHost host, ISectionWriter writer, Func<string, WorkflowProfile>? profiles = null)
{
    private WorkflowProfile Profile(string id) => (profiles ?? WorkflowProfiles.Get)(id);
    private readonly Dictionary<string, (ReviewVersion Version, SectionInsertPreview Preview, string Workbook, bool Draft)> previews = [];
    private readonly HashSet<Guid> inserted = [];
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly List<ExcelInsertDisplay> changes = [];
    public IReadOnlyList<ExcelInsertDisplay> Changes { get { lock (changes) return changes.ToArray(); } }

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
        await commands.WaitAsync(token);
        try
        {
            return await (draft ? receipt.WithDraftExcelAsync(version, Build, token) : receipt.WithVerifiedExcelAsync(version, Build, token));
            async Task<SectionInsertPreview> Build(TypedReviewRecord record)
            {
                if (inserted.Contains(version.ReviewId)) throw new FieldReviewException("ALREADY_INSERTED", "This receipt was already inserted or submitted. Review the highlighted row in Excel.");
                var profile = Profile(version.ProfileId);
                if (WorkflowProfiles.Hash(profile) != version.ProfileHash) throw new FieldReviewException("STALE_PROFILE", "The local profile changed. Start a new review.");
                var section = profile.Layout is { } layout
                    ? layout.Section(sectionId, sheetName ?? throw new FieldReviewException("SHEET_REQUIRED", "Choose a matching sheet."))
                    : profile.ExcelSections?.SingleOrDefault(s => s.Id == sectionId) ?? throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section from this receipt profile.");
                if (sheetName is not null && section.SheetName != sheetName) throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section on the selected sheet.");
                if (profile.Layout is { } accountLayout)
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
                var book = (await host.ListWorkbooksAsync()).SingleOrDefault(w => w.Id == workbookId) ?? throw new InvalidOperationException("Choose an open Excel workbook.");
                var preview = await writer.PreviewInsertAsync(workbookId, section, values);
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
        finally { commands.Release(); }
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
            return await receipt.WithVerifiedExcelAsync(version, async _ =>
            {
                if (!inserted.Add(version.ReviewId)) throw new FieldReviewException("ALREADY_INSERTED", "This receipt was already submitted. Review Excel before retrying.");
                // Once submitted, a failed/unknown outcome cannot be retried as another revision.
                SectionInsertResult result;
                try { result = await writer.InsertAsync(previewToken); }
                catch (ExcelInsertRejectedException error)
                {
                    inserted.Remove(version.ReviewId);
                    throw new FieldReviewException("STALE_INSERT_PREVIEW", error.Message);
                }
                var display = new ExcelInsertDisplay(plan.Workbook, result.Sheet, result.Range, result.ManualReview);
                lock (changes) { if (changes.Count >= 128) changes.RemoveAt(0); changes.Add(display); }
                return display;
            }, token);
        }
        finally { commands.Release(); }
    }

    private async Task RejectDuplicateAsync(string workbookId, ExcelSectionDefinition section, WorkflowProfile profile, TypedReviewRecord record)
    {
        var reader = host as IWorkbookReader ?? throw new InvalidOperationException("Excel readback is unavailable.");
        var data = await reader.ReadAsync(workbookId, section.SheetName, await reader.UsedRangeAsync(workbookId, section.SheetName));
        var boundary = ExcelLayoutMatching.Resolve(data, section);
        var identifiers = profile.IdentifierFields.Count > 0 ? profile.IdentifierFields
            : profile.Fields.Where(f => f.Required && f.Kind == "text").Take(1).Select(f => f.Key).ToArray();
        for (var row = boundary.First; row < boundary.Subtotal; row++)
        {
            foreach (var key in identifiers)
            {
                var value = record.Fields.Single(f => f.Key == key).LiteralText.Trim();
                if (value.Length == 0 || !section.ColumnMap.TryGetValue(key, out var column)) continue;
                var existing = Convert.ToString(ExcelLayoutMatching.Value(data, row, A1Formula.ColumnNumber(column)), System.Globalization.CultureInfo.InvariantCulture)?.Trim();
                if (StringComparer.OrdinalIgnoreCase.Equals(value, existing))
                    throw new FieldReviewException("DUPLICATE_RECORD", $"Duplicate {profile.Fields.Single(f => f.Key == key).Header} at row {row}. Review the existing Excel row.");
            }
            // Layout profiles expose invoice/vendor fields without legacy IdentifierFields.
            // Missing vendor identity cannot safely distinguish two equal invoice references.
            if (profile.Layout is not null && Match("invoice", out var invoice) && invoice)
            {
                var hasVendor = Match("vendor", out var sameVendor);
                if (!hasVendor || sameVendor)
                    throw new FieldReviewException("DUPLICATE_RECORD", $"Potential duplicate invoice at row {row}. Review the existing vendor and invoice in Excel.");
            }
            bool Match(string key, out bool same)
            {
                same = false;
                var value = record.Fields.SingleOrDefault(f => f.Key == key)?.LiteralText.Trim();
                if (string.IsNullOrEmpty(value) || !section.ColumnMap.TryGetValue(key, out var col)) return false;
                var existing = Convert.ToString(ExcelLayoutMatching.Value(data, row, A1Formula.ColumnNumber(col)), System.Globalization.CultureInfo.InvariantCulture)?.Trim();
                if (string.IsNullOrEmpty(existing)) return false;
                same = StringComparer.OrdinalIgnoreCase.Equals(value, existing); return true;
            }
        }
    }
}
