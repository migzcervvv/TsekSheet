using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace TsekSheet.OcrEval;

internal static class Worker
{
    internal const int FrameLimit = 67_108_864;
    public static async Task Run(string configPath, string engine)
    {
        var config = Configuration.Load(configPath);
        OpenCvSharp.Cv2.SetNumThreads(config.Threads);
        using IOcrEngine ocr = engine == "paddle-v5-mobile" ? new PaddleEngine(config) :
            engine.StartsWith("tesseract-psm", StringComparison.Ordinal) && int.TryParse(engine[13..], out var psm) && psm is 3 or 6 or 11 ? new TesseractEngine(config, psm) : throw new ArgumentException("Unknown engine.");
        using var input = Console.OpenStandardInput(); using var output = Console.OpenStandardOutput();
        await Write(output, "READY"u8.ToArray(), default);
        while (true)
        {
            byte[]? profileBytes = await Read(input, default, allowEof: true); if (profileBytes is null) return;
            var profile = System.Text.Encoding.UTF8.GetString(profileBytes);
            if (profileBytes.Length > 64 || (!Imaging.Profiles.Contains(profile) && profile != "orientation")) throw new InvalidDataException("Invalid preprocessing profile.");
            byte[] png = (await Read(input, default))!;
            WorkerReply reply;
            try
            {
                if (profile == "orientation")
                {
                    var watchOrientation = Stopwatch.StartNew();
                    using var source = OpenCvSharp.Cv2.ImDecode(png, OpenCvSharp.ImreadModes.Color);
                    using var small = new OpenCvSharp.Mat();
                    var scale = Math.Min(1, 1200d / Math.Max(source.Width, source.Height));
                    OpenCvSharp.Cv2.Resize(source, small, new((int)(source.Width * scale), (int)(source.Height * scale)));
                    var best = double.NegativeInfinity; var correction = 0;
                    foreach (var degrees in new[] { 0, 90, 180, 270 })
                    {
                        using var rotated = PacketRules.Rotate(small, degrees);
                        var probe = await ocr.RecognizeAsync(rotated.ToBytes(".png"), default);
                        var score = PacketRules.OrientationScore(probe);
                        if (score > best) { best = score; correction = degrees; }
                    }
                    reply = new(new("", []), null, 0, watchOrientation.Elapsed.TotalSeconds, correction);
                    await Write(output, JsonSerializer.SerializeToUtf8Bytes(reply, Configuration.Json), default);
                    continue;
                }
                var watch = Stopwatch.StartNew(); var prepared = Imaging.Prepare(png, profile); var preprocessSeconds = watch.Elapsed.TotalSeconds;
                watch.Restart(); var result = await ocr.RecognizeAsync(prepared, default);
                reply = new(result, null, preprocessSeconds, watch.Elapsed.TotalSeconds);
            }
            catch (Exception error) { reply = new(null, error.GetType().Name + ": " + error.Message); }
            await Write(output, JsonSerializer.SerializeToUtf8Bytes(reply, Configuration.Json), default);
        }
    }
    public static async Task<byte[]?> Read(Stream stream, CancellationToken token, bool allowEof = false)
    {
        var header = new byte[4]; var first = await stream.ReadAsync(header.AsMemory(0, 1), token);
        if (first == 0 && allowEof) return null;
        if (first == 0) throw new EndOfStreamException("OCR worker exited.");
        await stream.ReadExactlyAsync(header.AsMemory(1), token);
        var length = System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > FrameLimit) throw new InvalidDataException("OCR pipe frame exceeds limits.");
        var result = new byte[length]; await stream.ReadExactlyAsync(result, token); return result;
    }
    public static async Task Write(Stream stream, byte[] bytes, CancellationToken token)
    {
        if (bytes.Length is < 1 or > FrameLimit) throw new InvalidDataException("OCR pipe frame exceeds limits.");
        var header = new byte[4]; System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
}

