using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using ExcelLifecycle.Diagnostics;
using TsekSheet.Application.Excel;
using TsekSheet.Excel;
using Office = Microsoft.Office.Core;
using Interop = Microsoft.Office.Interop.Excel;

namespace ExcelActivation.Experiment;

// Only accessed on its creating, pumped STA. No COM object crosses the command boundary.
internal sealed class OwnedSession(string executable, string root, Result result)
{
    private Interop.Application? app;
    private Interop.Workbooks? books;
    private readonly List<(Interop.Workbook Book, string Path, nint Identity)> opened = [];
    private int ownerThread;
    private int session;
    internal Process? Process { get; private set; }

    internal void Start(string mode, string bootstrap)
    {
        ownerThread = Environment.CurrentManagedThreadId;
        result.Trial.ManagedThread = ownerThread;
        result.Trial.Apartment = Thread.CurrentThread.GetApartmentState().ToString();
        if (result.Trial.Apartment != "STA") throw new InvalidOperationException("STA required.");
        result.Baseline = Program.Census();
        var began = DateTime.UtcNow;
        if (mode == "x")
        {
            Program.Admit(bootstrap, root);
            var start = new ProcessStartInfo(executable) { UseShellExecute = false };
            start.ArgumentList.Add("/x");
            start.ArgumentList.Add(bootstrap);
            Process = System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("Process.Start returned null.");
            _ = Process.SafeHandle;
            result.LaunchPid = Process.Id;
            result.LaunchStartUtc = Process.StartTime.ToUniversalTime();
            session = Process.SessionId;
            CheckProcess(began);
            app = Acquire(began.AddSeconds(30));
        }
        else
        {
            app = new Interop.Application();
            Native.GetWindowThreadProcessId((nint)app.Hwnd, out var pid);
            Process = System.Diagnostics.Process.GetProcessById(checked((int)pid));
            _ = Process.SafeHandle;
            result.LaunchPid = Process.Id;
            result.LaunchStartUtc = Process.StartTime.ToUniversalTime();
            session = Process.SessionId;
            CheckProcess(began);
        }
        result.Trial.Pid = Process.Id;
        result.Trial.StartUtc = result.LaunchStartUtc;
        result.Trial.FileVersion = FileVersionInfo.GetVersionInfo(executable).FileVersion ?? "";
        result.Trial.NativeThread = Native.GetWindowThreadProcessId((nint)app.Hwnd, out _);
        Check("acquired");
        result.Acquired = true;
        result.Trial.Version = app.Version;
        result.Trial.Build = Convert.ToString(app.Build) ?? "";
        app.Visible = true;
        app.UserControl = true;
        app.AutomationSecurity = Office.MsoAutomationSecurity.msoAutomationSecurityForceDisable;
        result.Trial.Windows.Add(ExitObservation.WindowState(Process, "visible acquisition"));
        books = app.Workbooks;
        if (mode == "x")
        {
            if (books.Count != 1) throw new InvalidOperationException("Unexpected startup workbook; no workbook will be closed.");
            Interop.Workbook? bootstrapBook = null;
            try
            {
                bootstrapBook = books[1];
                if (!Same(bootstrapBook.FullName, bootstrap)) throw new InvalidOperationException("Unexpected startup path.");
                Track(bootstrapBook, bootstrap);
                bootstrapBook = null; // Ownership transferred to opened.
                CloseLast();
            }
            finally { Release(bootstrapBook, "unclaimed bootstrap"); }
        }
        if (books.Count != 0) throw new InvalidOperationException("Unexpected workbook; Quit will be withheld.");
        result.Trial.Events.Add($"{DateTime.UtcNow:O} ready; no workbook; bootstrap={(mode == "x" ? "admitted synthetic file closed" : "none")}");
    }

