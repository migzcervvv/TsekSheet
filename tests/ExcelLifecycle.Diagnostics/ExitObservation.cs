using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace ExcelLifecycle.Diagnostics;

public sealed class Trial
{
    public string Scenario { get; set; } = "";
    public string Environment { get; set; } = "";
    public int Repeat { get; set; }
    public int Pid { get; set; }
    public DateTime StartUtc { get; set; }
    public string Version { get; set; } = "";
    public string Build { get; set; } = "";
    public string FileVersion { get; set; } = "";
    public int ManagedThread { get; set; }
    public uint NativeThread { get; set; }
    public string Apartment { get; set; } = "";
    public bool? StaTerminated { get; set; }
    public string TimingAnchor { get; set; } = "Quit return";
    public DateTime QuitReturnedUtc { get; set; }
    public string QuitStatus { get; set; } = "not called";
    public DateTime? ExitUtc { get; set; }
    public double? ExitSeconds { get; set; }
    public bool PassedTenSeconds { get; set; }
    public bool AliveAtObservationLimit { get; set; }
    public string? Error { get; set; }
    public List<string> Events { get; } = [];
    public List<string> Addins { get; } = [];
    public List<string> Windows { get; } = [];
    public List<string> Releases { get; } = [];
    public string? NativeStack { get; set; }
}

public static class ExitObservation
{
    // The extra 20 seconds are diagnostic observation, never an acceptance extension.
    public static async Task ObserveAsync(Trial trial, Process process, string output, string? stackTool)
    {
        Directory.CreateDirectory(output);
        var tenSecondDeadline = trial.QuitReturnedUtc.AddSeconds(10);
        var observationDeadline = trial.QuitReturnedUtc.AddSeconds(30);
        await File.WriteAllTextAsync(Path.Combine(output, $"{trial.Environment}-{trial.Scenario}-{trial.Repeat}-{trial.Pid}.running.json"),
            JsonSerializer.Serialize(trial));
        await Task.Run(() => WaitForExit(process, RemainingMilliseconds(tenSecondDeadline)));
        trial.Windows.Add(WindowState(process, "10-second check"));
        // Use OS exit time: a delayed observer must not turn an on-time exit into a failure.
        SetExit(trial, process);
        trial.PassedTenSeconds = trial.ExitUtc <= tenSecondDeadline && trial.Error is null;
        if (!trial.PassedTenSeconds && !WaitForExit(process, 0) && stackTool is not null && File.Exists(stackTool))
        {
            var nativePath = Path.Combine(output, $"{trial.Environment}-{trial.Scenario}-{trial.Repeat}-{trial.Pid}.stack.log");
            var start = new ProcessStartInfo(stackTool) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add(trial.Pid.ToString());
            try
            {
                using var capture = Process.Start(start)!;
                var stdout = capture.StandardOutput.ReadToEndAsync();
                var stderr = capture.StandardError.ReadToEndAsync();
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                try { await capture.WaitForExitAsync(deadline.Token); }
                catch (OperationCanceledException) { throw new TimeoutException("Native capture exceeded eight seconds; Excel was not terminated."); }
                await File.WriteAllTextAsync(nativePath, await stdout + await stderr);
                trial.NativeStack = nativePath;
                trial.Events.Add($"{DateTime.UtcNow:O} native noninvasive stack capture returned {capture.ExitCode}; snapshot may briefly suspend owned Excel");
            }
            catch (Exception error) { trial.Events.Add("Native capture: " + error.Message); }
        }
        if (!WaitForExit(process, 0) && DateTime.UtcNow < observationDeadline)
        {
            await Task.Run(() => WaitForExit(process, RemainingMilliseconds(observationDeadline)));
        }
        SetExit(trial, process);
        trial.AliveAtObservationLimit = !WaitForExit(process, 0);
        await File.WriteAllTextAsync(Path.Combine(output, $"{trial.Environment}-{trial.Scenario}-{trial.Repeat}-{trial.Pid}.json"),
            JsonSerializer.Serialize(trial, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{trial.Environment} {trial.Scenario} #{trial.Repeat}: PID={trial.Pid}, ten-second={(trial.PassedTenSeconds ? "PASS" : "FAIL")}, exit seconds={trial.ExitSeconds?.ToString("F3") ?? ">30"}");
    }

    private static void SetExit(Trial trial, Process process)
    {
        if (!WaitForExit(process, 0)) return;
        var exit = ExitUtc(process);
        if (exit < trial.StartUtc)
        {
            trial.Events.Add($"{DateTime.UtcNow:O} OS exit timestamp unavailable (raw={exit:O}); no fabricated exit time recorded");
            return;
        }
        trial.ExitUtc = exit;
        trial.ExitSeconds = (trial.ExitUtc.Value - trial.QuitReturnedUtc).TotalSeconds;
    }
    private static int RemainingMilliseconds(DateTime deadline) => (int)Math.Clamp(Math.Ceiling((deadline - DateTime.UtcNow).TotalMilliseconds), 0, 90_000);

    // A managed HasExited/exit-code observation can precede kernel teardown and
    // expose a zero exit FILETIME. Retain the owned handle and wait for its signal.
    public static bool WaitForExit(Process process, int milliseconds)
    {
        var result = WaitForSingleObject(process.SafeHandle, (uint)milliseconds);
        return result switch
        {
            0 => true,
            258 => false,
            _ => throw new Win32Exception(Marshal.GetLastWin32Error())
        };
    }

    public static DateTime ExitUtc(Process process)
    {
        if (!GetProcessTimes(process.SafeHandle, out _, out var exit, out _, out _))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return DateTime.FromFileTimeUtc(exit);
    }

    public static string WindowState(Process process, string phase)
    {
        try
        {
            process.Refresh();
            var window = process.MainWindowHandle;
            return $"{DateTime.UtcNow:O} {phase}: exited={WaitForExit(process, 0)}, HWND=0x{window:X}, visible={window != 0 && IsWindowVisible(window)}, title={process.MainWindowTitle}";
        }
        catch (InvalidOperationException) { return $"{DateTime.UtcNow:O} {phase}: exited"; }
    }

    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(nint window);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint WaitForSingleObject(SafeProcessHandle handle, uint milliseconds);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process, out long creation, out long exit, out long kernel, out long user);
}
