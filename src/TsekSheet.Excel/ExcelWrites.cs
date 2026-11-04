using System.Globalization;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel;

public sealed partial class ExcelHost
{
    private sealed record HighlightState(HighlightReceipt Receipt, IReadOnlyList<RangeFill> Before);
    private readonly Dictionary<string, HighlightState> highlights = [];

    public Task<WorkbookWriteResult> WriteAsync(string workbookId, string sheet, CellRange range, object?[,] values)
    {
        range.Validate();
        var normalized = Normalize(values, range);
        return queue.InvokeAsync(() => WithSheet(workbookId, sheet, (workbook, worksheet) =>
            WriteCore(workbookId, workbook, worksheet, range, normalized)));
    }

    private WorkbookWriteResult WriteCore(string id, Interop.Workbook workbook, Interop.Worksheet sheet,
        CellRange bounds, object?[,] values)
    {
        var registration = books[id];
        if (registration.Faulted) throw new InvalidOperationException("This workbook needs manual review after a readback failure. Close and reopen it before writing again.");
        if (workbook.ReadOnly) throw new InvalidOperationException("The workbook is read-only.");
        var before = ExcelReadback.Capture(workbook);
        var current = ExcelReadback.Read(sheet, bounds);
        for (var r = 0; r < bounds.Rows; r++)
            for (var c = 0; c < bounds.Columns; c++)
                if (current.Formulas[r, c] is not null) throw new InvalidOperationException("The target contains a formula. No cells were written.");
        // A partial merged range, table formula propagation or an array/spill must not reach Value2.
        ValidateTarget(sheet, bounds);
        registration.Backup ??= backups.Create(workbook.FullName);
        var encoded = (object?[,])values.Clone();
        for (var r = 0; r < bounds.Rows; r++)
            for (var c = 0; c < bounds.Columns; c++)
                if (encoded[r, c] is string text) encoded[r, c] = "'" + text;
        try
        {
            InBatch(() =>
            {
                Interop.Range? target = null;
                try { target = ExcelReadback.Range(sheet, bounds); target.Value2 = encoded; }
                finally { ComLifetime.Release(target); }
                // Recalculate dirty formulas even when the user's original mode is manual.
                application!.Calculate();
                return true;
            });
            var highlight = HighlightCore(id, sheet, bounds, 0x99FFFF);
            var after = ExcelReadback.Capture(workbook);
            var differences = PreservationCheck.Compare(before, after, sheet.Name, bounds, values);
            if (differences.Count != 0) throw new PreservationException(differences);
            return new(registration.Backup, highlight, before, after);
        }
        catch { registration.Faulted = true; throw; }
    }

    private static object?[,] Normalize(object?[,] values, CellRange range)
    {
        ArgumentNullException.ThrowIfNull(values);
        if (values.GetLength(0) != range.Rows || values.GetLength(1) != range.Columns)
            throw new ArgumentException("Values must match the exact target dimensions.", nameof(values));
        var result = new object?[range.Rows, range.Columns];
        for (var r = 0; r < range.Rows; r++)
            for (var c = 0; c < range.Columns; c++)
            {
                var value = values[r, c];
                result[r, c] = value switch
                {
                    null => null,
                    string text when text.Length <= 32767 && !text.Contains('\0') => text,
                    bool boolean => boolean,
                    byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
                        when double.IsFinite(Convert.ToDouble(value, CultureInfo.InvariantCulture)) => Convert.ToDouble(value, CultureInfo.InvariantCulture),
                    _ => throw new ArgumentException("Use finite numbers, booleans, null, or Excel-length text values.", nameof(values))
                };
            }
        return result;
    }

    private static void ValidateTarget(Interop.Worksheet sheet, CellRange bounds, string? addedTable = null, int? addedRow = null)
    {
        Interop.Range? range = null;
        Interop.ListObjects? tables = null;
        try
        {
            range = ExcelReadback.Range(sheet, bounds);
            if (sheet.ProtectContents || range.MergeCells is not false || range.HasArray is not false || range.HasSpill is not false)
                throw new InvalidOperationException("Select unmerged, unprotected cells outside array and spill formulas.");
            // Table calculated-column propagation is an insertion concern for T2. A T1 bulk overwrite
            // cannot promise a bounded change in a table, so it rejects table intersections before writing.
            tables = sheet.ListObjects;
            for (var i = 1; i <= tables.Count; i++)
            {
                Interop.ListObject? table = null;
                Interop.Range? tableRange = null;
                try
                {
                    table = tables[i];
                    tableRange = table.Range;
                    var t = ExcelReadback.Bounds(tableRange);
                    if (bounds.Row < (long)t.Row + t.Rows && t.Row < (long)bounds.Row + bounds.Rows &&
                        bounds.Column < (long)t.Column + t.Columns && t.Column < (long)bounds.Column + bounds.Columns)
                    {
                        if (table.Name == addedTable && bounds.Rows == 1 && bounds.Row == addedRow) continue;
                        throw new InvalidOperationException("Table writes must target the data row created by the section inserter.");
                    }
                }
                finally { ComLifetime.Release(tableRange); ComLifetime.Release(table); }
            }
        }
        finally { ComLifetime.Release(tables); ComLifetime.Release(range); }
    }

