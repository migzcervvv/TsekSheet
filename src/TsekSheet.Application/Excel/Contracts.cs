using System.Globalization;

namespace TsekSheet.Application.Excel;

// Native application contracts only. These DTOs are not WebView bridge messages.
public readonly record struct CellRange(int Row, int Column, int Rows, int Columns)
{
    public void Validate()
    {
        if (Row < 1 || Column < 1 || Rows < 1 || Columns < 1 ||
            (long)Row + Rows - 1 > 1_048_576 || (long)Column + Columns - 1 > 16_384)
            throw new ArgumentOutOfRangeException(nameof(CellRange));
    }

    public string Address
    {
        get
        {
            Validate();
            static string Col(int n)
            {
                var result = "";
                while (n > 0) { n--; result = (char)('A' + n % 26) + result; n /= 26; }
                return result;
            }
            return $"{Col(Column)}{Row.ToString(CultureInfo.InvariantCulture)}:{Col(Column + Columns - 1)}{(Row + Rows - 1).ToString(CultureInfo.InvariantCulture)}";
        }
    }

    public bool Contains(int row, int column) => row >= Row && column >= Column &&
        row < (long)Row + Rows && column < (long)Column + Columns;
}

public sealed record ExcelWorkbook(string Id, string Name, string FullPath, IReadOnlyList<string> Sheets);
public sealed record ExcelHostInfo(int ProcessId, string Version);
public sealed record RangeData(CellRange Range, object?[,] Values, string?[,] Formulas, string[,] NumberFormats);
public sealed record NamedReference(string Name, string RefersTo);
public sealed record TableReference(string Name, string Address);
public sealed record SheetSnapshot(string Name, RangeData Data, IReadOnlyList<string> MergedAreas,
    IReadOnlyList<TableReference> Tables, long ValidationCells, int ValidationAreas, int ConditionalFormats);
public sealed record PreservationSnapshot(IReadOnlyList<SheetSnapshot> Sheets, IReadOnlyList<NamedReference> Names, bool Date1904 = false);
public sealed record BackupReceipt(string Path, string Sha256);
public sealed record FillStyle(double Color, int ColorIndex, int Pattern, double PatternColor,
    int? Theme, int? PatternTheme, double Tint, double PatternTint);
public sealed record RangeFill(CellRange Bounds, FillStyle Style);
public sealed record HighlightReceipt(string Id, string WorkbookId, string Sheet, CellRange Range,
    IReadOnlyList<RangeFill> PreviousFills);
public sealed record WorkbookWriteResult(BackupReceipt Backup, HighlightReceipt Highlight,
    PreservationSnapshot Before, PreservationSnapshot After);

public interface IExcelHost : IAsyncDisposable
{
    Task<ExcelHostInfo> StartAsync();
    Task<IReadOnlyList<ExcelWorkbook>> ListWorkbooksAsync();
    Task<ExcelWorkbook> OpenAsync(string path);
    Task ActivateAsync(string workbookId, string sheet, CellRange range);
    // Only books opened by this host can be closed. Discarding edits is explicit.
    Task CloseAsync(string workbookId, bool discardChanges = false);
}

public interface IWorkbookReader
{
    Task<RangeData> ReadAsync(string workbookId, string sheet, CellRange range);
    Task<CellRange> UsedRangeAsync(string workbookId, string sheet);
    Task<PreservationSnapshot> CaptureAsync(string workbookId);
}

public interface IWorkbookWriter
{
    Task<WorkbookWriteResult> WriteAsync(string workbookId, string sheet, CellRange range, object?[,] values);
    Task<HighlightReceipt> HighlightAsync(string workbookId, string sheet, CellRange range, int color);
    Task RestoreHighlightAsync(string highlightId);
}

public sealed class ExcelBusyException(Exception? inner = null) : Exception("Excel is busy. Close its dialog or finish editing and retry.", inner);
public sealed class PreservationException(IReadOnlyList<string> differences)
    : Exception("Excel readback did not match the intended change. Review the highlighted cells before continuing.")
{
    public IReadOnlyList<string> Differences { get; } = differences;
}