    private Interop.Application Acquire(DateTime deadline)
    {
        while (DateTime.UtcNow < deadline)
        {
            CheckProcess();
            var panes = Native.Panes(Process!.Id);
            // Initial bootstrap has exactly one root/document pane. Ambiguity is a failure, not a selection heuristic.
            if (panes.Count > 1) throw new InvalidOperationException("Ambiguous native object-model panes.");
            if (panes.Count == 1)
            {
                var pane = panes[0];
                CheckWindow(pane, "EXCEL7");
                var iid = new Guid("00020400-0000-0000-C000-000000000046");
                object? native = null;
                Interop.Application? candidate = null;
                try
                {
                    var hr = Native.AccessibleObjectFromWindow(pane, 0xFFFFFFF0, ref iid, out native);
                    if (hr == 0 && native is Interop.Window window)
                    {
                        candidate = window.Application;
                        CheckWindow(pane, "EXCEL7");
                        CheckWindow((nint)candidate.Hwnd, "XLMAIN");
                        if (Native.GetAncestor(pane, 2) != (nint)candidate.Hwnd)
                            throw new InvalidOperationException("Native pane and application root disagree.");
                        result.AcquisitionPane = $"0x{pane:X}";
                        result.ApplicationWindow = $"0x{candidate.Hwnd:X}";
                        var acquired = candidate;
                        candidate = null;
                        return acquired;
                    }
                    result.Trial.Events.Add($"{DateTime.UtcNow:O} native acquisition HRESULT=0x{hr:X8}");
                }
                finally { Release(candidate, "rejected application"); Release(native, "native Window"); }
            }
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(25);
        }
        throw new TimeoutException("Exact-process native object model unavailable after 30 seconds; no active-object fallback.");
    }

    private void CheckProcess(DateTime? earliest = null)
    {
        if (Process is null || Process.WaitForExit(0)) throw new InvalidOperationException("Owned process missing.");
        Process.Refresh();
        var actualStart = Process.StartTime.ToUniversalTime();
        var actualSession = Process.SessionId;
        var controllerSession = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        var actualExecutable = Process.MainModule!.FileName;
        if (Process.Id != result.LaunchPid || actualStart != result.LaunchStartUtc ||
            actualSession != session || session != controllerSession ||
            !Same(actualExecutable, executable) || result.Baseline.Any(x => x.Pid == Process.Id) ||
            (earliest is not null && result.LaunchStartUtc < earliest.Value.AddSeconds(-1)))
        {
            result.IdentityMismatches++;
            throw new InvalidOperationException($"Process identity mismatch: PID={Process.Id}/{result.LaunchPid}; start={actualStart:O}/{result.LaunchStartUtc:O}; session={actualSession}/{session}/{controllerSession}; executable={actualExecutable}/{executable}; baseline={result.Baseline.Any(x => x.Pid == Process.Id)}; earliest={earliest:O}.");
        }
    }

    // Cleanup-only revalidation of a retained, recorded launch. Never used by acquisition or the matrix as a fallback.
    internal void RevalidateForCleanup(string bootstrap)
    {
        ownerThread = Environment.CurrentManagedThreadId;
        Process = System.Diagnostics.Process.GetProcessById(result.LaunchPid);
        _ = Process.SafeHandle;
        session = System.Diagnostics.Process.GetCurrentProcess().SessionId;
        CheckProcess();
        Program.Admit(bootstrap, root);
        app = Acquire(DateTime.UtcNow.AddSeconds(10));
        Check("cleanup-only revalidation");
        books = app.Workbooks;
        if (books.Count != 1) throw new InvalidOperationException("Cleanup rejected: unexpected workbook count.");
        Interop.Workbook? book = null;
        try
        {
            book = books[1];
            if (!Same(book.FullName, bootstrap) || !book.Saved) throw new InvalidOperationException("Cleanup rejected: path changed or unsaved changes.");
            Track(book, bootstrap); book = null;
            result.Acquired = true;
            result.Trial.Pid = result.LaunchPid; result.Trial.StartUtc = result.LaunchStartUtc;
        }
        finally { Release(book, "unclaimed cleanup workbook"); }
    }

    private void CheckWindow(nint hwnd, string expectedClass)
    {
        CheckProcess();
        Native.GetWindowThreadProcessId(hwnd, out var pid);
        if (hwnd == 0 || pid != result.LaunchPid || Native.Class(hwnd) != expectedClass)
        {
            result.IdentityMismatches++;
            throw new InvalidOperationException("Window identity mismatch; operation rejected.");
        }
    }

