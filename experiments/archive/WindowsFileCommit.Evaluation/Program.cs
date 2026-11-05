using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

// Isolated research only. No production references or workbook access.
if (args.Length == 3 && args[0] == "child-create")
{
    File.WriteAllText(args[1], args[2]);
    return;
}
if (args.Length == 3 && args[0] == "child-create-new")
{
    try { using var f = new FileStream(args[1], FileMode.CreateNew, FileAccess.Write, FileShare.None); f.Write(Encoding.UTF8.GetBytes(args[2])); f.Flush(true); }
    catch (IOException) { Environment.ExitCode = 2; }
    return;
}
if (args.Length == 3 && args[0] == "child-move") { File.Move(args[1], args[2]); return; }
if (args.Length == 3 && args[0] == "child-delete") { File.Delete(args[1]); return; }

if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
var root = Path.Combine(Path.GetTempPath(), "TsekSheet-commit-evaluation-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var counts = new Dictionary<string, int>();
var failures = new List<string>();
var candidate = Encoding.UTF8.GetBytes("synthetic candidate \u03a9");
var original = Encoding.UTF8.GetBytes("synthetic original");
var unrelated = Encoding.UTF8.GetBytes("unrelated synthetic file");
var executable = Environment.ProcessPath!;
var assembly = System.Reflection.Assembly.GetExecutingAssembly().Location;
var runtimeHosted = Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase);

void Check(bool value, string message) { if (!value) throw new Exception(message); }
void Bytes(string path, byte[] expected) => Check(File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(expected), "Unexpected bytes or missing file: " + Path.GetFileName(path));
void ChildCreate(string path, string value)
{
    var start = new ProcessStartInfo(executable) { UseShellExecute = false };
    if (runtimeHosted) start.ArgumentList.Add(assembly);
    start.ArgumentList.Add("child-create"); start.ArgumentList.Add(path); start.ArgumentList.Add(value);
    using var child = Process.Start(start) ?? throw new Exception("Child failed to start");
    Check(child.WaitForExit(10000) && child.ExitCode == 0, "Child failed");
}
void ChildAction(string command, string first, string second)
{
    var start = new ProcessStartInfo(executable) { UseShellExecute = false };
    if (runtimeHosted) start.ArgumentList.Add(assembly);
    start.ArgumentList.Add(command); start.ArgumentList.Add(first); start.ArgumentList.Add(second);
    using var child = Process.Start(start) ?? throw new Exception("Child failed to start");
    Check(child.WaitForExit(10000) && child.ExitCode == 0, "Child failed");
}
void Run(string name, Action<string> test)
{
    var dir = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
    var source = Path.Combine(dir, "source.bin"); var bystander = Path.Combine(dir, "unrelated.bin");
    File.WriteAllBytes(source, original); File.WriteAllBytes(bystander, unrelated);
    var sourceHash = SHA256.HashData(File.ReadAllBytes(source));
    try
    {
        test(dir);
        counts[name] = counts.GetValueOrDefault(name) + 1;
    }
    catch (Exception e) { failures.Add(name + ": " + e.GetType().Name + ": " + e.Message); }
    finally { try { Bytes(source, original); Bytes(bystander, unrelated); Check(sourceHash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(source))), "Source hash changed"); } catch(Exception e) { failures.Add(name + " preservation: " + e.Message); } }
}
void Stage(string path) { using var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); f.Write(candidate); f.Flush(true); }
bool MoveRejected(string from, string to) { try { File.Move(from, to, false); return false; } catch (IOException) { return true; } }

