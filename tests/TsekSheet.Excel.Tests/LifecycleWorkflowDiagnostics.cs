using System.Diagnostics;
using ExcelLifecycle.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class LifecycleWorkflowDiagnostics
{
    // Three fixed original-workflow repetitions. Original assertions execute unchanged.
    [TestMethod] public Task OriginalT2Run1() => Run(1);
    [TestMethod] public Task OriginalT2Run2() => Run(2);
    [TestMethod] public Task OriginalT2Run3() => Run(3);
    internal static EvidenceContext? Current { get; private set; }

    private static async Task Run(int repeat)
    {
#if DEBUG
        Assert.Fail("Excel diagnostics require Release.");
#endif
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        var repository = directory!.FullName;
        Current = new EvidenceContext(repository, repeat);
        try { await new SectionInsertTests().FiveInsertsPreserveNeighboursFillFormulasAndExtendOnlyTheSectionSubtotal("columns", false); }
        finally { Current = null; }
    }

    internal sealed class EvidenceContext(string repository, int repeat)
    {
        private readonly Dictionary<int, Trial> owned = [];
        private readonly string output = Path.GetFullPath(Environment.GetEnvironmentVariable("TSEKSHEET_LIFECYCLE_OUTPUT")
            ?? Path.Combine(repository, "artifacts", "lifecycle-matrix", "original"));
        internal void FixtureCreated(int pid, uint nativeThread, string version, string build)
        {
            using var process = Process.GetProcessById(pid);
            var trial = new Trial { Scenario = "t2-fixture", Environment = "original", Repeat = repeat, Pid = pid,
                StartUtc = process.StartTime.ToUniversalTime(), Version = version, Build = build,
                FileVersion = FileVersionInfo.GetVersionInfo(process.MainModule!.FileName).FileVersion ?? "",
                ManagedThread = Environment.CurrentManagedThreadId, NativeThread = nativeThread,
                Apartment = Thread.CurrentThread.GetApartmentState().ToString() };
            trial.Windows.Add(ExitObservation.WindowState(process, "fixture visible, UserControl=true"));
            owned.Add(pid, trial);
        }
        internal void FixtureQuit(int pid)
        {
            if (!owned.TryGetValue(pid, out var trial)) return;
            trial.QuitReturnedUtc = DateTime.UtcNow;
            trial.QuitStatus = "fixture Quit returned with zero open workbooks";
        }
        internal void HostCleanup(int pid)
        {
            using var process = Process.GetProcessById(pid);
            var version = FileVersionInfo.GetVersionInfo(process.MainModule!.FileName);
            var trial = new Trial { Scenario = "t2-five-inserts", Environment = "original", Repeat = repeat, Pid = pid,
                StartUtc = process.StartTime.ToUniversalTime(), Version = version.FileVersion ?? "", Build = version.ProductVersion ?? "",
                FileVersion = version.FileVersion ?? "",
                Apartment = "ExcelHost dedicated serialized STA; observer does not access COM",
                TimingAnchor = "Host.DisposeAsync return (exact Quit return not directly instrumented)" };
            trial.Windows.Add(ExitObservation.WindowState(process, "host cleanup entry"));
            owned.Add(pid, trial);
        }
        internal void HostDisposed(int pid, DateTime? quitUtc)
        {
            if (!owned.TryGetValue(pid, out var trial)) return;
            trial.QuitReturnedUtc = quitUtc ?? throw new InvalidOperationException("Owned host did not Quit.");
            trial.QuitStatus = "Quit returned with zero workbooks; exact adapter timestamp";
            trial.TimingAnchor = "Quit return, including subsequent GC and STA termination";
            trial.Events.Add($"{DateTime.UtcNow:O} Host disposal returned without COM exception");
        }
        internal void OwnerThreadStopped(int pid, object owner)
        {
            if (!owned.TryGetValue(pid, out var trial)) return;
            const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var queue = owner is ExcelHost ? typeof(ExcelHost).GetField("queue", fields)!.GetValue(owner)! : owner;
            var thread = (Thread)queue.GetType().GetField("thread", fields)!.GetValue(queue)!;
            trial.ManagedThread = thread.ManagedThreadId;
            trial.StaTerminated = !thread.IsAlive;
            trial.Apartment = "Dedicated STA configured by StaCommandQueue; managed thread identity observed after disposal";
            trial.Events.Add($"{DateTime.UtcNow:O} owning STA observed: managed thread={thread.ManagedThreadId}, alive={thread.IsAlive}");
        }
        internal async Task ObserveAsync(int pid, Process process)
        {
            if (!owned.TryGetValue(pid, out var trial)) throw new InvalidOperationException("No diagnostic ownership record.");
            if (process.StartTime.ToUniversalTime() != trial.StartUtc) throw new InvalidOperationException("Owned PID identity changed.");
            trial.Events.Add($"{DateTime.UtcNow:O} original exit check entered; original COM helper methods have returned");
            await ExitObservation.ObserveAsync(trial, process, output,
                Path.Combine(repository, "artifacts", "lifecycle-debugger", "engine", "content", "amd64", "ExcelStack.exe"));
            Assert.IsTrue(trial.PassedTenSeconds, $"Original T2 owned PID={pid} missed its unchanged ten-second deadline; late exit={trial.ExitSeconds} seconds.");
        }
    }
}
