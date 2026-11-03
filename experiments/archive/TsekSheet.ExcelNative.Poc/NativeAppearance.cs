namespace TsekSheet.ExcelNative.Poc;

// Read-only DTOs for retained diagnostic captures. Native readback was removed
// from the worker after it failed cleanup qualification. No COM reader remains.
public sealed record NativeCellAppearance(string Sheet,string Address,string Font,double FontSize,int Color,int ThemeFont,
    bool Bold,bool Italic,int Underline,string NumberFormat,double Left,double Top,double Width,double Height,double RowHeight,double ColumnWidth);
public sealed record NativeSheetAppearance(string Sheet,string UsedRange,double StandardHeight,double StandardWidth);
public sealed record NativeAppearanceReport(IReadOnlyList<NativeSheetAppearance> Sheets,IReadOnlyList<NativeCellAppearance> Cells,IReadOnlyList<int>? OutstandingRcwCounts = null);

