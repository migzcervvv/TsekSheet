using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using ExcelLifecycle.Diagnostics;
using TsekSheet.Excel;
using TsekSheet.Workbooks;

namespace ExcelActivation.Experiment;

internal sealed record ProcessIdentity(int Pid, DateTime StartUtc, int Session, string Window);
internal sealed class Result
{
    public int Order { get; set; }
    public Trial Trial { get; set; } = new();
    public int LaunchPid { get; set; }
    public DateTime LaunchStartUtc { get; set; }
    public bool Acquired { get; set; }
    public int IdentityMismatches { get; set; }
    public string AcquisitionPane { get; set; } = "none (COM activation)";
    public string ApplicationWindow { get; set; } = "";
    public ProcessIdentity[] Baseline { get; set; } = [];
    public List<string> OwnershipChecks { get; set; } = [];
    public List<string> WindowInventory { get; set; } = [];
    public int UnexpectedPrompts { get; set; }
    public List<string> Backups { get; set; } = [];
    public int CorrectnessChecks { get; set; }
    public bool ConcurrentSessionUnchanged { get; set; }
    public bool AliveAt90Seconds { get; set; }
    public DateTime? LateExitUtc { get; set; }
    public double? LateExitSeconds { get; set; }
    public double? ExitDetectedSeconds { get; set; }
}

internal static class Program
{
    private static readonly Dictionary<string, string> admitted = new(StringComparer.OrdinalIgnoreCase);
    private static string output = "";
    private static readonly JsonSerializerOptions json = new() { WriteIndented = true };

