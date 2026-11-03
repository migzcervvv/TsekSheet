using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Forms = System.Windows.Forms;

namespace TsekSheet.Excel;

internal sealed class StaCommandQueue : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly ConcurrentQueue<Action> commands = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Thread thread;
    private Forms.Control? dispatcher;
    private Forms.ApplicationContext? context;
    private bool closing;
    private bool executing;

    public StaCommandQueue()
    {
        thread = new Thread(Run) { Name = "TsekSheet Excel STA", IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public async Task<T> InvokeAsync<T>(Func<T> command)
    {
        await ready.Task.ConfigureAwait(false);
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(closing, this);
            commands.Enqueue(() =>
            {
                try { completion.SetResult(command()); }
                catch (COMException error) when (BusyRetryPolicy.IsBusy(error.HResult))
                { completion.SetException(new ExcelBusyException(error)); }
                catch (Exception error) { completion.SetException(error); }
                finally { ComLifetime.Collect(); }
            });
            dispatcher!.BeginInvoke((Action)DrainOne);
        }
        return await completion.Task.ConfigureAwait(false);
    }

    private void DrainOne()
    {
        // Excel can pump window messages while a COM call is in progress. Never allow a
        // nested BeginInvoke to run another workbook command during that call.
        if (executing) return;
        executing = true;
        try { if (commands.TryDequeue(out var command)) command(); }
        finally
        {
            executing = false;
            if (!commands.IsEmpty) dispatcher!.BeginInvoke((Action)DrainOne);
        }
    }

    private void Run()
    {
        try
        {
            using var filter = new BusyMessageFilter();
            using var control = new Forms.Control();
            using var appContext = new Forms.ApplicationContext();
            dispatcher = control;
            context = appContext;
            _ = control.Handle;
            ready.SetResult();
            Forms.Application.Run(appContext);
        }
        catch (Exception error) { ready.TrySetException(error); stopped.TrySetException(error); }
        finally { ComLifetime.Collect(); stopped.TrySetResult(); }
    }

    public ValueTask DisposeAsync() => new(CompleteAsync(null));

    internal async Task CompleteAsync(Action? cleanup)
    {
        await ready.Task.ConfigureAwait(false);
        lock (gate)
        {
            if (!closing)
            {
                closing = true;
                commands.Enqueue(() =>
                {
                    try { cleanup?.Invoke(); }
                    catch (Exception error) { stopped.TrySetException(error); }
                    finally { ComLifetime.Collect(); context!.ExitThread(); }
                });
                dispatcher!.BeginInvoke((Action)DrainOne);
            }
        }
        await stopped.Task.ConfigureAwait(false);
    }
}

internal static class ComLifetime
{
    public static void Release(object? value)
    {
        if (value is not null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value);
    }

    public static void Collect()
    {
        GC.Collect(); GC.WaitForPendingFinalizers();
        GC.Collect(); GC.WaitForPendingFinalizers();
    }
}
