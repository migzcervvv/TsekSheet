namespace TsekSheet.Excel.Tests;

// Every synthetic host is disposed through this scope, including assertion/open failures.
// Only this test's own host is used; no ROT attachment, GUI action, process kill or user file.
internal sealed class ExcelTestScope : IAsyncDisposable
{
    internal ExcelHost Host { get; } = new() { LifecycleLog = Console.Error.WriteLine };
    private bool disposed;
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var process = (await Host.StartAsync()).ProcessId;
        ExcelFixture.TrackProcess(process);
        LifecycleWorkflowDiagnostics.Current?.HostCleanup(process);
        Console.Error.WriteLine($"Host cleanup started: owned Excel PID={process}.");
        try
        {
            var workbooks = await Host.ListWorkbooksAsync();
            Console.Error.WriteLine($"Host cleanup: owned Excel PID={process}, open workbooks={workbooks.Count}.");
            foreach (var book in workbooks)
            {
                var path = Path.GetFullPath(book.FullPath);
                var repository = new DirectoryInfo(AppContext.BaseDirectory);
                while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "GOALS.md"))) repository = repository.Parent;
                if (repository is null) throw new InvalidOperationException("Test repository root unavailable.");
                var root = Path.Combine(repository.FullName, "fixtures", "excel") + Path.DirectorySeparatorChar;
                if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected non-fixture workbook in the test-owned host; left untouched.");
                await Host.CloseAsync(book.Id, discardChanges: true);
            }
        }
        finally
        {
            await Host.DisposeAsync();
            LifecycleWorkflowDiagnostics.Current?.HostDisposed(process, Host.QuitReturnedUtc);
            LifecycleWorkflowDiagnostics.Current?.OwnerThreadStopped(process, Host);
            Console.Error.WriteLine($"Host disposal returned: owned Excel PID={process}; checking process exit.");
            await ExcelFixture.WaitForExitAsync(process, Host.QuitReturnedUtc);
        }
    }
}