// Each engine lives outside the evaluator in a bounded job. Tesseract's child
// inherits the job; closing it stops only processes started by this evaluator.
internal sealed class OcrJob : SafeHandleZeroOrMinusOneIsInvalid
{
    public OcrJob(int memoryMb) : base(true)
    {
        SetHandle(CreateJobObject(IntPtr.Zero, null));
        if (IsInvalid) throw new System.ComponentModel.Win32Exception();
        var limits = new Extended { Basic = new Basic { Flags = 0x2000 | 0x100 | 0x200 | 0x8, ActiveProcesses = 2 }, ProcessMemory = (UIntPtr)((ulong)memoryMb * 1024 * 1024), JobMemory = (UIntPtr)((ulong)memoryMb * 1024 * 1024) };
        if (!SetInformationJobObject(handle, 9, ref limits, (uint)Marshal.SizeOf<Extended>())) { Dispose(); throw new System.ComponentModel.Win32Exception(); }
    }
    public void Attach(Process process)
    { if (!AssignProcessToJobObject(handle, process.Handle)) throw new System.ComponentModel.Win32Exception(); }
    public long WorkingSet()
    {
        var memory = Marshal.AllocHGlobal(8 + 8 * 8);
        try
        {
            if (!QueryInformationJobObject(handle, 3, memory, 72, out _)) return 0;
            long result = 0; var count = Math.Min(8, Marshal.ReadInt32(memory, 4));
            for (var i = 0; i < count; i++)
                try { using var process = Process.GetProcessById((int)Marshal.ReadInt64(memory, 8 + i * 8)); result += process.WorkingSet64; }
                catch (ArgumentException) { } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { }
            return result;
        }
        finally { Marshal.FreeHGlobal(memory); }
    }
    protected override bool ReleaseHandle() => CloseHandle(handle);
    [StructLayout(LayoutKind.Sequential)] private struct Basic { public long ProcessTime, JobTime; public uint Flags; public UIntPtr MinWorking, MaxWorking; public uint ActiveProcesses; public UIntPtr Affinity; public uint Priority, Scheduling; }
    [StructLayout(LayoutKind.Sequential)] private struct Io { public ulong A, B, C, D, E, F; }
    [StructLayout(LayoutKind.Sequential)] private struct Extended { public Basic Basic; public Io Io; public UIntPtr ProcessMemory, JobMemory, PeakProcess, PeakJob; }
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);
    [DllImport("kernel32.dll")] private static extern bool SetInformationJobObject(IntPtr job, int information, ref Extended limits, uint length);
    [DllImport("kernel32.dll")] private static extern bool QueryInformationJobObject(IntPtr job, int information, IntPtr buffer, uint length, out uint returned);
    [DllImport("kernel32.dll")] private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
}

internal sealed class EngineClient(string configPath, string engine, EvalConfig config) : IAsyncDisposable
{
    public int LastRotation { get; private set; }
    private Process? process; private OcrJob? job; private Task<string>? stderr;
    public double InitializationSeconds { get; private set; }
    private async Task Start(CancellationToken token)
    {
        var watch = Stopwatch.StartNew();
        var start = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "OcrEval.exe")) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "--worker", configPath, engine }) start.ArgumentList.Add(arg);
        job = new(config.MemoryMb); process = new() { StartInfo = start };
        if (!process.Start()) throw new IOException("OCR worker failed to start.");
        job.Attach(process); stderr = DrainError(process.StandardError);
        var ready = await Worker.Read(process.StandardOutput.BaseStream, token);
        if (!ready!.AsSpan().SequenceEqual("READY"u8)) throw new InvalidDataException("Invalid worker handshake.");
        InitializationSeconds += watch.Elapsed.TotalSeconds;
    }
    private static async Task<string> DrainError(StreamReader reader)
    {
        var kept = new System.Text.StringBuilder(); var buffer = new char[4096]; int read;
        while ((read = await reader.ReadAsync(buffer)) > 0) if (kept.Length < 8192) kept.Append(buffer, 0, Math.Min(read, 8192 - kept.Length));
        return kept.ToString();
    }
    public async Task<(OcrResult Result, double Seconds, double PeakMb, double PreprocessSeconds, double OcrSeconds)> Recognize(byte[] png, string profile, CancellationToken token)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(TimeSpan.FromSeconds(config.TimeoutSeconds));
        try
        {
            if (process is null) await Start(deadline.Token);
            var watch = Stopwatch.StartNew(); long peak = job!.WorkingSet();
            using var sampling = new CancellationTokenSource();
            var sampler = Task.Run(async () =>
            {
                try { while (true) { peak = Math.Max(peak, job.WorkingSet()); await Task.Delay(20, sampling.Token); } }
                catch (OperationCanceledException) { }
            });
            WorkerReply? reply;
            try
            {
                await Worker.Write(process!.StandardInput.BaseStream, System.Text.Encoding.UTF8.GetBytes(profile), deadline.Token);
                await Worker.Write(process.StandardInput.BaseStream, png, deadline.Token);
                reply = JsonSerializer.Deserialize<WorkerReply>((await Worker.Read(process.StandardOutput.BaseStream, deadline.Token))!, Configuration.Json);
            }
            finally { sampling.Cancel(); await sampler; }
            if (reply?.Result is null) throw new InvalidDataException(reply?.Error ?? "No OCR result.");
            LastRotation = reply.Rotation;
            return (reply.Result, watch.Elapsed.TotalSeconds, peak / 1048576d, reply.PreprocessSeconds, reply.OcrSeconds);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }
    public async ValueTask DisposeAsync()
    {
        job?.Dispose(); job = null;
        if (process is not null)
        {
            try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)); } catch (TimeoutException) { }
            process.Dispose(); process = null;
        }
        if (stderr is not null) { try { var text = await stderr.WaitAsync(TimeSpan.FromSeconds(2)); if (text.Length > 0) Console.Error.WriteLine(text); } catch (TimeoutException) { } stderr = null; }
    }
}
