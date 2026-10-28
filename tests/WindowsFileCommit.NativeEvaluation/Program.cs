using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

// Research only: synthetic data and a unique owned directory. Never used by the application.
if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess) throw new PlatformNotSupportedException();
var root = Path.Combine(Path.GetTempPath(), "TsekSheet-native-commit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var failures = new List<string>(); var passed = 0;
Console.WriteLine($"Native FILE_RENAME_INFO offsets: root={Marshal.OffsetOf<Native.RenameInfo>(nameof(Native.RenameInfo.Root))}, length={Marshal.OffsetOf<Native.RenameInfo>(nameof(Native.RenameInfo.Length))}, name={Marshal.OffsetOf<Native.RenameInfo>(nameof(Native.RenameInfo.Name))}");
for (var i = 0; i < 100; i++)
{
    foreach (var mode in new[] { "relative", "absolute", "nt" }) foreach (var existing in new[] { false, true })
    {
        var dir = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        var stage = Path.Combine(dir, "stage.bin"); var target = Path.Combine(dir, "target.bin");
        var src = Path.Combine(dir, "source.bin"); File.WriteAllText(src, "original");
        if (existing) File.WriteAllText(target, "external");
        try
        {
            using var folder = Native.Open(dir, 0x80, 1, 3, 0x02200000);
            using var handle = Native.Open(stage, 0xC0010000, 0, 1, 0x80);
            using var stream = new FileStream(handle, FileAccess.ReadWrite);
            var bytes = Encoding.UTF8.GetBytes("candidate"); stream.Write(bytes); stream.Flush(true);
            bool blocked = false; try { File.WriteAllText(stage, "tamper"); } catch (IOException) { blocked = true; }
            if (!blocked) throw new Exception("stage was not protected");
            blocked = false; try { Directory.Move(dir, dir + "-moved"); } catch (IOException) { blocked = true; }
            if (!blocked) throw new Exception("directory rename not blocked");
            var result = Native.Rename(handle, mode == "relative" ? folder : null, mode switch { "relative" => "target.bin", "absolute" => target, _ => @"\??\" + target }, out var error);
            if (mode == "relative")
            {
                if (result || error != 87) throw new Exception("Unexpected directory-relative rename behavior");
                passed++; continue; // Explicitly unsupported variant: verify its stable rejection.
            }
            if (existing && result) { stream.Dispose(); throw new Exception($"unexpected success; target={(File.Exists(target) ? File.ReadAllText(target) : "missing")}; stage={(File.Exists(stage) ? File.ReadAllText(stage) : "missing")}; files={string.Join(",", Directory.GetFiles(dir).Select(Path.GetFileName))}"); }
            if (existing && error is not (80 or 183)) throw new Win32Exception(error);
            if (!existing && !result) throw new Win32Exception(error);
            stream.Dispose();
            if (!File.Exists(target) || File.ReadAllText(target) != (existing ? "external" : "candidate")) throw new Exception($"target bytes differ; target={(File.Exists(target) ? File.ReadAllText(target) : "missing")}; stage={(File.Exists(stage) ? File.ReadAllText(stage) : "missing")}; files={string.Join(",", Directory.GetFiles(dir).Select(Path.GetFileName))}");
            if (File.ReadAllText(src) != "original") throw new Exception("source changed");
            if (existing && !File.Exists(stage)) throw new Exception("stage missing after rejection");
            if (!existing && File.Exists(stage)) throw new Exception("stage present after commit");
            passed++;
        }
        catch (Exception ex) { failures.Add($"mode={mode} existing={existing}: {ex.GetType().Name}: {ex.Message}"); }
        finally { foreach (var file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
    }
}
Directory.Delete(root);
foreach (var group in failures.GroupBy(x => x)) Console.WriteLine($"FAIL {group.Count()}: {group.Key}");
Console.WriteLine($"{passed} passed, {failures.Count} failed");
Console.WriteLine("Native return values: " + string.Join(", ", Native.Returns.Select(p => $"{p.Key}={p.Value}")));
if (failures.Count != 0) Environment.ExitCode = 1;

internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
    internal struct RenameInfo { public byte Replace; public IntPtr Root; public uint Length; public char Name; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
    private static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
    [DllImport("kernel32.dll", SetLastError=true)]
    private static extern int SetFileInformationByHandle(SafeFileHandle file, int kind, IntPtr info, uint size);
    internal static readonly Dictionary<int, int> Returns = new();
    internal static SafeFileHandle Open(string name, uint access, uint share, uint disposition, uint flags)
    {
        var handle = CreateFileW(name, access, share, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid) { var error=Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }
    internal static bool Rename(SafeFileHandle file, SafeFileHandle? directory, string name, out int error)
    {
        var nameBytes = Encoding.Unicode.GetBytes(name); var offset = (int)Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.Name));
        // Win32 path conversion requires a terminating UTF-16 NUL even though FileNameLength
        // excludes it. Keep the flexible array and terminator inside the supplied allocation.
        var buffer = new byte[offset + nameBytes.Length + sizeof(char)];
        BitConverter.GetBytes(directory?.DangerousGetHandle().ToInt64() ?? 0).CopyTo(buffer, (int)Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.Root)));
        BitConverter.GetBytes(nameBytes.Length).CopyTo(buffer, (int)Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.Length)));
        nameBytes.CopyTo(buffer, offset);
        var pointer=Marshal.AllocHGlobal(buffer.Length);
        try { Marshal.Copy(buffer,0,pointer,buffer.Length); var raw=SetFileInformationByHandle(file,3,pointer,(uint)buffer.Length); var lastError=Marshal.GetLastWin32Error(); Returns[raw]=Returns.GetValueOrDefault(raw)+1; error=raw!=0 ? 0 : lastError; return raw != 0; }
        finally { Marshal.FreeHGlobal(pointer); }
    }
}
