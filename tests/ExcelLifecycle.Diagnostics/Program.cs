using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Forms = System.Windows.Forms;
using Office = Microsoft.Office.Core;
using Interop = Microsoft.Office.Interop.Excel;

namespace ExcelLifecycle.Diagnostics;

internal static class Program
{
    // One independent process per trial. No product assemblies, ROT lookup, or test framework.
    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        throw new InvalidOperationException("Run this diagnostic in Release.");
#endif
        if (args.Length is < 5 or > 6 || !new[] { "com", "shell", "safe" }.Contains(args[0]) ||
            !new[] { "author", "activate", "open", "value", "formula" }.Contains(args[1]) ||
            !int.TryParse(args[2], out var repeat) || repeat is < 0 or > 3)
            throw new ArgumentException("environment(com|shell|safe) scenario(author|activate|open|value|formula) repeat(0..3) synthetic-path output-dir [stack-tool]");
        var path = Path.GetFullPath(args[3]);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "GOALS.md"))) repository = repository.Parent;
        var fixtureRoot = Path.Combine(repository!.FullName, "fixtures", "excel", "t1-");
        if (!path.StartsWith(fixtureRoot, StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path) != ".xlsx")
            throw new ArgumentException("Only synthetic .xlsx files in fixtures/excel/t1-* are supported.");
        if (args[1] == "author" && File.Exists(path)) throw new ArgumentException("Authoring never overwrites a file.");
        var trial = new Trial { Environment = args[0], Scenario = args[1], Repeat = repeat };
        var thread = new Thread(() => Pump(() => Execute(trial, path))) { IsBackground = true, Name = "Independent Excel diagnostic STA" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        // Native COM calls are additionally supervised by the external fixed-run script.
        await Task.Run(thread.Join);
        trial.StaTerminated = !thread.IsAlive;
        if (trial.Pid == 0) throw new InvalidOperationException(trial.Error ?? "No owned Excel process was established.");
        try
        {
            using var process = Process.GetProcessById(trial.Pid);
            if (process.StartTime.ToUniversalTime() != trial.StartUtc) throw new InvalidOperationException("PID was reused; no inspection performed.");
            await ExitObservation.ObserveAsync(trial, process, args[4], args.Length >= 6 ? args[5] : null);
        }
        catch (ArgumentException)
        {
            // Usually observation starts before exit. Retain explicit missing-process evidence.
            trial.Events.Add("Process already gone before observer opened a handle; exact exit time unavailable.");
            trial.Error ??= "Exact exit time unavailable.";
            Directory.CreateDirectory(args[4]);
            await File.WriteAllTextAsync(Path.Combine(args[4], $"{trial.Environment}-{trial.Scenario}-{repeat}-{trial.Pid}.json"), System.Text.Json.JsonSerializer.Serialize(trial));
        }
        return trial.PassedTenSeconds ? 0 : 1;
    }

    private static void Pump(Action action)
    {
        using var filter = new RejectionFilter();
        using var dispatcher = new Forms.Control();
        using var context = new Forms.ApplicationContext();
        _ = dispatcher.Handle;
        dispatcher.BeginInvoke((Action)(() => { try { action(); } finally { Collect(); context.ExitThread(); } }));
        Forms.Application.Run(context);
    }

    private static void Execute(Trial trial, string path)
    {
        var previous = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName("EXCEL")) using (process) previous.Add(process.Id);
        Interop.Application? app = null;
        Interop.Workbooks? books = null;
        Interop.Workbook? book = null;
        Process? owned = null;
        var isolated = false;
        try
        {
            trial.ManagedThread = Environment.CurrentManagedThreadId;
            trial.Apartment = Thread.CurrentThread.GetApartmentState().ToString();
            trial.Events.Add($"{DateTime.UtcNow:O} COM activation begins on pumped STA");
            if (trial.Environment == "com") app = new Interop.Application();
            else app = Launch(trial, path, previous);
            trial.NativeThread = GetWindowThreadProcessId((nint)app.Hwnd, out var pid);
            if (pid == 0 || previous.Contains((int)pid)) throw new InvalidOperationException("Excel is not an independent owned process.");
            isolated = true;
            trial.Pid = (int)pid;
            owned = Process.GetProcessById((int)pid);
            trial.StartUtc = owned.StartTime.ToUniversalTime();
            trial.FileVersion = FileVersionInfo.GetVersionInfo(owned.MainModule!.FileName).FileVersion ?? "";
            trial.Version = app.Version;
            trial.Build = Convert.ToString(app.Build, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            app.Visible = true;
            app.UserControl = true;
            app.AutomationSecurity = Office.MsoAutomationSecurity.msoAutomationSecurityForceDisable;
            trial.Windows.Add(ExitObservation.WindowState(owned, "activated"));
            trial.Events.Add($"{DateTime.UtcNow:O} Visible={app.Visible} UserControl={app.UserControl} startup={app.StartupPath} alternate={app.AltStartupPath}");
            books = app.Workbooks;
            if (trial.Environment != "com")
            {
                if (books.Count != 1) throw new InvalidOperationException("Unexpected startup workbook; left open.");
                book = books[1];
                if (!book.FullName.Equals(path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected startup workbook; left open.");
            }
            if (trial.Scenario == "author") ReadAddins(app, trial);
            else trial.Addins.Add("Not enumerated during minimal scenario; see separate inventory evidence to avoid observer-induced COM references.");
            if (trial.Scenario == "author")
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                book = books.Add(Interop.XlWBATemplate.xlWBATWorksheet);
                Edit(app, book, "value", trial);
                book.SaveAs(path, Interop.XlFileFormat.xlOpenXMLWorkbook, AddToMru: false);
            }
            else if (trial.Environment != "com")
            {
                if (trial.Scenario is "value" or "formula") BackupSynthetic(path, trial);
                Edit(app, book!, trial.Scenario, trial);
            }
            else if (trial.Scenario != "activate")
            {
                book = books.Open(path, UpdateLinks: 0, ReadOnly: false, IgnoreReadOnlyRecommended: true, AddToMru: false);
                if (trial.Scenario is "value" or "formula") BackupSynthetic(path, trial);
                Edit(app, book, trial.Scenario, trial);
            }
        }
        catch (Exception error) { trial.Error = error.ToString(); }
        finally
        {
            try
            {
                if (book is not null && isolated && book.FullName.Equals(path, StringComparison.OrdinalIgnoreCase))
                {
                    book.Close(SaveChanges: false);
                    trial.Events.Add($"{DateTime.UtcNow:O} synthetic workbook Close(false) returned");
                }
                Release(book, "workbook", trial);
                book = null;
                if (app is not null && isolated)
                {
                    books ??= app.Workbooks;
                    trial.Events.Add($"{DateTime.UtcNow:O} open workbooks before Quit={books.Count}");
                    if (books.Count == 0)
                    {
                        if (owned is not null) trial.Windows.Add(ExitObservation.WindowState(owned, "before Quit"));
                        app.Quit();
                        trial.QuitReturnedUtc = DateTime.UtcNow;
                        trial.QuitStatus = "returned successfully with zero open workbooks";
                    }
                    else trial.Error ??= "Quit withheld: unexpected open workbook was left untouched.";
                }
            }
            catch (Exception error) { trial.Error = error.ToString(); trial.QuitStatus = "cleanup threw"; }
            finally
            {
                Release(book, "workbook after failure", trial);
                Release(books, "workbooks", trial);
                Release(app, "application", trial);
                if (owned is not null) { trial.Windows.Add(ExitObservation.WindowState(owned, "after release")); owned.Dispose(); }
                trial.Events.Add($"{DateTime.UtcNow:O} owned RCW releases complete; STA command returning");
            }
        }
    }

    private static void Edit(Interop.Application app, Interop.Workbook book, string scenario, Trial trial)
    {
        if (scenario is "open" or "activate") return;
        Interop.Sheets? sheets = null;
        Interop.Worksheet? sheet = null;
        Interop.Range? values = null, formula = null;
        var screen = app.ScreenUpdating;
        var events = app.EnableEvents;
        var calculation = app.Calculation;
        try
        {
            app.ScreenUpdating = false; app.EnableEvents = false; app.Calculation = Interop.XlCalculation.xlCalculationManual;
            sheets = book.Worksheets;
            sheet = (Interop.Worksheet)(object)sheets[1];
            values = sheet.Range["A1:B1"];
            values.Value2 = new object[,] { { 7d, 11d } };
            if (scenario == "formula")
            {
                formula = sheet.Range["C1:C1"];
                formula.Formula2 = new object[,] { { "=SUM(A1:B1)" } };
                app.Calculate();
                if (!Equals((object)formula.Value2, 18d)) throw new InvalidOperationException("Formula readback was not 18.");
            }
            trial.Events.Add($"{DateTime.UtcNow:O} {scenario} operation complete; calculation={app.CalculationState}");
        }
        finally
        {
            Release(formula, "formula range", trial); Release(values, "values range", trial);
            Release(sheet, "worksheet", trial); Release(sheets, "worksheets", trial);
            try { app.Calculation = calculation; }
            finally { try { app.EnableEvents = events; } finally { app.ScreenUpdating = screen; } }
        }
    }

    private static void BackupSynthetic(string path, Trial trial)
    {
        var backup = Path.Combine(Path.GetDirectoryName(path)!, $"{trial.Environment}-{trial.Scenario}-{trial.Repeat}-{trial.Pid}.backup.xlsx");
        File.Copy(path, backup, overwrite: false);
        static string Hash(string file)
        {
            using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        }
        if (Hash(path) != Hash(backup)) throw new IOException("Synthetic backup hash mismatch.");
        trial.Events.Add($"{DateTime.UtcNow:O} synthetic backup hash verified before write");
    }

    private static void ReadAddins(Interop.Application app, Trial trial)
    {
        Office.COMAddIns? com = null;
        Interop.AddIns? excel = null;
        try
        {
            try { com = app.COMAddIns; }
            catch (COMException error) when (trial.Environment == "safe" && error.HResult == unchecked((int)0x800A03EC))
            { trial.Addins.Add("COMAddIns unavailable in Safe Mode (0x800A03EC)"); }
            for (var i = 1; com is not null && i <= com.Count; i++)
            {
                Office.COMAddIn? item = null;
                try { item = com.Item(i); trial.Addins.Add($"COM {item.ProgId}: connected={item.Connect}"); }
                finally { Release(item, "COM add-in", trial); }
            }
            try { excel = app.AddIns; }
            catch (COMException error) when (trial.Environment == "safe" && error.HResult == unchecked((int)0x800A03EC))
            { trial.Addins.Add("AddIns unavailable in Safe Mode (0x800A03EC)"); }
            for (var i = 1; excel is not null && i <= excel.Count; i++)
            {
                Interop.AddIn? item = null;
                try { item = excel[i]; trial.Addins.Add($"Excel {item.Name}: installed={item.Installed}"); }
                finally { Release(item, "Excel add-in", trial); }
            }
        }
        finally { Release(excel, "Excel add-ins", trial); Release(com, "COM add-ins", trial); }
    }

    private static Interop.Application Launch(Trial trial, string path, HashSet<int> previous)
    {
        var start = new ProcessStartInfo(@"C:\Program Files\Microsoft Office\root\Office16\EXCEL.EXE") { UseShellExecute = false, CreateNoWindow = true };
        start.ArgumentList.Add("/x");
        if (trial.Environment == "safe") start.ArgumentList.Add("/safe");
        start.ArgumentList.Add(path); // Only our pre-authored, macro-free synthetic workbook.
        using var process = Process.Start(start)!;
        if (previous.Contains(process.Id)) throw new InvalidOperationException("Launched process is not new.");
        trial.Pid = process.Id;
        trial.StartUtc = process.StartTime.ToUniversalTime();
        return ConnectOwned(process, trial);
    }

    private static Interop.Application ConnectOwned(Process process, Trial trial)
    {
        var deadline = Environment.TickCount64 + 30_000;
        while (Environment.TickCount64 < deadline && !process.HasExited)
        {
            nint target = 0;
            EnumWindows((window, _) =>
            {
                GetWindowThreadProcessId(window, out var pid);
                if (pid != process.Id) return true;
                EnumChildWindows(window, (child, _) => { var name = new StringBuilder(256); GetClassName(child, name, name.Capacity); if (name.ToString() == "EXCEL7") target = child; return true; }, 0);
                return target == 0;
            }, 0);
            if (target != 0)
            {
                var iid = new Guid("00020400-0000-0000-C000-000000000046");
                object? native = null;
                try
                {
                    var result = AccessibleObjectFromWindow(target, 0xFFFFFFF0, ref iid, out native);
                    if (result >= 0 && native is Interop.Window window) return window.Application;
                }
                finally { Release(native, "own-process native Window", trial); }
            }
            Forms.Application.DoEvents(); Thread.Sleep(25);
        }
        throw new InvalidOperationException("Owned Excel native object model was unavailable within 30 seconds; no unrelated instance was attached.");
    }

    private static void Release(object? value, string name, Trial trial)
    {
        if (value is not null && Marshal.IsComObject(value)) trial.Releases.Add($"{DateTime.UtcNow:O} {name}: RCW remaining={Marshal.ReleaseComObject(value)}, thread={Environment.CurrentManagedThreadId}");
    }
    private static void Collect() { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); GC.WaitForPendingFinalizers(); }
    private delegate bool WindowCallback(nint window, nint parameter);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint process);
    [DllImport("user32.dll")] private static extern bool EnumWindows(WindowCallback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint parent, WindowCallback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint window, StringBuilder name, int capacity);
    [DllImport("oleacc.dll")] private static extern int AccessibleObjectFromWindow(nint window, uint objectId, ref Guid interfaceId, [MarshalAs(UnmanagedType.Interface)] out object? result);
}
