using System.Globalization;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel;

internal static class ExcelReadback
{
    internal static Interop.Range Range(Interop.Worksheet sheet, CellRange bounds) => sheet.Range[bounds.Address];

    internal static CellRange Bounds(Interop.Range range)
    {
        Interop.Range? rows = null, columns = null;
        try
        {
            rows = range.Rows;
            columns = range.Columns;
            return new(range.Row, range.Column, rows.Count, columns.Count);
        }
        finally { ComLifetime.Release(columns); ComLifetime.Release(rows); }
    }

    internal static CellRange Used(Interop.Worksheet sheet)
    {
        Interop.Range? used = null;
        try { used = sheet.UsedRange; return Bounds(used); }
        finally { ComLifetime.Release(used); }
    }

    internal static RangeData Read(Interop.Worksheet sheet, CellRange bounds)
    {
        bounds.Validate();
        Interop.Range? range = null, formulaCells = null;
        Interop.Areas? areas = null;
        try
        {
            range = Range(sheet, bounds);
            var values = Matrix((object?)range.Value2, bounds.Rows, bounds.Columns);
            var formulas = new string?[bounds.Rows, bounds.Columns];
            if (range.HasFormula is not false)
            {
                formulaCells = range.SpecialCells(Interop.XlCellType.xlCellTypeFormulas);
                areas = formulaCells.Areas;
                for (var i = 1; i <= areas.Count; i++)
                {
                    Interop.Range? area = null;
                    try
                    {
                        area = areas[i];
                        var a = Bounds(area);
                        var entries = Matrix((object?)area.Formula2, a.Rows, a.Columns);
                        // SpecialCells on a single cell can search the entire used range.
                        for (var r = 0; r < a.Rows; r++)
                            for (var c = 0; c < a.Columns; c++)
                                if (bounds.Contains(a.Row + r, a.Column + c))
                                    formulas[a.Row + r - bounds.Row, a.Column + c - bounds.Column] = (string?)entries[r, c];
                    }
                    finally { ComLifetime.Release(area); }
                }
            }
            var formats = new string[bounds.Rows, bounds.Columns];
            ReadFormats(sheet, bounds, bounds, formats);
            return new(bounds, values, formulas, formats);
        }
        finally { ComLifetime.Release(areas); ComLifetime.Release(formulaCells); ComLifetime.Release(range); }
    }