    private static async Task<int> Main(string[] args)
    {
#if DEBUG
        throw new InvalidOperationException("Release only.");
#endif
        if (args.Length == 3 && args[0] == "cleanup-recorded-launch") return await CleanupRecorded(args[1], args[2]);
        if (args.Length != 4) throw new ArgumentException("validated-excel.exe synthetic-seed.xlsx fresh-output-directory native-stack-tool");
        var executable = Path.GetFullPath(args[0]);
        var seed = Path.GetFullPath(args[1]);
        output = Path.GetFullPath(args[2]);
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "GOALS.md"))) repository = repository.Parent;
        var fixtures = Path.Combine(repository!.FullName, "fixtures", "excel");
        if (!seed.StartsWith(fixtures + Path.DirectorySeparatorChar + "t1-", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(seed) != ".xlsx")
            throw new ArgumentException("Only the retained Excel-authored synthetic .xlsx seed is admitted.");
        if (Hash(seed) != "41515AFF89A6E7F1F12EA40B3E75728F55B05A55CAC6D2A66AAD6EF03097F116")
            throw new ArgumentException("This experiment only admits the retained, value-only synthetic seed fingerprint.");
        if (!Path.IsPathFullyQualified(args[0]) || !Path.GetFileName(executable).Equals("EXCEL.EXE", StringComparison.OrdinalIgnoreCase) ||
            FileVersionInfo.GetVersionInfo(executable).CompanyName != "Microsoft Corporation") throw new ArgumentException("Invalid Excel executable.");
        RejectReparse(executable); RejectReparse(seed);
        if (Directory.Exists(output)) throw new IOException("Evidence directory already exists; choose a fresh directory.");
        Directory.CreateDirectory(output);
        // Executable Authenticode validation and startup inventory are performed by Run.ps1 before this command.
        Save("environment", new { Executable = executable, ExecutableSha256 = Hash(executable), FileVersionInfo.GetVersionInfo(executable).FileVersion,
            Seed = seed, SeedSha256 = Hash(seed), Runtime = Environment.Version.ToString(), OS = Environment.OSVersion.ToString(),
            Session = Process.GetCurrentProcess().SessionId, StartupUtc = DateTime.UtcNow, Repeats = 5, Scenarios = new[] { "launch", "open", "bulk", "formula", "repeated", "multiple" } });
        var baseline = Census(); Save("processes-before", baseline);
        var existing = baseline.Select(x => (Identity: x, Process: Process.GetProcessById(x.Pid))).ToArray();
        foreach (var item in existing)
        {
            _ = item.Process.SafeHandle;
            if (item.Process.StartTime.ToUniversalTime() != item.Identity.StartUtc) throw new InvalidOperationException("Baseline PID reused.");
        }
        var root = Path.Combine(fixtures, "t1-activation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string Copy(string name)
        {
            var copy = Path.Combine(root, name + ".xlsx");
            File.Copy(seed, copy, false);
            if (Hash(copy) != Hash(seed)) throw new IOException("Working-copy hash mismatch.");
            admitted.Add(copy, Hash(copy));
            Admit(copy, root);
            return copy;
        }
        var observers = new List<Task>();
        var results = new List<Result>();
        var sentinelResult = new Result { Trial = new() { Environment = "x", Scenario = "concurrent-sentinel", Repeat = 0 } };
        var sentinelQueue = new StaCommandQueue();
        var sentinel = new OwnedSession(executable, root, sentinelResult);
        var sentinelHash = "";
        var stopped = false;
        try
        {
            var bootstrap = Copy("sentinel-bootstrap");
            var workbook = Copy("sentinel-open-workbook");
            await sentinelQueue.InvokeAsync(() => { sentinel.Start("x", bootstrap); sentinel.Open(workbook); sentinelHash = sentinel.Snapshot(); return 0; }).WaitAsync(TimeSpan.FromSeconds(45));
            Save("sentinel-acquired", sentinelResult);
            for (var repeat = 1; repeat <= 5 && !stopped; repeat++)
            foreach (var scenario in new[] { "launch", "open", "bulk", "formula", "repeated", "multiple" })
            foreach (var mode in repeat % 2 == 1 ? new[] { "com", "x" } : new[] { "x", "com" })
            {
                if (stopped) break;
                var result = new Result { Order = results.Count + 1, Trial = new() { Environment = mode, Scenario = scenario, Repeat = repeat } };
                results.Add(result);
                var bootstrapCopy = Copy($"{result.Order:D2}-bootstrap");
                var first = Copy($"{result.Order:D2}-working");
                var second = Copy($"{result.Order:D2}-second");
                var queue = new StaCommandQueue();
                var session = new OwnedSession(executable, root, result);
                try
                {
                    await queue.InvokeAsync(() =>
                    {
                        try
                        {
                            session.Start(mode, bootstrapCopy);
                            if (scenario != "launch") session.Open(first);
                            if (scenario is "bulk" or "formula" or "repeated" or "multiple") session.Write(scenario != "bulk", 0);
                            if (scenario == "repeated") for (var i = 1; i <= 9; i++) session.Write(true, i);
                            if (scenario == "multiple")
                            {
                                var firstHash = session.Snapshot();
                                session.Open(second); session.Write(true, 3); session.CloseLast();
                                if (session.Snapshot() != firstHash) throw new InvalidOperationException("First workbook changed while editing second.");
                            }
                        }
                        catch (Exception error) { result.Trial.Error = error.ToString(); }
                        finally { session.Cleanup(); }
                        return 0;
                    }).WaitAsync(TimeSpan.FromSeconds(60));
                    await Stop(queue);
                    result.Trial.StaTerminated = true;
                    result.ConcurrentSessionUnchanged = await sentinelQueue.InvokeAsync(() => sentinel.Snapshot() == sentinelHash).WaitAsync(TimeSpan.FromSeconds(15));
                    if (!result.ConcurrentSessionUnchanged || existing.Any(x => x.Process.WaitForExit(0)))
                        throw new InvalidOperationException("Concurrent/baseline session changed or exited; stopping.");
                    if (!result.Acquired || result.IdentityMismatches != 0 || result.Trial.QuitReturnedUtc == default)
                    {
                        stopped = true;
                        Save($"result-{result.Order:D2}", result);
                    }
                    else
                    {
                        var observation = Observe(result, session.Process!, args[3]);
                        observers.Add(observation);
                        // Keep the ten-second decision separate from the longer, concurrent diagnostic observation.
                        await Task.WhenAny(observation, Task.Delay(TimeSpan.FromSeconds(10.5)));
                    }
                }
                catch (Exception error)
                {
                    result.Trial.Error ??= error.ToString(); stopped = true;
                    Save($"result-{result.Order:D2}", result);
                    // A timed-out native COM call cannot be cancelled safely. External watchdog bounds this controller.
                }
                Console.WriteLine($"Order {result.Order}/60 {mode}/{scenario} #{repeat}: acquired={result.Acquired}; error={result.Trial.Error ?? "none"}");
                Save("order-and-results", results);
            }
        }
        catch (Exception error) { sentinelResult.Trial.Error ??= error.ToString(); stopped = true; }
        finally
        {
            try { await sentinelQueue.InvokeAsync(() => { if (sentinelResult.Acquired) sentinelResult.ConcurrentSessionUnchanged = sentinel.Snapshot() == sentinelHash; sentinel.Cleanup(); return 0; }).WaitAsync(TimeSpan.FromSeconds(20)); await Stop(sentinelQueue); sentinelResult.Trial.StaTerminated = true; }
            catch (Exception error) { sentinelResult.Trial.Error ??= error.ToString(); }
            if (sentinel.Process is not null && sentinelResult.Trial.QuitReturnedUtc != default) observers.Add(Observe(sentinelResult, sentinel.Process, args[3]));
            else Save("sentinel-final", sentinelResult);
            await Task.WhenAll(observers);
            Save("order-and-results", results);
            Save("sentinel-final", sentinelResult);
            Save("processes-after", Census());
            Save("baseline-ownership-after", existing.Select(x => new { x.Identity, StillRunning = !x.Process.WaitForExit(0) }).ToArray());
            foreach (var item in existing) item.Process.Dispose();
        }
        var failures = results.Count(x => !x.Trial.PassedTenSeconds || !x.ConcurrentSessionUnchanged);
        Save("summary", new { Attempts = results.Count, Completed = results.Count(x => x.Trial.QuitReturnedUtc != default), Passed = results.Count - failures, Failed = failures, Stopped = stopped,
            SentinelUnchanged = sentinelResult.ConcurrentSessionUnchanged, SentinelExit = sentinelResult.Trial.PassedTenSeconds });
        return !stopped && results.Count == 60 && failures == 0 && sentinelResult.Trial.PassedTenSeconds ? 0 : 1;
    }

    private static async Task<int> CleanupRecorded(string evidenceDirectory, string executable)
    {
        // Evidence remains immutable: cleanup has its own record and does not convert a failed trial to a pass.
        var retained = Path.GetFullPath(evidenceDirectory);
        var failed = JsonSerializer.Deserialize<Result>(File.ReadAllText(Path.Combine(retained, "result-21.json")))!;
        if (failed.Acquired || failed.Order != 21 || failed.LaunchPid == 0 || failed.Trial.QuitReturnedUtc != default)
            throw new InvalidOperationException("Not the retained rejected launch.");
        using var admission = JsonDocument.Parse(File.ReadAllText(Path.Combine(retained, "admission-21-bootstrap.json")));
        var bootstrap = admission.RootElement.GetProperty("Path").GetString()!;
        var hash = admission.RootElement.GetProperty("Sha256").GetString()!;
        if (hash != "41515AFF89A6E7F1F12EA40B3E75728F55B05A55CAC6D2A66AAD6EF03097F116") throw new InvalidOperationException("Unexpected synthetic seed fingerprint.");
        output = Path.Combine(retained, "cleanup-revalidation-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfffffff"));
        Directory.CreateDirectory(output);
        admitted.Add(bootstrap, hash);
        var cleanup = new Result { LaunchPid = failed.LaunchPid, LaunchStartUtc = failed.LaunchStartUtc, Baseline = failed.Baseline,
            Trial = new() { Environment = "x", Scenario = "cleanup-only-revalidation", Repeat = 0 } };
        var queue = new StaCommandQueue();
        var session = new OwnedSession(Path.GetFullPath(executable), Path.GetDirectoryName(bootstrap)!, cleanup);
        await queue.InvokeAsync(() =>
        {
            try { session.RevalidateForCleanup(bootstrap); }
            catch (Exception error) { cleanup.Trial.Error = error.ToString(); }
            finally { session.Cleanup(); }
            return 0;
        }).WaitAsync(TimeSpan.FromSeconds(20));
        await Stop(queue); cleanup.Trial.StaTerminated = true;
        if (cleanup.Trial.QuitReturnedUtc != default) await ExitObservation.ObserveAsync(cleanup.Trial, session.Process!, output, null);
        Save("rejected-launch-cleanup", cleanup);
        Save("processes-after-cleanup", Census());
        session.Process?.Dispose();
        return cleanup.Trial.PassedTenSeconds ? 0 : 1;
    }

    private static async Task Stop(StaCommandQueue queue)
    {
        var thread = (Thread)typeof(StaCommandQueue).GetField("thread", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(queue)!;
        await queue.DisposeAsync();
        if (!await Task.Run(() => thread.Join(10_000))) throw new TimeoutException("STA did not terminate.");
    }

    private static async Task Observe(Result result, Process process, string stack)
    {
        try
        {
            await ExitObservation.ObserveAsync(result.Trial, process, output, stack);
            var deadline = result.Trial.QuitReturnedUtc.AddSeconds(90);
            while (!process.WaitForExit(0) && DateTime.UtcNow < deadline) await Task.Delay(100);
            result.AliveAt90Seconds = !process.WaitForExit(0);
            if (!result.AliveAt90Seconds)
            {
                result.ExitDetectedSeconds = (DateTime.UtcNow - result.Trial.QuitReturnedUtc).TotalSeconds;
                process.Refresh();
                var exit = process.ExitTime.ToUniversalTime();
                if (exit >= result.LaunchStartUtc)
                {
                    result.LateExitUtc = exit;
                    result.LateExitSeconds = (exit - result.Trial.QuitReturnedUtc).TotalSeconds;
                }
            }
            Save(result.Order == 0 ? "sentinel-final" : $"result-{result.Order:D2}", result);
        }
        finally { process.Dispose(); }
    }

    internal static void Admit(string path, string root)
    {
        if (!Path.IsPathFullyQualified(path) || !path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(path) != ".xlsx" || !admitted.TryGetValue(path, out var hash) || Hash(path) != hash)
            throw new InvalidOperationException("Working-copy path/hash not admitted.");
        RejectReparse(path);
        // All admission exceptions (macros, links, protection, unsafe/invalid packages and limits)
        // remain fatal. Compatibility findings are retained; no package is rewritten here.
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var copy = new MemoryStream();
        input.CopyTo(copy);
        var bytes = copy.ToArray();
        if (Convert.ToHexString(SHA256.HashData(bytes)) != hash) throw new IOException("Admission content changed during read.");
        var facts = PackageGuard.Inspect(bytes, new WorkbookLimits(), CancellationToken.None);
        Save("admission-" + Path.GetFileNameWithoutExtension(path), new { Path = path, Sha256 = hash, facts.Findings });
    }

    private static void RejectReparse(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Reparse-point path rejected.");
    }

    internal static string Hash(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(input));
    }
    internal static ProcessIdentity[] Census()
    {
        var result = new List<ProcessIdentity>();
        foreach (var p in Process.GetProcessesByName("EXCEL")) using (p) result.Add(new(p.Id, p.StartTime.ToUniversalTime(), p.SessionId, $"0x{p.MainWindowHandle:X}"));
        return result.ToArray();
    }
    private static void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, json));
}
