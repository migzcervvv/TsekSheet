using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using ExcelLifecycle.Diagnostics;

namespace TsekSheet.Excel.Tests;

internal static class OwnedExcelProcesses
{
    private sealed class Owned(Process process)
    {
        internal readonly object Gate = new();
        internal readonly Process Process = process;
        internal readonly DateTime StartUtc = process.StartTime.ToUniversalTime();
        internal Task<bool>? Observation;
        internal bool Exited;
    }

    private static readonly ConcurrentDictionary<int, Owned> owned = new();

    internal static void Track(int pid)
    {
        owned.GetOrAdd(pid, id =>
        {
            var process = Process.GetProcessById(id);
            _ = process.SafeHandle;
            Console.Error.WriteLine($"{DateTime.UtcNow:O} owned PID={id}, start={process.StartTime.ToUniversalTime():O}");
            return new(process);
        });
    }

    internal static void AssertNoPreviousOwnedInstance()
    {
        var alive = new List<int>();
        foreach (var pair in owned)
            lock (pair.Value.Gate)
                if (pair.Value.Observation is not null && !pair.Value.Exited && !ExitObservation.WaitForExit(pair.Value.Process, 0))
                    alive.Add(pair.Key);
        Console.Error.WriteLine($"{DateTime.UtcNow:O} previous owned instances alive at start: {string.Join(",", alive)} (count={alive.Count})");
        if (alive.Count != 0) throw new InvalidOperationException("Host start found a previous owned Excel instance alive.");
    }

    internal static void LogQuit(int pid, DateTime quitUtc, int cycle)
    {
        Track(pid);
        var entry = owned[pid];
        lock (entry.Gate)
        {
            if (entry.Observation is not null) return;
            var output = Environment.GetEnvironmentVariable("TSEKSHEET_LIFECYCLE_OUTPUT")
                ?? Path.Combine(AppContext.BaseDirectory, "TestResults", "exit120");
            Directory.CreateDirectory(output);
            var stem = Path.Combine(output, $"exit120-{cycle}-{pid}-{entry.StartUtc.Ticks}");
            var initial = new { Cycle = cycle, Pid = pid, entry.StartUtc, QuitReturnedUtc = quitUtc };
            File.WriteAllText(stem + ".running.json", JsonSerializer.Serialize(initial));
            // Functional tests do not await this task. Only the lifecycle test asserts
            // process liveness at the D11 deadline; latency itself is diagnostic data.
            entry.Observation = Task.Run(() =>
            {
                var remaining = (int)Math.Clamp(Math.Ceiling((quitUtc.AddSeconds(120) - DateTime.UtcNow).TotalMilliseconds), 0, 120_000);
                var exited = ExitObservation.WaitForExit(entry.Process, remaining);
                var exitUtc = exited ? ExitObservation.ExitUtc(entry.Process) : (DateTime?)null;
                var goneAtDeadline = exited && exitUtc <= quitUtc.AddSeconds(120);
                var seconds = (exitUtc - quitUtc)?.TotalSeconds;
                File.WriteAllText(stem + ".json", JsonSerializer.Serialize(new
                {
                    Cycle = cycle,
                    Pid = pid,
                    entry.StartUtc,
                    QuitReturnedUtc = quitUtc,
                    ExitUtc = exitUtc,
                    LatencySeconds = seconds,
                    AliveAt120Seconds = !goneAtDeadline
                }));
                Console.Error.WriteLine($"PID={pid}; Quit->exit seconds={seconds?.ToString("F6") ?? "unobserved"}; alive at 120 seconds={!goneAtDeadline}.");
                if (exited)
                    lock (entry.Gate)
                    {
                        entry.Exited = true;
                        if (goneAtDeadline) owned.TryRemove(pid, out _);
                        entry.Process.Dispose();
                    }
                return goneAtDeadline;
            });
        }
    }

    internal static Task<bool> WaitForExitAsync(int pid) =>
        owned.TryGetValue(pid, out var entry)
            ? entry.Observation ?? throw new InvalidOperationException("Quit has not been recorded.")
            : Task.FromResult(true);

    internal static async Task<bool> WaitForPendingExitsAsync()
    {
        var observations = owned.Values.Select(entry => entry.Observation).OfType<Task<bool>>().ToArray();
        return (await Task.WhenAll(observations)).All(exited => exited);
    }
}
