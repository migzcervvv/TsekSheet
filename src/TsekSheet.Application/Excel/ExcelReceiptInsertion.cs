using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Excel;

public sealed record ExcelWorkbookChoice(string Id, string Name, IReadOnlyList<string> Sheets);
public sealed record ExcelSectionChoice(string Id, string Label, string SheetName);
public sealed record ExcelInsertDisplay(string Workbook, string Sheet, string Range, IReadOnlyList<FormulaReview> ManualReview);

public sealed class ExcelReceiptInsertion(IExcelHost host, ISectionWriter writer)
{
    private readonly Dictionary<string, (ReviewVersion Version, SectionInsertPreview Preview, string Workbook)> previews = [];
    private readonly HashSet<(Guid Review, int Revision)> inserted = [];
    private readonly SemaphoreSlim commands = new(1, 1);
    private readonly List<ExcelInsertDisplay> changes = [];
    public IReadOnlyList<ExcelInsertDisplay> Changes { get { lock (changes) return changes.ToArray(); } }

    public async Task<IReadOnlyList<ExcelWorkbookChoice>> ListAsync(string? path = null)
    {
        await commands.WaitAsync();
        try
        {
            if (path is not null) await host.OpenAsync(path);
            return (await host.ListWorkbooksAsync()).Select(w => new ExcelWorkbookChoice(w.Id, w.Name, w.Sheets)).ToArray();
        }
        finally { commands.Release(); }
    }

    public static IReadOnlyList<ExcelSectionChoice> Sections(string profileId) =>
        (WorkflowProfiles.Get(profileId).ExcelSections ?? []).Select(s => new ExcelSectionChoice(s.Id, s.Label, s.SheetName)).ToArray();

    public async Task<SectionInsertPreview> PreviewAsync(DesktopReceiptReview receipt, ReviewVersion version, string workbookId, string sectionId, CancellationToken token)
    {
        await commands.WaitAsync(token);
        try
        {
            return await receipt.WithVerifiedExcelAsync(version, async record =>
            {
                if (inserted.Contains((version.ReviewId, version.Revision))) throw new FieldReviewException("ALREADY_INSERTED", "This verified revision was already inserted. Review the highlighted row in Excel.");
                var profile = WorkflowProfiles.Get(version.ProfileId);
                var section = profile.ExcelSections?.SingleOrDefault(s => s.Id == sectionId) ?? throw new FieldReviewException("UNKNOWN_SECTION", "Choose a section from this receipt profile.");
                var values = record.Fields.Select(field => new InsertValue(field.Key, profile.Fields.Single(f => f.Key == field.Key).Header,
                    A1Formula.ColumnNumber(section.ColumnMap[field.Key]), field.Kind switch
                    {
                        "decimal" => field.DecimalValue is decimal number ? (object)MapNumber(number) : null,
                        "date" => field.DateValue is DateOnly date ? MapDate(date) : null,
                        _ => field.LiteralText.Length == 0 ? null : field.LiteralText
                    }, field.LiteralText, field.Kind == "date")).ToArray();
                var book = (await host.ListWorkbooksAsync()).SingleOrDefault(w => w.Id == workbookId) ?? throw new InvalidOperationException("Choose an open Excel workbook.");
                var preview = await writer.PreviewInsertAsync(workbookId, section, values);
                if (previews.Count >= 64) previews.Clear();
                previews[preview.Token] = (version, preview, book.Name);
                return preview;
            }, token);
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

    public async Task<ExcelInsertDisplay> InsertAsync(DesktopReceiptReview receipt, ReviewVersion version, string previewToken, CancellationToken token)
    {
        await commands.WaitAsync(token);
        try
        {
            if (!previews.Remove(previewToken, out var plan) || plan.Version != version) throw new FieldReviewException("STALE_INSERT_PREVIEW", "Request a fresh preview for the current receipt.");
            return await receipt.WithVerifiedExcelAsync(version, async _ =>
            {
                if (!inserted.Add((version.ReviewId, version.Revision))) throw new FieldReviewException("ALREADY_INSERTED", "This verified revision was already submitted. Review Excel before retrying.");
                // Once submitted, a failed/unknown outcome cannot be retried with the same revision.
                SectionInsertResult result;
                try { result = await writer.InsertAsync(previewToken); }
                catch (ExcelInsertRejectedException error)
                {
                    inserted.Remove((version.ReviewId, version.Revision));
                    throw new FieldReviewException("STALE_INSERT_PREVIEW", error.Message);
                }
                var display = new ExcelInsertDisplay(plan.Workbook, result.Sheet, result.Range, result.ManualReview);
                lock (changes) { if (changes.Count >= 128) changes.RemoveAt(0); changes.Add(display); }
                return display;
            }, token);
        }
        finally { commands.Release(); }
    }
}
