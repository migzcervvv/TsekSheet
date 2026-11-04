using System.Diagnostics;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;
using ExcelLifecycle.Diagnostics;

namespace TsekSheet.Excel.Tests;

internal sealed record ExcelFixture(string Root, string Path)
{
    private static readonly Dictionary<int, Process> ownedProcesses = [];
    private static readonly Dictionary<int, DateTime> quitTimes = [];
    internal static int MeasurementCycle { get; set; }

    internal static void AssertNoPreviousOwnedInstance()
    {
        var alive = ownedProcesses.Where(p => !ExitObservation.WaitForExit(p.Value, 0)).Select(p => p.Key).ToArray();
        Console.Error.WriteLine($"{DateTime.UtcNow:O} previous owned instances alive at start: {string.Join(",", alive)} (count={alive.Length})");
        if (alive.Length != 0) throw new InvalidOperationException("Host start found a previous owned Excel instance alive.");
    }

    internal static void TrackProcess(int pid)
    {
        var process = Process.GetProcessById(pid);
        _ = process.SafeHandle;
        ownedProcesses.Add(pid, process);
        Console.Error.WriteLine($"{DateTime.UtcNow:O} owned PID={pid}, start={process.StartTime.ToUniversalTime():O}");
    }

    internal static async Task<ExcelFixture> CreateAsync(bool sections = false, bool unsupportedSubtotal = false, bool date1904 = false)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(System.IO.Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        if (directory is null) throw new InvalidOperationException("Repository root not found.");
        var root = System.IO.Path.Combine(directory.FullName, "fixtures", "excel", "t1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = System.IO.Path.Combine(root, "fixture.xlsx");
        await using var queue = new StaCommandQueue();
        var pid = await queue.InvokeAsync(() => Author(path, sections, unsupportedSubtotal, date1904));
        await queue.DisposeAsync();
        Console.Error.WriteLine($"{DateTime.UtcNow:O} fixture PID={pid}: STA exited");
        LifecycleWorkflowDiagnostics.Current?.OwnerThreadStopped(pid, queue);
        try { await WaitForExitAsync(pid); }
        catch (ExcelExitDeadlineException error) { error.Fixture = new(root, path); throw; }
        return new(root, path);
    }

    private static int Author(string path, bool sections, bool unsupportedSubtotal, bool date1904)
    {
        AssertNoPreviousOwnedInstance();
        var previous = new HashSet<int>();
        foreach (var p in Process.GetProcessesByName("EXCEL")) using (p) previous.Add(p.Id);
        Interop.Application? app = null;
        Interop.Workbooks? books = null;
        Interop.Workbook? book = null;
        Interop.Sheets? sheets = null;
        Interop.Worksheet? sheet = null, other = null;
        var owned = false;
        var ownedProcess = 0;
        try
        {
            app = new Interop.Application();
            var nativeThread = GetWindowThreadProcessId((nint)app.Hwnd, out var pid);
            if (previous.Contains((int)pid) || pid == 0) throw new InvalidOperationException("Fixture Excel is not isolated.");
            owned = true;
            ownedProcess = (int)pid;
            TrackProcess((int)pid);
            Console.Error.WriteLine($"Fixture Excel owned: PID={pid}, native thread={nativeThread}, managed thread={Environment.CurrentManagedThreadId}, apartment={Thread.CurrentThread.GetApartmentState()}.");
            app.Visible = true;
            app.UserControl = true;
            app.AutomationSecurity = Microsoft.Office.Core.MsoAutomationSecurity.msoAutomationSecurityForceDisable;
            LifecycleWorkflowDiagnostics.Current?.FixtureCreated((int)pid, nativeThread, app.Version, Convert.ToString(app.Build)!);
            books = app.Workbooks;
            book = books.Add(Interop.XlWBATemplate.xlWBATWorksheet);
            book.Date1904 = date1904;
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
            if (sections) AddSectionSheets(app, book);
            if (unsupportedSubtotal)
            {
                Interop.Worksheet? columns = null;
                try { columns = (Interop.Worksheet)(object)sheets["Columns"]; Put(columns, new(5, 7, 1, 1), range => range.Formula2 = new object[,] { { "=SUM(Records[Derived])" } }); }
                finally { ComLifetime.Release(columns); }
            }
            book.SaveAs(path, Interop.XlFileFormat.xlOpenXMLWorkbook);
            return (int)pid;
        }
        finally
        {
            ComLifetime.Release(other); ComLifetime.Release(sheet); ComLifetime.Release(sheets);
            try
            {
                if (book is not null)
                {
                    book.Close(SaveChanges: false);
                    Console.Error.WriteLine($"{DateTime.UtcNow:O} fixture PID={ownedProcess}: Close SaveChanges=false returned");
                }
            }
            finally
            {
                ComLifetime.Release(book);
                try
                {
                    if (owned && books is not null && books.Count == 0)
                    {
                        app!.Quit();
                        quitTimes.Add(ownedProcess, DateTime.UtcNow);
                        LifecycleWorkflowDiagnostics.Current?.FixtureQuit(ownedProcess);
                        Console.Error.WriteLine($"{DateTime.UtcNow:O} fixture PID={ownedProcess}: Quit returned with zero open workbooks");
                    }
                }
                finally
                {
                    ComLifetime.Release(books);
                    var remaining = ComLifetime.Release(app);
                    Console.Error.WriteLine($"{DateTime.UtcNow:O} fixture PID={ownedProcess}: root RCW released, remaining={remaining}");
                }
            }
        }
    }

    internal static async Task WaitForExitAsync(int pid, DateTime? quitUtc = null)
    {
        try
        {
            var process = ownedProcesses.TryGetValue(pid, out var tracked) ? tracked : Process.GetProcessById(pid);
            try
            {
                if (quitTimes.Remove(pid, out var fixtureQuit)) quitUtc = fixtureQuit;
                if (LifecycleWorkflowDiagnostics.Current is { } diagnostics)
                {
                    await diagnostics.ObserveAsync(pid, process);
                    return;
                }
                string identity;
                try { identity = $"PID={pid}, started UTC={process.StartTime.ToUniversalTime():O}"; }
                catch (InvalidOperationException) when (process.HasExited)
                {
                    Console.Error.WriteLine($"Test-owned Excel PID={pid} exited before identity capture.");
                    return;
                }
                var trial = new Trial
                {
                    Environment = "exit30",
                    Scenario = fixtureQuit == default ? "host" : "fixture",
                    Repeat = fixtureQuit == default ? MeasurementCycle : 0,
                    Pid = pid,
                    StartUtc = process.StartTime.ToUniversalTime(),
                    QuitReturnedUtc = quitUtc ?? throw new InvalidOperationException("Quit timestamp unavailable."),
                    QuitStatus = "Quit returned with zero workbooks",
                    StaTerminated = true
                };
                var output = Environment.GetEnvironmentVariable("TSEKSHEET_LIFECYCLE_OUTPUT") ?? System.IO.Path.Combine(AppContext.BaseDirectory, "TestResults", "exit30");
                await ExitObservation.ObserveAsync(trial, process, output, null, acceptanceSeconds: 30, observationSeconds: 90);
                Console.Error.WriteLine($"Test-owned Excel: {identity}; exit UTC={trial.ExitUtc:O}; seconds={trial.ExitSeconds}; thirty-second={trial.PassedThirtySeconds}.");
                if (!trial.PassedThirtySeconds) throw new ExcelExitDeadlineException(trial);
            }
            finally
            {
                if (ExitObservation.WaitForExit(process, 0)) { ownedProcesses.Remove(pid); process.Dispose(); }
            }
        }
        catch (ArgumentException) { Console.Error.WriteLine($"Test-owned Excel PID={pid} already exited before the exit check."); }
    }


    private static void AddSectionSheets(Interop.Application app, Interop.Workbook book)
    {
        Interop.Sheets? sheets = null;
        try
        {
            sheets = book.Worksheets;
            foreach (var name in new[] { "Columns", "Stacked", "Table" })
            {
                Interop.Worksheet? sheet = null;
                try
                {
                    sheet = (Interop.Worksheet)(object)sheets.Add(); sheet.Name = name;
                    var screen = app.ScreenUpdating; var events = app.EnableEvents; var calculation = app.Calculation;
                    try
                    {
                        app.ScreenUpdating = false; app.EnableEvents = false; app.Calculation = Interop.XlCalculation.xlCalculationManual;
                        var data = new object?[8, 12];
                        data[0, 0] = "Synthetic receipt destination";
                        string[] headers = ["Vendor name", "Particulars", "RFP number", "Invoice number", "Amount", "Record date", "Derived"];
                        for (var c = 0; c < headers.Length; c++) data[1, c] = headers[c];
                        for (var r = 2; r <= 3; r++) { data[r, 0] = "Synthetic vendor"; data[r, 1] = "Template"; data[r, 2] = "RFP-" + r; data[r, 4] = r * 10d; }
                        if (name != "Table") data[4, 0] = "Subtotal";
                        if (name == "Stacked") { data[6, 0] = "Other section"; data[7, 0] = "Neighbour"; data[7, 4] = 77d; }
                        else { data[1, 9] = "Neighbour"; data[2, 9] = "Keep"; data[2, 10] = 77d; data[3, 9] = "Other total"; }
                        Put(sheet, new(1, 1, 8, 12), range => range.Value2 = data);
                        Put(sheet, new(3, 7, 2, 1), range => range.Formula2 = new object[,] { { "=E3*2" }, { "=E4*2" } });
                        Put(sheet, new(3, 5, 2, 1), range => range.NumberFormat = "0.00");
                        Put(sheet, new(3, 6, 2, 1), range => range.NumberFormat = "yyyy-mm-dd");
                        if (name != "Stacked") Put(sheet, new(4, 11, 1, 1), range => range.Formula2 = new object[,] { { "=SUM(K3:K3)" } });
                        if (name == "Table")
                        {
                            Interop.ListObjects? tables = null; Interop.Range? source = null; Interop.ListObject? table = null;
                            try
                            {
                                tables = sheet.ListObjects; source = sheet.Range["A2:G4"];
                                table = tables.Add(Interop.XlListObjectSourceType.xlSrcRange, source, XlListObjectHasHeaders: Interop.XlYesNoGuess.xlYes);
                                table.Name = "Records"; table.ShowTotals = true;
                                Put(sheet, new(5, 1, 1, 1), range => range.Value2 = new object[,] { { "Subtotal" } });
                            }
                            finally { ComLifetime.Release(table); ComLifetime.Release(source); ComLifetime.Release(tables); }
                        }
                        Put(sheet, new(5, 5, 1, 1), range => range.Formula2 = new object[,] { { "=SUM(E3:E4)" } });
                        Put(sheet, new(5, 3, 1, 1), range => range.Formula2 = new object[,] { { "=SUM(E3:E4)+SUM(E3:E3)+E4" } });
                        Put(sheet, new(5, 7, 1, 1), range => range.Formula2 = new object[,] { { "=SUM(G3:G4)" } });
                    }
                    finally
                    {
                        try { app.Calculation = calculation; }
                        finally { try { app.EnableEvents = events; } finally { app.ScreenUpdating = screen; } }
                    }
                }
                finally { ComLifetime.Release(sheet); }
            }
        }
        finally { ComLifetime.Release(sheets); }
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

internal sealed class ExcelExitDeadlineException(Trial evidence) : InvalidOperationException(
    $"Owned Excel PID={evidence.Pid} missed thirty seconds after Quit; exit seconds={evidence.ExitSeconds?.ToString("F3") ?? ">90"}.")
{
    internal Trial Evidence { get; } = evidence;
    internal ExcelFixture? Fixture { get; set; }
}
