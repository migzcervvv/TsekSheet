using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using TsekSheet.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.TestFixtures;

internal static class LiquidationFixture
{
    internal static async Task<string> CreateAsync(ExcelHost host)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        var root = Path.Combine(directory!.FullName, "fixtures", "excel", "t1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "liquidation-replica.xlsx");
        await host.StartAsync();
        // Fixture authoring borrows the host's STA; no RCW crosses the command boundary.
        var queue = typeof(ExcelHost).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        var invoke = queue.GetType().GetMethod("InvokeAsync")!.MakeGenericMethod(typeof(int));
        await (Task<int>)invoke.Invoke(queue, [new Func<int>(() => Author(host, path))])!;
        return path;
    }

    private static int Author(ExcelHost host, string path)
    {
        var app = (Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        Interop.Workbooks? books = null; Interop.Workbook? book = null; Interop.Sheets? sheets = null;
        try
        {
            books = app.Workbooks; book = books.Add(Interop.XlWBATemplate.xlWBATWorksheet); sheets = book.Worksheets;
            var screen = app.ScreenUpdating; var events = app.EnableEvents; var calculation = app.Calculation;
            try
            {
                app.ScreenUpdating = false; app.EnableEvents = false; app.Calculation = Interop.XlCalculation.xlCalculationManual;
                for (var index = 0; index < 3; index++)
                {
                    Interop.Worksheet? sheet = null; Interop.PageSetup? setup = null;
                    try
                    {
                        sheet = index == 0 ? (Interop.Worksheet)(object)sheets[1] : (Interop.Worksheet)(object)sheets.Add();
                        sheet.Name = index == 2 ? "VENDOR" : "Person " + (index + 1);
                        if (index == 2)
                        {
                            Put(sheet, new(1, 1, 3, 3), r => r.Value2 = new object[,] { { "Name", "TIN", "Address" }, { "Fake vendor A", "FAKE-001", "Synthetic street" }, { "Fake vendor B", "FAKE-002", "Test avenue" } });
                            continue;
                        }
                        var data = new object?[43, 32];
                        data[0, 1] = "Synthetic liquidation " + index;
                        data[1, 2] = "PARTICULARS"; data[1, 3] = "VENDOR"; data[1, 4] = "TIN"; data[1, 6] = "INVOICE";
                        for (var row = 4; row <= 28; row++)
                        {
                            data[row - 1, 1] = "F-" + row; data[row - 1, 2] = "Fake expense " + row;
                            data[row - 1, 3] = "Fake vendor"; data[row - 1, 9] = 10d;
                        }
                        data[30, 2] = "TOTAL"; data[32, 2] = "Credits";
                        data[33, 1] = "C-1"; data[33, 2] = "Fake credit"; data[33, 16] = 5d;
                        data[36, 2] = "TOTAL credit"; data[39, 2] = "TOTAL";
                        Put(sheet, new(1, 1, 43, 32), r => r.Value2 = data);
                        AddFormulas(sheet, 4, 30); AddFormulas(sheet, 34, 36);
                        foreach (var row in new[] { 31, 37 })
                        {
                            var first = row == 31 ? 4 : 34;
                            var formulas = new object?[1, 23];
                            for (var c = 10; c <= 32; c++)
                            {
                                var col = new CellRange(1, c, 1, 1).Address.Split('1')[0];
                                formulas[0, c - 10] = $"=SUM({col}{first}:{col}{row - 1})";
                            }
                            Put(sheet, new(row, 10, 1, 23), r => r.Formula2 = formulas);
                        }
                        Put(sheet, new(40, 32, 1, 1), r => r.Formula2 = new object[,] { { "=AF31-AF37" } });
                        Put(sheet, new(4, 10, 37, 23), r => r.NumberFormat = "0.00");
                        setup = sheet.PageSetup;
                        setup.Orientation = Interop.XlPageOrientation.xlLandscape;
                        setup.PaperSize = Interop.XlPaperSize.xlPaperA4;
                        setup.PrintArea = "$B$2:$AF$43"; setup.PrintTitleRows = "$2:$2";
                    }
                    finally { Release(setup); Release(sheet); }
                }
                app.Calculate(); book.SaveAs(path, Interop.XlFileFormat.xlOpenXMLWorkbook);
            }
            finally { try { app.Calculation = calculation; } finally { try { app.EnableEvents = events; } finally { app.ScreenUpdating = screen; } } }
            return 0;
        }
        finally
        {
            Release(sheets);
            try { book?.Close(SaveChanges: false); }
            finally { Release(book); Release(books); }
        }
    }

    private static void AddFormulas(Interop.Worksheet sheet, int first, int last)
    {
        foreach (var col in new[] { 14, 15, 32 })
        {
            var formulas = new object[last - first + 1, 1];
            for (var row = first; row <= last; row++) formulas[row - first, 0] = col == 14 ? $"=SUM(J{row}:M{row})*0.12" : col == 15 ? $"=SUM(J{row}:N{row})" : $"=O{row}+SUM(Q{row}:AE{row})";
            Put(sheet, new(first, col, last - first + 1, 1), r => r.Formula2 = formulas);
        }
    }
    private static void Put(Interop.Worksheet sheet, CellRange range, Action<Interop.Range> action)
    {
        Interop.Range? target = null;
        try { target = sheet.Range[range.Address]; action(target); }
        finally { Release(target); }
    }
    private static void Release(object? value) { if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
}