    private void Check(string operation)
    {
        if (ownerThread != Environment.CurrentManagedThreadId) throw new InvalidOperationException("COM thread changed.");
        CheckWindow((nint)app!.Hwnd, "XLMAIN");
        result.ApplicationWindow = $"0x{app.Hwnd:X}";
        var windows = Native.Windows(result.LaunchPid);
        result.WindowInventory.Add($"{DateTime.UtcNow:O} {operation}: {string.Join(" | ", windows)}");
        if (windows.Any(x => x.Contains("class=#32770", StringComparison.Ordinal)))
        {
            result.UnexpectedPrompts++;
            throw new InvalidOperationException("Owned modal dialog detected; no GUI cleanup performed.");
        }
        result.OwnershipChecks.Add($"{DateTime.UtcNow:O} {operation}: PID={result.LaunchPid}; start={result.LaunchStartUtc:O}; session={session}; HWND=0x{app.Hwnd:X}; native={result.AcquisitionPane}; thread={ownerThread}");
        if (books is null) return;
        if (books.Count != opened.Count) throw new InvalidOperationException("Unexpected workbook count; destructive operation rejected.");
        for (var i = 1; i <= books.Count; i++)
        {
            Interop.Workbook? book = null;
            nint identity = 0;
            try
            {
                book = books[i];
                identity = Marshal.GetIUnknownForObject(book);
                if (!opened.Any(x => x.Identity == identity && Same(book.FullName, x.Path)))
                    throw new InvalidOperationException("Unregistered workbook; destructive operation rejected.");
            }
            finally { if (identity != 0) Marshal.Release(identity); Release(book, "ownership inventory item"); }
        }
    }

    private void Track(Interop.Workbook book, string path) => opened.Add((book, path, Marshal.GetIUnknownForObject(book)));

    internal void Open(string path)
    {
        Program.Admit(path, root);
        Check("before Open");
        app!.AutomationSecurity = Office.MsoAutomationSecurity.msoAutomationSecurityForceDisable;
        Interop.Workbook? book = null;
        try
        {
            book = books!.Open(path, UpdateLinks: 0, ReadOnly: false, IgnoreReadOnlyRecommended: true, AddToMru: false);
            if (!Same(book.FullName, path) || book.ReadOnly || book.ProtectStructure || book.ProtectWindows)
                throw new InvalidOperationException("Workbook admission/identity changed after Open.");
            Track(book, path);
            book = null;
            Check("after Open");
        }
        finally { Release(book, "unclaimed opened book"); }
    }

    internal string Snapshot() { Check("snapshot"); return SnapshotIdentity.Hash(ExcelReadback.Capture(opened[^1].Book)); }

    internal void Write(bool formula, int iteration)
    {
        Check("before write");
        var (book, path, _) = opened[^1];
        var backup = path + $".{DateTime.UtcNow:yyyyMMddHHmmssfffffff}.backup.xlsx";
        File.Copy(path, backup, false);
        if (Program.Hash(path) != Program.Hash(backup)) throw new IOException("Backup hash mismatch.");
        result.Backups.Add(backup);
        var before = ExcelReadback.Capture(book);
        Interop.Sheets? sheets = null;
        Interop.Worksheet? sheet = null;
        Interop.Range? values = null, calculated = null;
        var screen = app!.ScreenUpdating;
        var events = app.EnableEvents;
        var calculation = app.Calculation;
        try
        {
            app.ScreenUpdating = false; app.EnableEvents = false; app.Calculation = Interop.XlCalculation.xlCalculationManual;
            sheets = book.Worksheets;
            sheet = (Interop.Worksheet)(object)sheets[1];
            if (sheet.ProtectContents) throw new InvalidOperationException("Protected worksheet.");
            values = sheet.Range["A1:B1"];
            var data = ExcelReadback.Read(sheet, new(1, 1, 1, 2));
            if (data.Formulas.Cast<string?>().Any(x => x is not null) || Convert.ToBoolean(values.MergeCells) || Convert.ToBoolean(values.HasArray))
                throw new InvalidOperationException("Unsafe value target.");
            Check("immediately before Value2 assignment");
            values.Value2 = new object[,] { { 9d + iteration, 13d + iteration } };
            var afterValues = ExcelReadback.Capture(book);
            var errors = PreservationCheck.Compare(before, afterValues, sheet.Name, new(1, 1, 1, 2), new object?[,] { { 9d + iteration, 13d + iteration } });
            if (errors.Count != 0) throw new InvalidOperationException(string.Join(";", errors));
            if (formula)
            {
                calculated = sheet.Range["C1"];
                if (Convert.ToBoolean(calculated.MergeCells) || Convert.ToBoolean(calculated.HasArray)) throw new InvalidOperationException("Unsafe formula target.");
                // C1 is the declared synthetic formula column; never an OCR/manual value target.
                Check("immediately before Formula2 assignment");
                calculated.Formula2 = new object[,] { { "=SUM(A1:B1)" } };
                Check("before Calculate");
                app.Calculate();
                if (!Equals((object)calculated.Value2, 22d + 2 * iteration) || (string)(object)calculated.Formula2 != "=SUM(A1:B1)")
                    throw new InvalidOperationException("Formula/value readback incorrect.");
                var afterFormula = ExcelReadback.Capture(book);
                // Preserve all metadata and non-target formulas/values. The formula itself was checked above.
                var normalized = afterFormula with { Sheets = afterFormula.Sheets.Select(s =>
                {
                    if (s.Name != sheet.Name) return s;
                    var formulas = (string?[,])s.Data.Formulas.Clone();
                    formulas[1 - s.Data.Range.Row, 3 - s.Data.Range.Column] = null;
                    return s with { Data = s.Data with { Formulas = formulas } };
                }).ToArray() };
                errors = PreservationCheck.Compare(afterValues, normalized, sheet.Name, new(1, 3, 1, 1), new object?[,] { { 22d + 2 * iteration } });
                if (errors.Count != 0) throw new InvalidOperationException(string.Join(";", errors));
            }
            result.CorrectnessChecks++;
        }
        finally
        {
            Release(calculated, "formula range"); Release(values, "value range"); Release(sheet, "worksheet"); Release(sheets, "worksheets");
            try { app.Calculation = calculation; }
            finally { try { app.EnableEvents = events; } finally { app.ScreenUpdating = screen; } }
        }
    }

