using System.Diagnostics;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel.Tests;

internal sealed record ExcelFixture(string Root, string Path)
{
    internal static async Task<ExcelFixture> CreateAsync()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        var root = System.IO.Path.Combine(directory.FullName, "fixtures", "excel", "t1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "fixture.xlsx");
        await using var queue = new StaCommandQueue();
        await queue.InvokeAsync(() => { Author(path); return true; });
        return new(root, path);
    }

    private static void Author(string path)
    {
        var previous = new HashSet<int>();
        foreach (var p in Process.GetProcessesByName("EXCEL")) using (p) previous.Add(p.Id);
        Interop.Application? app = null;
        Interop.Workbooks? books = null;
        Interop.Workbook? book = null;
        Interop.Sheets? sheets = null;
        Interop.Worksheet? sheet = null, other = null;
        var owned = false;
        try
        {
            app = new Interop.Application();
            GetWindowThreadProcessId((nint)app.Hwnd, out var pid);
            if (previous.Contains((int)pid) || pid == 0) throw new InvalidOperationException("Fixture Excel is not isolated.");
            owned = true;
            app.Visible = true;
            app.UserControl = true;
            app.AutomationSecurity = Microsoft.Office.Core.MsoAutomationSecurity.msoAutomationSecurityForceDisable;
            books = app.Workbooks;
            book = books.Add(Interop.XlWBATemplate.xlWBATWorksheet);
            sheets = book.Worksheets;
            sheet = (Interop.Worksheet)(object)sheets[1];
            sheet.Name = "Entry";
            other = (Interop.Worksheet)(object)sheets.Add(After: sheet);
            other.Name = "Other";
            var screen = app.ScreenUpdating;
            var events = app.EnableEvents;
            var calculation = app.Calculation;
            try
            {
                app.ScreenUpdating = false;
                app.EnableEvents = false;
                app.Calculation = Interop.XlCalculation.xlCalculationManual;
                var data = new object?[13, 8];
                data[0, 0] = "Left section";
                data[1, 0] = "Item"; data[1, 1] = "Amount"; data[1, 2] = "Double";
                for (var i = 0; i < 10; i++) { data[i + 2, 0] = "Synthetic " + i; data[i + 2, 1] = (double)(i + 1); }
                data[12, 0] = "Subtotal";
                data[0, 5] = "Code"; data[0, 6] = "Amount"; data[0, 7] = "Note";
                for (var i = 1; i <= 3; i++) { data[i, 5] = "Right " + i; data[i, 6] = i * 100d; data[i, 7] = "Unchanged"; }
                Put(sheet, new(1, 1, 13, 8), r => r.Value2 = data);
                var formulas = new object[10, 1];
                for (var i = 0; i < 10; i++) formulas[i, 0] = $"=B{i + 3}*2";
                Put(sheet, new(3, 3, 10, 1), r => r.Formula2 = formulas);
                Put(sheet, new(13, 2, 1, 1), r => r.Formula2 = new object[,] { { "=SUM(B3:B12)" } });
                Put(sheet, new(3, 2, 10, 1), r => r.NumberFormat = "0.00");
                Put(sheet, new(3, 3, 10, 1), r => r.NumberFormat = "0.000");
                Put(sheet, new(15, 1, 1, 3), r => { r.Merge(); r.Value2 = new object[,] { { "Merged note", null!, null! } }; });
                Put(other, new(1, 1, 2, 2), r => r.Value2 = new object[,] { { "Other sheet", "Keep" }, { 7d, 9d } });
                AddStructures(book, sheet);
                SetFill(sheet, new(3, 2, 5, 1), false);
                SetFill(sheet, new(8, 2, 5, 1), true);
            }
            finally
            {
                try { app.Calculation = calculation; }
                finally { try { app.EnableEvents = events; } finally { app.ScreenUpdating = screen; } }
            }
            book.SaveAs(path, Interop.XlFileFormat.xlOpenXMLWorkbook);
        }
        finally
        {
            ComLifetime.Release(other); ComLifetime.Release(sheet); ComLifetime.Release(sheets);
            try { if (book is not null) book.Close(SaveChanges: false); }
            finally
            {
                ComLifetime.Release(book);
                try { if (owned && books is not null && books.Count == 0) app!.Quit(); }
                finally { ComLifetime.Release(books); ComLifetime.Release(app); }
            }
        }
    }

    private static void Put(Interop.Worksheet sheet, CellRange bounds, Action<Interop.Range> write)
    {
        Interop.Range? range = null;
        try { range = sheet.Range[bounds.Address]; write(range); }
        finally { ComLifetime.Release(range); }
    }

    private static void AddStructures(Interop.Workbook book, Interop.Worksheet sheet)
    {
        Interop.ListObjects? tables = null;
        Interop.Range? right = null, input = null;
        Interop.ListObject? table = null;
        Interop.Validation? validation = null;
        Interop.FormatConditions? conditions = null;
        Interop.FormatCondition? condition = null;
        Interop.Names? names = null;
        Interop.Name? name = null;
        try
        {
            tables = sheet.ListObjects;
            right = sheet.Range["F1:H4"];
            table = tables.Add(Interop.XlListObjectSourceType.xlSrcRange, right, XlListObjectHasHeaders: Interop.XlYesNoGuess.xlYes);
            table.Name = "RightSection";
            input = sheet.Range["B3:B12"];
            validation = input.Validation;
            validation.Add(Interop.XlDVType.xlValidateDecimal, Interop.XlDVAlertStyle.xlValidAlertStop, Interop.XlFormatConditionOperator.xlBetween, "0", "10000");
            conditions = input.FormatConditions;
            condition = (Interop.FormatCondition)(object)conditions.Add(Interop.XlFormatConditionType.xlCellValue, Interop.XlFormatConditionOperator.xlGreater, "500");
            names = book.Names;
            name = names.Add("InputValues", "=Entry!$B$3:$B$12");
        }
        finally
        {
            ComLifetime.Release(name); ComLifetime.Release(names); ComLifetime.Release(condition); ComLifetime.Release(conditions);
            ComLifetime.Release(validation); ComLifetime.Release(input); ComLifetime.Release(table); ComLifetime.Release(right); ComLifetime.Release(tables);
        }
    }

    private static void SetFill(Interop.Worksheet sheet, CellRange bounds, bool theme)
    {
        Interop.Range? range = null;
        Interop.Interior? fill = null;
        try
        {
            range = sheet.Range[bounds.Address];
            fill = range.Interior;
            fill.Pattern = Interop.XlPattern.xlPatternSolid;
            if (theme) { fill.ThemeColor = Interop.XlThemeColor.xlThemeColorAccent2; fill.TintAndShade = 0.4; }
            else fill.Color = 0x1122CC;
        }
        finally { ComLifetime.Release(fill); ComLifetime.Release(range); }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
