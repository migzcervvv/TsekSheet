namespace TsekSheet.Application.Excel;

public sealed record ExcelSectionDefinition(string Id, string Label, string SheetName, int HeaderRow,
    IReadOnlyDictionary<string, string> ColumnMap, IReadOnlyList<string> FormulaColumns,
    string SubtotalColumn, string SubtotalMarker, string InsertMode, string? TableName = null,
    Workflows.SheetMatch? SheetMatch = null, Workflows.SectionStart? Start = null,
    int SubtotalOccurrence = 0, bool FillBlankRowsFirst = false, IReadOnlyList<string>? InputColumns = null,
    IReadOnlyList<Workflows.RowMarker>? ProtectedRows = null, bool HybridInputs = false, Workflows.RowMarker? SubtotalLocator = null, IReadOnlyList<string>? HybridFormulaColumns = null,
    IReadOnlyList<Workflows.RowMarker>? StructureMarkers = null, bool BlankHybridNewRows = false);
public sealed record InsertValue(string Key, string Header, int Column, object? Value, string? DisplayValue = null, bool IsDate = false);
public sealed record FormulaReview(string Cell, string Formula, string Reason);
public sealed record SectionInsertPreview(string Token, string WorkbookId, string SectionId, string Sheet,
    string Range, int InsertRow, IReadOnlyList<InsertValue> Values, IReadOnlyList<FormulaReview> ManualReview, IReadOnlyList<string>? AdvisoryWarnings = null);
public sealed record SectionInsertResult(string WorkbookId, string SectionId, string Sheet, string Range,
    IReadOnlyList<FormulaReview> ManualReview, HighlightReceipt Highlight);

// Optional local diagnostic observation; contains no workbook or receipt contents.
public sealed record PreviewStageTiming(string Stage, double Milliseconds);

public interface ISectionWriter
{
    Task<SectionInsertPreview> PreviewInsertAsync(string workbookId, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values);
    Task<SectionInsertResult> InsertAsync(string previewToken);
}

// A rejection before any insertion began; the same verified record may be previewed again.
public sealed class ExcelInsertRejectedException(string message) : InvalidOperationException(message);
