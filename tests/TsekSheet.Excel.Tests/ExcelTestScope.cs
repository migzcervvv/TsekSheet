namespace TsekSheet.Excel.Tests;

// Every synthetic host is disposed through this scope, including assertion/open failures.
// Only this test's own host is used; no ROT attachment, GUI action, process kill or user file.
internal sealed class ExcelTestScope : IAsyncDisposable
{
    internal ExcelHost Host { get; } = new();
    private bool disposed;
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        var process = (await Host.StartAsync()).ProcessId;
        try
        {
            foreach (var book in await Host.ListWorkbooksAsync())
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
            await ExcelFixture.WaitForExitAsync(process);
        }
    }
}
