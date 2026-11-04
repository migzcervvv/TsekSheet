namespace TsekSheet.Application.Excel;

public sealed record ExcelSectionDefinition(string Id, string Label, string SheetName, int HeaderRow,
    IReadOnlyDictionary<string, string> ColumnMap, IReadOnlyList<string> FormulaColumns,
    string SubtotalColumn, string SubtotalMarker, string InsertMode, string? TableName = null);
public sealed record InsertValue(string Key, string Header, int Column, object? Value, string? DisplayValue = null, bool IsDate = false);
public sealed record FormulaReview(string Cell, string Formula, string Reason);
public sealed record SectionInsertPreview(string Token, string WorkbookId, string SectionId, string Sheet,
    string Range, int InsertRow, IReadOnlyList<InsertValue> Values, IReadOnlyList<FormulaReview> ManualReview);
public sealed record SectionInsertResult(string WorkbookId, string SectionId, string Sheet, string Range,
    IReadOnlyList<FormulaReview> ManualReview, HighlightReceipt Highlight);

public interface ISectionWriter
{
    Task<SectionInsertPreview> PreviewInsertAsync(string workbookId, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values);
    Task<SectionInsertResult> InsertAsync(string previewToken);
}

// A rejection before any insertion began; the same verified record may be previewed again.
public sealed class ExcelInsertRejectedException(string message) : InvalidOperationException(message);