    private T InBatch<T>(Func<T> operation)
    {
        var app = application!;
        var screen = app.ScreenUpdating;
        var events = app.EnableEvents;
        var calculation = app.Calculation;
        var interactive = app.Interactive;
        try
        {
            app.ScreenUpdating = false;
            app.EnableEvents = false;
            app.Calculation = Interop.XlCalculation.xlCalculationManual;
            app.Interactive = false;
            return operation();
        }
        finally
        {
            try { app.Calculation = calculation; }
            finally
            {
                try { app.EnableEvents = events; }
                finally { try { app.ScreenUpdating = screen; } finally { app.Interactive = interactive; } }
            }
        }
    }

    public Task<HighlightReceipt> HighlightAsync(string workbookId, string sheet, CellRange range, int color)
    {
        range.Validate();
        if (color is < 0 or > 0xFFFFFF) throw new ArgumentOutOfRangeException(nameof(color));
        return queue.InvokeAsync(() => WithSheet(workbookId, sheet, (workbook, worksheet) =>
        {
            var registration = books[workbookId];
            registration.Backup ??= backups.Create(workbook.FullName);
            return HighlightCore(workbookId, worksheet, range, color);
        }));
    }

    private HighlightReceipt HighlightCore(string workbookId, Interop.Worksheet sheet, CellRange bounds, int color)
    {
        var fills = new List<RangeFill>();
        CaptureFill(sheet, bounds, fills);
        var receipt = new HighlightReceipt(Guid.NewGuid().ToString("N"), workbookId, sheet.Name, bounds, fills.AsReadOnly());
        highlights.Add(receipt.Id, new(receipt, fills));
        InBatch(() =>
        {
            Interop.Range? range = null;
            Interop.Interior? interior = null;
            try
            {
                range = ExcelReadback.Range(sheet, bounds);
                interior = range.Interior;
                interior.Pattern = Interop.XlPattern.xlPatternSolid;
                interior.Color = color;
                interior.TintAndShade = 0;
                return true;
            }
            finally { ComLifetime.Release(interior); ComLifetime.Release(range); }
        });
        return receipt;
    }

    public Task RestoreHighlightAsync(string highlightId) => queue.InvokeAsync(() =>
    {
        if (!highlights.TryGetValue(highlightId, out var state)) throw new InvalidOperationException("Highlight is no longer available.");
        return WithSheet(state.Receipt.WorkbookId, state.Receipt.Sheet, (_, sheet) => InBatch(() =>
        {
            foreach (var block in state.Before) RestoreFill(sheet, block);
            highlights.Remove(highlightId);
            return true;
        }));
    });

    private static void CaptureFill(Interop.Worksheet sheet, CellRange bounds, List<RangeFill> output)
    {
        Interop.Range? range = null;
        Interop.Interior? interior = null;
        try
        {
            range = ExcelReadback.Range(sheet, bounds);
            interior = range.Interior;
            object? colorIndex = interior.ColorIndex;
            object? pattern = interior.Pattern;
            object? patternIndex = interior.PatternColorIndex;
            object? color = interior.Color;
            object? patternColor = interior.PatternColor;
            object? tint = interior.TintAndShade;
            object? patternTint = interior.PatternTintAndShade;
            if (colorIndex is not (null or DBNull) && pattern is not (null or DBNull) && patternIndex is not (null or DBNull)
                && color is not (null or DBNull) && patternColor is not (null or DBNull)
                && tint is not (null or DBNull) && patternTint is not (null or DBNull))
            {
                static int? Theme(Func<object> read)
                {
                    try { var value = Convert.ToInt32(read(), CultureInfo.InvariantCulture); return value is >= 1 and <= 12 ? value : null; }
                    catch (COMException e) when (e.HResult == unchecked((int)0x800A03EC)) { return null; }
                }
                output.Add(new(bounds, new(Convert.ToDouble(color), Convert.ToInt32(colorIndex),
                    Convert.ToInt32(pattern), Convert.ToDouble(patternColor), Theme(() => (object)interior.ThemeColor),
                    Theme(() => (object)interior.PatternThemeColor), Convert.ToDouble(tint), Convert.ToDouble(patternTint))));
                return;
            }
        }
        finally { ComLifetime.Release(interior); ComLifetime.Release(range); }
        var (a, b) = ExcelReadback.Split(bounds);
        CaptureFill(sheet, a, output);
        CaptureFill(sheet, b, output);
    }

    private static void RestoreFill(Interop.Worksheet sheet, RangeFill block)
    {
        Interop.Range? range = null;
        Interop.Interior? interior = null;
        try
        {
            range = ExcelReadback.Range(sheet, block.Bounds);
            interior = range.Interior;
            var style = block.Style;
            if (style.Theme is int theme) interior.ThemeColor = theme;
            else if (style.ColorIndex == (int)Interop.XlColorIndex.xlColorIndexNone) interior.ColorIndex = style.ColorIndex;
            else interior.Color = style.Color;
            if (style.PatternTheme is int patternTheme) interior.PatternThemeColor = patternTheme;
            else interior.PatternColor = style.PatternColor;
            interior.TintAndShade = style.Tint;
            interior.PatternTintAndShade = style.PatternTint;
            interior.Pattern = style.Pattern;
        }
        finally { ComLifetime.Release(interior); ComLifetime.Release(range); }
    }
}
