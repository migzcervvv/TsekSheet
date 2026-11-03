using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;

namespace TsekSheet.Excel.Tests;

[TestClass]
public sealed class QueueAndRetryTests
{
    [TestMethod]
    public async Task QueueSerializesCommandsOnOnePumpedStaAndSurvivesCommandFailure()
    {
        await using var queue = new StaCommandQueue();
        await queue.InvokeAsync(() => true);
        var order = new List<int>();
        var active = false;
        var calls = Enumerable.Range(0, 20).Select(i => queue.InvokeAsync(() =>
        {
            Assert.IsFalse(active, "Pumping messages must not reenter the command queue.");
            active = true;
            order.Add(i);
            Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState());
            using var timer = new System.Windows.Forms.Timer { Interval = 1 };
            var tick = false;
            timer.Tick += (_, _) => tick = true;
            timer.Start();
            var deadline = Environment.TickCount64 + 2000;
            while (!tick && Environment.TickCount64 < deadline) System.Windows.Forms.Application.DoEvents();
            Assert.IsTrue(tick, "The STA must pump native messages.");
            active = false;
            return (Index: i, Thread: Environment.CurrentManagedThreadId);
        })).ToArray();
        var results = await Task.WhenAll(calls);
        Assert.AreEqual(1, results.Select(r => r.Thread).Distinct().Count());
        CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToArray(), results.Select(r => r.Index).ToArray());
        CollectionAssert.AreEqual(Enumerable.Range(0, 20).ToArray(), order.ToArray());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => queue.InvokeAsync<int>(() => throw new InvalidOperationException("test")));
        Assert.AreEqual(42, await queue.InvokeAsync(() => 42));
    }

    [TestMethod]
    public async Task QueueDrainsAcceptedWorkAndRejectsWorkAfterShutdown()
    {
        var queue = new StaCommandQueue();
        var first = queue.InvokeAsync(() => 1);
        var second = queue.InvokeAsync(() => 2);
        // Wait until accepted before requesting shutdown, rather than racing readiness.
        Assert.AreEqual(1, await first);
        Assert.AreEqual(2, await second);
        await queue.DisposeAsync();
        await queue.DisposeAsync();
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => queue.InvokeAsync(() => 3));
    }

    [TestMethod]
    public async Task BusyRejectionsHaveATenSecondDeadlineAndAnActionableError()
    {
        Assert.AreEqual(100, BusyRetryPolicy.RetryDelay(0, 2));
        Assert.AreEqual(1, BusyRetryPolicy.RetryDelay(9999, 2));
        Assert.AreEqual(-1, BusyRetryPolicy.RetryDelay(10000, 2));
        Assert.AreEqual(-1, BusyRetryPolicy.RetryDelay(12000, 1));
        Assert.AreEqual(-1, BusyRetryPolicy.RetryDelay(0, 3));
        await using var queue = new StaCommandQueue();
        var error = await Assert.ThrowsExactlyAsync<ExcelBusyException>(() => queue.InvokeAsync<int>(
            () => throw new COMException("busy", unchecked((int)0x80010001))));
        StringAssert.Contains(error.Message, "Excel is busy");
    }

    [TestMethod]
    public void InteropTypesAreEmbeddedAndNoPiaIsRequiredOrCopied()
    {
        var assembly = typeof(ExcelHost).Assembly;
        Assert.IsFalse(assembly.GetReferencedAssemblies().Any(a => a.Name is "office" or "Microsoft.Office.Interop.Excel"));
        Assert.IsFalse(assembly.GetReferencedAssemblies().Any(a => a.Name == "Microsoft.CSharp"), "Interop must not generate dynamic binder calls.");
        Assert.IsTrue(assembly.GetTypes().Any(t => t.Namespace == "Microsoft.Office.Interop.Excel" && t.IsImport));
        Assert.IsFalse(File.Exists(Path.Combine(AppContext.BaseDirectory, "Microsoft.Office.Interop.Excel.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(AppContext.BaseDirectory, "office.dll")));
    }
}