    internal static object?[,] Matrix(object? value, int rows, int columns)
    {
        var result = new object?[rows, columns];
        if (value is Array array)
        {
            var r0 = array.GetLowerBound(0);
            var c0 = array.GetLowerBound(1);
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < columns; c++) result[r, c] = array.GetValue(r + r0, c + c0);
        }
        else result[0, 0] = value;
        return result;
    }

    private static void ReadFormats(Interop.Worksheet sheet, CellRange part, CellRange whole, string[,] output)
    {
        Interop.Range? range = null;
        try
        {
            range = Range(sheet, part);
            var format = range.NumberFormat as string;
            if (format is not null)
            {
                for (var r = 0; r < part.Rows; r++)
                    for (var c = 0; c < part.Columns; c++) output[part.Row + r - whole.Row, part.Column + c - whole.Column] = format;
                return;
            }
        }
        finally { ComLifetime.Release(range); }
        // Excel exposes NumberFormat as a scalar, null for mixed formats (no bulk matrix API).
        // Partition homogeneous rectangles; values and formulas are always transferred in arrays.
        var (first, second) = Split(part);
        ReadFormats(sheet, first, whole, output);
        ReadFormats(sheet, second, whole, output);
    }

    internal static (CellRange First, CellRange Second) Split(CellRange part)
    {
        if (part.Rows > 1 && part.Rows >= part.Columns)
        {
            var n = part.Rows / 2;
            return (part with { Rows = n }, part with { Row = part.Row + n, Rows = part.Rows - n });
        }
        if (part.Columns > 1)
        {
            var n = part.Columns / 2;
            return (part with { Columns = n }, part with { Column = part.Column + n, Columns = part.Columns - n });
        }
        throw new InvalidOperationException("Excel returned mixed metadata for a single cell.");
    }

    internal static PreservationSnapshot Capture(Interop.Workbook workbook)
    {
        Interop.Sheets? sheets = null;
        Interop.Names? names = null;
        try
        {
            sheets = workbook.Worksheets;
            var sheetData = new List<SheetSnapshot>();
            for (var i = 1; i <= sheets.Count; i++)
            {
                Interop.Worksheet? sheet = null;
                try { sheet = (Interop.Worksheet)(object)sheets[i]; sheetData.Add(CaptureSheet(sheet)); }
                finally { ComLifetime.Release(sheet); }
            }
            names = workbook.Names;
            var nameData = new List<NamedReference>();
            for (var i = 1; i <= names.Count; i++)
            {
                Interop.Name? name = null;
                try { name = names.Item(i); nameData.Add(new(name.Name, (string)(object)name.RefersTo)); }
                finally { ComLifetime.Release(name); }
            }
            return new(sheetData, nameData.OrderBy(n => n.Name, StringComparer.Ordinal).ToArray(), workbook.Date1904);
        }
        finally { ComLifetime.Release(names); ComLifetime.Release(sheets); }
    }

    private static SheetSnapshot CaptureSheet(Interop.Worksheet sheet)
    {
        Interop.ListObjects? tables = null;
        Interop.Range? cells = null, validation = null;
        Interop.Areas? validationAreas = null;
        Interop.FormatConditions? conditions = null;
        try
        {
            var used = Used(sheet);
            var data = Read(sheet, used);
            var merged = new HashSet<string>(StringComparer.Ordinal);
            ReadMerges(sheet, used, merged);
            tables = sheet.ListObjects;
            var tableData = new List<TableReference>();
            for (var i = 1; i <= tables.Count; i++)
            {
                Interop.ListObject? table = null;
                Interop.Range? tableRange = null;
                try
                {
                    table = tables[i];
                    tableRange = table.Range;
                    tableData.Add(new(table.Name, Bounds(tableRange).Address));
                }
                finally { ComLifetime.Release(tableRange); ComLifetime.Release(table); }
            }
            cells = sheet.Cells;
            conditions = cells.FormatConditions;
            long validationCount = 0;
            var areaCount = 0;
            try { validation = cells.SpecialCells(Interop.XlCellType.xlCellTypeAllValidation); }
            catch (COMException e) when (e.HResult == unchecked((int)0x800A03EC)) { /* No validation cells. */ }
            if (validation is not null)
            {
                validationCount = Convert.ToInt64((object)validation.CountLarge, CultureInfo.InvariantCulture);
                validationAreas = validation.Areas;
                areaCount = validationAreas.Count;
            }
            return new(sheet.Name, data, merged.Order(StringComparer.Ordinal).ToArray(),
                tableData.OrderBy(t => t.Name, StringComparer.Ordinal).ToArray(), validationCount, areaCount, conditions.Count);
        }
        finally
        {
            ComLifetime.Release(validationAreas); ComLifetime.Release(validation); ComLifetime.Release(conditions);
            ComLifetime.Release(cells); ComLifetime.Release(tables);
        }
    }

    private static void ReadMerges(Interop.Worksheet sheet, CellRange bounds, HashSet<string> output)
    {
        Interop.Range? range = null, first = null, merged = null;
        try
        {
            range = Range(sheet, bounds);
            if (range.MergeCells is false) return;
            first = Range(sheet, new(bounds.Row, bounds.Column, 1, 1));
            merged = first.MergeArea;
            var area = Bounds(merged);
            if (area.Rows > 1 || area.Columns > 1)
            {
                output.Add(area.Address);
                if (area.Contains(bounds.Row, bounds.Column) &&
                    area.Contains(bounds.Row + bounds.Rows - 1, bounds.Column + bounds.Columns - 1)) return;
            }
            if (bounds.Rows == 1 && bounds.Columns == 1) return;
        }
        finally { ComLifetime.Release(merged); ComLifetime.Release(first); ComLifetime.Release(range); }
        var (a, b) = Split(bounds);
        ReadMerges(sheet, a, output);
        ReadMerges(sheet, b, output);
    }
}