    internal void CloseLast()
    {
        Check("before Close(false)");
        var item = opened[^1];
        item.Book.Close(SaveChanges: false);
        opened.RemoveAt(opened.Count - 1);
        Marshal.Release(item.Identity);
        Release(item.Book, "workbook");
    }

    internal void Cleanup()
    {
        try
        {
            if (!result.Acquired) { result.Trial.QuitStatus = "withheld: acquisition unverified"; return; }
            while (opened.Count > 0) CloseLast();
            Check("before Quit (zero registered and open workbooks)");
            result.Trial.Windows.Add(ExitObservation.WindowState(Process!, "before Quit"));
            app!.Quit();
            result.Trial.QuitReturnedUtc = DateTime.UtcNow;
            result.Trial.QuitStatus = "returned successfully with zero open workbooks";
        }
        catch (Exception error) { result.Trial.Error ??= error.ToString(); result.Trial.QuitStatus = "failed or withheld: " + error.Message; }
        finally
        {
            // Release even if ownership changed. Never Close/Quit an unverified instance.
            for (var i = opened.Count - 1; i >= 0; i--) { Marshal.Release(opened[i].Identity); Release(opened[i].Book, "unclosed workbook"); }
            opened.Clear(); Release(books, "workbooks"); books = null; Release(app, "application"); app = null;
            result.Trial.Events.Add($"{DateTime.UtcNow:O} owned references released on STA={ownerThread}");
        }
    }

    private void Release(object? value, string label)
    {
        if (value is not null && Marshal.IsComObject(value))
            result.Trial.Releases.Add($"{label}; remaining={Marshal.ReleaseComObject(value)}; thread={Environment.CurrentManagedThreadId}");
    }
    private static bool Same(string first, string second) => string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
}

internal static class Native
{
    private delegate bool Callback(nint hwnd, nint parameter);
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] internal static extern nint GetAncestor(nint hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumWindows(Callback callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumChildWindows(nint hwnd, Callback callback, nint parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetClassName(nint hwnd, StringBuilder text, int capacity);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint hwnd, StringBuilder text, int capacity);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint hwnd);
    [DllImport("oleacc.dll")] internal static extern int AccessibleObjectFromWindow(nint hwnd, uint objectId, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object? value);
    internal static string Class(nint hwnd) { var name = new StringBuilder(256); GetClassName(hwnd, name, name.Capacity); return name.ToString(); }
    internal static List<string> Windows(int pid)
    {
        var result = new List<string>();
        EnumWindows((hwnd, _) =>
        {
            GetWindowThreadProcessId(hwnd, out var owner);
            if (owner == pid)
            {
                var text = new StringBuilder(512); GetWindowText(hwnd, text, text.Capacity);
                result.Add($"HWND=0x{hwnd:X}; class={Class(hwnd)}; visible={IsWindowVisible(hwnd)}; title={text}");
            }
            return true;
        }, 0);
        return result;
    }
    internal static List<nint> Panes(int pid)
    {
        var panes = new List<nint>();
        EnumWindows((root, _) =>
        {
            GetWindowThreadProcessId(root, out var rootPid);
            if (rootPid == pid && Class(root) == "XLMAIN") EnumChildWindows(root, (child, _) =>
            {
                GetWindowThreadProcessId(child, out var childPid);
                if (childPid == pid && Class(child) == "EXCEL7") panes.Add(child);
                return true;
            }, 0);
            return true;
        }, 0);
        return panes;
    }
}
