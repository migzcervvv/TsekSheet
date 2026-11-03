using System.Diagnostics;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel;

public sealed partial class ExcelHost : IExcelHost, IWorkbookReader, IWorkbookWriter, ISectionWriter
{
    private readonly StaCommandQueue queue = new();
    private readonly WorkbookBackupService backups;
    private readonly Dictionary<string, Book> books = [];
    private Interop.Application? application;
    private ExcelHostInfo? info;
    private Task? disposal;
    private readonly object disposalGate = new();

    private sealed class Book(string id, string path, nint identity, bool opened) : IDisposable
    {
        public string Id { get; } = id;
        public string Path { get; } = path;
        // Retain one IUnknown reference for the registration lifetime. Otherwise Excel's
        // out-of-process proxy identity can be recycled between command RCW releases.
        public nint Identity { get; } = identity;
        public bool Opened { get; set; } = opened;
        public bool Faulted { get; set; }
        public BackupReceipt? Backup { get; set; }
        public void Dispose() => Marshal.Release(Identity);
    }

    public ExcelHost(string? backupFolder = null) => backups = new(backupFolder);

    public Task<ExcelHostInfo> StartAsync() => queue.InvokeAsync(StartCore);
    public Task<IReadOnlyList<ExcelWorkbook>> ListWorkbooksAsync() => queue.InvokeAsync(ListCore);
    public Task<ExcelWorkbook> OpenAsync(string path)
    {
        if (!Path.IsPathFullyQualified(path) || path.StartsWith(@"\\?\", StringComparison.Ordinal) ||
            !new[] { ".xlsx", ".xlsm", ".xlsb", ".xls" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException("Select an absolute local or network Excel workbook path.", nameof(path));
        var fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Workbook not found.");
        return queue.InvokeAsync(() => OpenCore(fullPath));
    }

    public Task ActivateAsync(string workbookId, string sheet, CellRange range)
    {
        range.Validate();
        return queue.InvokeAsync(() => WithSheet(workbookId, sheet, (_, worksheet) =>
        {
            Interop.Range? target = null;
            try
            {
                worksheet.Activate();
                target = ExcelReadback.Range(worksheet, range);
                target.Select();
                return true;
            }
            finally { ComLifetime.Release(target); }
        }));
    }

    public Task CloseAsync(string workbookId, bool discardChanges = false) => queue.InvokeAsync(() =>
        WithBook(workbookId, workbook =>
        {
            if (!books[workbookId].Opened) throw new InvalidOperationException("This workbook was opened by the user and must be closed in Excel.");
            if (!discardChanges && !workbook.Saved) throw new InvalidOperationException("Save the workbook in Excel or explicitly discard its changes.");
            workbook.Close(SaveChanges: false);
            books[workbookId].Dispose();
            books.Remove(workbookId);
            foreach (var key in highlights.Where(h => h.Value.Receipt.WorkbookId == workbookId).Select(h => h.Key).ToArray()) highlights.Remove(key);
            return true;
        }));

    public Task<RangeData> ReadAsync(string workbookId, string sheet, CellRange range)
    {
        range.Validate();
        return queue.InvokeAsync(() => WithSheet(workbookId, sheet, (_, worksheet) => ExcelReadback.Read(worksheet, range)));
    }

    public Task<CellRange> UsedRangeAsync(string workbookId, string sheet) =>
        queue.InvokeAsync(() => WithSheet(workbookId, sheet, (_, worksheet) => ExcelReadback.Used(worksheet)));

    public Task<PreservationSnapshot> CaptureAsync(string workbookId) =>
        queue.InvokeAsync(() => WithBook(workbookId, ExcelReadback.Capture));

    private ExcelHostInfo StartCore()
    {
        if (info is not null) return info;
        var previous = new HashSet<int>();
        foreach (var process in Process.GetProcessesByName("EXCEL"))
            using (process) previous.Add(process.Id);
        Interop.Application? created = null;
        var isolated = false;
        try
        {
            created = new Interop.Application();
            GetWindowThreadProcessId((nint)created.Hwnd, out var pid);
            if (pid == 0 || previous.Contains((int)pid))
                throw new InvalidOperationException("Excel did not start in an independent process. The existing instance was left untouched.");
            isolated = true;
            created.Visible = true;
            created.UserControl = true;
            // msoAutomationSecurityForceDisable; numeric value avoids a second Office PIA dependency.
            created.AutomationSecurity = (Microsoft.Office.Core.MsoAutomationSecurity)3;
            info = new((int)pid, created.Version);
            application = created;
            created = null; // Root RCW is retained only for this STA session; released in ShutdownCore.
            return info;
        }
        finally
        {
            if (created is not null)
            {
                try { if (isolated) QuitIfEmpty(created); }
                finally { ComLifetime.Release(created); }
            }
        }
    }

    private ExcelWorkbook OpenCore(string path)
    {
        StartCore();
        foreach (var existing in ListCore())
            if (string.Equals(existing.FullPath, path, StringComparison.OrdinalIgnoreCase)) return existing;
        Interop.Workbooks? collection = null;
        Interop.Workbook? workbook = null;
        try
        {
            application!.AutomationSecurity = (Microsoft.Office.Core.MsoAutomationSecurity)3;
            collection = application.Workbooks;
            workbook = collection.Open(path, UpdateLinks: 0, ReadOnly: false, IgnoreReadOnlyRecommended: true, AddToMru: false);
            var result = Describe(workbook, true);
            return result;
        }
        finally { ComLifetime.Release(workbook); ComLifetime.Release(collection); }
    }

    private IReadOnlyList<ExcelWorkbook> ListCore()
    {
        StartCore();
        Interop.Workbooks? collection = null;
        try
        {
            collection = application!.Workbooks;
            var result = new List<ExcelWorkbook>();
            for (var i = 1; i <= collection.Count; i++)
            {
                Interop.Workbook? workbook = null;
                try { workbook = collection[i]; result.Add(Describe(workbook, false)); }
                finally { ComLifetime.Release(workbook); }
            }
            var liveIds = result.Select(w => w.Id).ToHashSet(StringComparer.Ordinal);
            foreach (var stale in books.Keys.Where(k => !liveIds.Contains(k)).ToArray())
            {
                books[stale].Dispose();
                books.Remove(stale);
            }
            return result;
        }
        finally { ComLifetime.Release(collection); }
    }

    private ExcelWorkbook Describe(Interop.Workbook workbook, bool opened)
    {
        var path = workbook.FullName;
        var identity = Identity(workbook);
        var registration = books.Values.FirstOrDefault(b => b.Identity == identity && b.Path == path);
        if (registration is null)
        {
            registration = new(Guid.NewGuid().ToString("N"), path, Marshal.GetIUnknownForObject(workbook), opened);
            books.Add(registration.Id, registration);
        }
        Interop.Sheets? sheets = null;
        try
        {
            sheets = workbook.Worksheets;
            var names = new List<string>();
            for (var i = 1; i <= sheets.Count; i++)
            {
                Interop.Worksheet? sheet = null;
                try { sheet = (Interop.Worksheet)(object)sheets[i]; names.Add(sheet.Name); }
                finally { ComLifetime.Release(sheet); }
            }
            return new(registration.Id, workbook.Name, path, names);
        }
        finally { ComLifetime.Release(sheets); }
    }

    private static nint Identity(Interop.Workbook workbook)
    {
        var pointer = Marshal.GetIUnknownForObject(workbook);
        try { return pointer; }
        finally { Marshal.Release(pointer); }
    }

    private T WithBook<T>(string id, Func<Interop.Workbook, T> operation)
    {
        if (!books.TryGetValue(id, out var registration)) throw new InvalidOperationException("Workbook is no longer open. Refresh the workbook list.");
        Interop.Workbooks? collection = null;
        try
        {
            collection = application!.Workbooks;
            for (var i = 1; i <= collection.Count; i++)
            {
                Interop.Workbook? workbook = null;
                try
                {
                    workbook = collection[i];
                    if (Identity(workbook) == registration.Identity && workbook.FullName == registration.Path)
                        return operation(workbook);
                }
                finally { ComLifetime.Release(workbook); }
            }
            throw new InvalidOperationException("Workbook was closed or saved under a new name. Refresh the workbook list.");
        }
        finally { ComLifetime.Release(collection); }
    }

    private T WithSheet<T>(string id, string name, Func<Interop.Workbook, Interop.Worksheet, T> operation) =>
        WithBook(id, workbook =>
        {
            Interop.Sheets? sheets = null;
            Interop.Worksheet? sheet = null;
            try
            {
                sheets = workbook.Worksheets;
                sheet = (Interop.Worksheet)(object)sheets[name];
                workbook.Activate();
                return operation(workbook, sheet);
            }
            finally { ComLifetime.Release(sheet); ComLifetime.Release(sheets); }
        });

    private static void QuitIfEmpty(Interop.Application app)
    {
        Interop.Workbooks? collection = null;
        try { collection = app.Workbooks; if (collection.Count == 0) app.Quit(); }
        finally { ComLifetime.Release(collection); }
    }

    private bool ShutdownCore()
    {
        var root = application;
        application = null;
        try
        {
            foreach (var registration in books.Values) registration.Dispose();
            books.Clear();
            highlights.Clear();
            // Any open book (including a user-created one) keeps the visible instance alive.
            if (root is not null) QuitIfEmpty(root);
            return true;
        }
        finally { ComLifetime.Release(root); info = null; }
    }

    public ValueTask DisposeAsync()
    {
        lock (disposalGate) return new(disposal ??= DisposeCoreAsync());
    }

    private Task DisposeCoreAsync() => queue.CompleteAsync(() => { ShutdownCore(); });

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
}
