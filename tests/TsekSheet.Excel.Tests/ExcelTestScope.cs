namespace TsekSheet.Excel.Tests;

// Scopes close synthetic workbooks even on assertion/open failures. Functional tests
// share a host retired at suite end; dedicated hosts are retired by their own scopes.
// Exit observation is asynchronous; only the lifecycle test awaits it.
internal sealed class ExcelTestScope : IAsyncDisposable
{
    private static readonly SemaphoreSlim sharedGate = new(1);
    private static ExcelHost? sharedHost;
    private readonly bool ownsHost;
    internal ExcelHost Host { get; }

    internal ExcelTestScope(bool ownsHost = false)
    {
        this.ownsHost = ownsHost;
        if (ownsHost) OwnedExcelProcesses.AssertNoPreviousOwnedInstance();
        Host = ownsHost ? new() { LifecycleLog = Console.Error.WriteLine }
            : sharedHost ?? throw new InvalidOperationException("Author the fixture before creating its scope.");
    }

    internal static async Task<ExcelHost> SharedHostAsync()
    {
        await sharedGate.WaitAsync();
        try
        {
            if (sharedHost is null)
            {
                OwnedExcelProcesses.AssertNoPreviousOwnedInstance();
                var host = new ExcelHost { LifecycleLog = Console.Error.WriteLine };
                var info = await host.StartAsync();
                OwnedExcelProcesses.Track(info.ProcessId);
                sharedHost = host;
            }
            return sharedHost;
        }
        finally { sharedGate.Release(); }
    }

    internal static async Task RetireSharedHostAsync()
    {
        await sharedGate.WaitAsync();
        try
        {
            if (sharedHost is null) return;
            var host = sharedHost;
            sharedHost = null;
            await CloseFixtureWorkbooksAsync(host);
            var pid = (await host.StartAsync()).ProcessId;
            await host.DisposeAsync();
            if (host.QuitReturnedUtc is { } quitUtc) OwnedExcelProcesses.LogQuit(pid, quitUtc, 0);
        }
        finally { sharedGate.Release(); }
    }
    private bool disposed;
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var process = (await Host.StartAsync()).ProcessId;
        Console.Error.WriteLine($"Host cleanup started: owned Excel PID={process}.");
        try
        {
            await CloseFixtureWorkbooksAsync(Host);
        }
        finally
        {
            if (ownsHost)
            {
                OwnedExcelProcesses.Track(process);
                await Host.DisposeAsync();
                if (Host.QuitReturnedUtc is { } quitUtc)
                    OwnedExcelProcesses.LogQuit(process, quitUtc, ExcelFixture.MeasurementCycle);
            }
        }
    }

    private static async Task CloseFixtureWorkbooksAsync(ExcelHost host)
    {
        var workbooks = await host.ListWorkbooksAsync();
        Console.Error.WriteLine($"Host cleanup: open workbooks={workbooks.Count}.");
        foreach (var book in workbooks)
        {
            var path = Path.GetFullPath(book.FullPath);
            var repository = new DirectoryInfo(AppContext.BaseDirectory);
            while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "GOALS.md"))) repository = repository.Parent;
            if (repository is null) throw new InvalidOperationException("Test repository root unavailable.");
            var root = Path.Combine(repository.FullName, "fixtures", "excel") + Path.DirectorySeparatorChar;
            if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unexpected non-fixture workbook in the test-owned host; left untouched.");
            await host.CloseAsync(book.Id, discardChanges: true);
        }
    }
}