for (var i = 0; i < 30; i++)
{
    Run("new destination", d => { var s = Path.Combine(d,"stage"); var t = Path.Combine(d,"target"); Stage(s); var id=Identity.Of(s); File.Move(s,t,false); Bytes(t,candidate); Check(id==Identity.Of(t),"File identity changed"); Check(!File.Exists(s),"Stage still exists"); });
    Run("existing destination", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); File.WriteAllBytes(t,original); var id=Identity.Of(t); Check(MoveRejected(s,t),"Existing target overwritten"); Bytes(t,original); Check(id==Identity.Of(t),"Target identity changed"); Bytes(s,candidate); });
    Run("other process creates destination at boundary", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); ChildCreate(t,"external"); Check(MoveRejected(s,t),"Concurrent target overwritten"); Bytes(t,Encoding.UTF8.GetBytes("external")); Bytes(s,candidate); });
    Run("concurrent create-new race", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); var p=new ProcessStartInfo(executable) { UseShellExecute=false }; if(runtimeHosted) p.ArgumentList.Add(assembly); p.ArgumentList.Add("child-create-new"); p.ArgumentList.Add(t); p.ArgumentList.Add("racer"); using var child=Process.Start(p)!; var moved=!MoveRejected(s,t); Check(child.WaitForExit(10000),"Child timeout"); if(moved) { Check(child.ExitCode==2,"Both operations succeeded"); Bytes(t,candidate); } else { Check(child.ExitCode==0,"Neither operation succeeded"); Bytes(t,Encoding.UTF8.GetBytes("racer")); Bytes(s,candidate); } });
    Run("fingerprint then external change", d => { var t=Path.Combine(d,"target"); File.WriteAllBytes(t,original); var hash=SHA256.HashData(File.ReadAllBytes(t)); ChildCreate(t,"changed!"); Check(!hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(t))),"Change missed"); Bytes(t,Encoding.UTF8.GetBytes("changed!")); });
    Run("destination locked against replacement", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); var b=Path.Combine(d,"backup"); Stage(s); File.WriteAllBytes(t,original); using(var locked=new FileStream(t,FileMode.Open,FileAccess.Read,FileShare.Read)) { var rejected=false; try { File.Replace(s,t,b); } catch(IOException) { rejected=true; } Check(rejected,"Replacement unexpectedly succeeded with delete denied"); } Bytes(t,original); Bytes(s,candidate); });
    Run("replacement fingerprint race demonstration", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); var b=Path.Combine(d,"backup"); Stage(s); File.WriteAllBytes(t,original); var hash=SHA256.HashData(File.ReadAllBytes(t)); ChildCreate(t,"changed!"); Check(!hash.SequenceEqual(SHA256.HashData(File.ReadAllBytes(t))),"Change not established"); File.Replace(s,t,b); Bytes(t,candidate); Bytes(b,Encoding.UTF8.GetBytes("changed!")); });
    Run("destination deleted after fingerprint", d => { var t=Path.Combine(d,"target"); File.WriteAllBytes(t,original); _=SHA256.HashData(File.ReadAllBytes(t)); ChildAction("child-delete",t,"unused"); Check(!File.Exists(t),"Deletion missed"); });
    Run("destination renamed after fingerprint", d => { var t=Path.Combine(d,"target"); var moved=Path.Combine(d,"moved"); File.WriteAllBytes(t,original); _=SHA256.HashData(File.ReadAllBytes(t)); ChildAction("child-move",t,moved); Check(!File.Exists(t),"Rename missed"); Bytes(moved,original); });
    Run("cancel before boundary", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); using var c=new CancellationTokenSource(); c.Cancel(); try { c.Token.ThrowIfCancellationRequested(); File.Move(s,t,false); throw new Exception("Cancellation ignored"); } catch(OperationCanceledException) {} Check(!File.Exists(t),"Cancelled commit wrote destination"); Bytes(s,candidate); });
    Run("injected staging failure", d => { var t=Path.Combine(d,"target"); try { throw new IOException("injected"); } catch(IOException) {} Check(!File.Exists(t),"Staging failure wrote destination"); });
    Run("injected validation failure", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); Check(!SHA256.HashData(File.ReadAllBytes(s)).SequenceEqual(SHA256.HashData(original)),"Fault not detected"); Check(!File.Exists(t),"Validation failure wrote destination"); Bytes(s,candidate); });
    Run("injected precommit failure", d => { var s=Path.Combine(d,"stage"); var t=Path.Combine(d,"target"); Stage(s); try { throw new IOException("injected"); } catch(IOException) {} Check(!File.Exists(t),"Commit failure wrote destination"); Bytes(s,candidate); });
}
foreach (var pair in counts.OrderBy(p => p.Key)) Console.WriteLine($"{pair.Key}: {pair.Value} passed");
foreach (var failure in failures) Console.Error.WriteLine("FAIL " + failure);
Console.WriteLine($"TOTAL: {counts.Values.Sum()} passed, {failures.Count} failed; temp={root}");
// Delete only files and directories created under this run's unique directory.
foreach (var dir in Directory.GetDirectories(root)) { foreach (var file in Directory.GetFiles(dir)) File.Delete(file); Directory.Delete(dir); }
Directory.Delete(root);
if (failures.Count != 0) Environment.ExitCode = 1;

internal static class Identity
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct Information
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [System.Runtime.InteropServices.DllImport("kernel32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file, out Information information);
    internal static string Of(string path)
    {
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (!GetFileInformationByHandle(handle, out var info)) throw new System.ComponentModel.Win32Exception();
        return $"{info.Volume:X8}:{info.IndexHigh:X8}{info.IndexLow:X8}";
    }
}
