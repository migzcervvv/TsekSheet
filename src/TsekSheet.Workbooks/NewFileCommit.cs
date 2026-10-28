using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32.SafeHandles;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed class CommitFaults
{
    internal Action<string>? AfterStage;
    internal Action? BeforeCommit;
    internal Action? AfterCommit;
    internal bool CleanupFailure;
}

// Narrow Windows x64/local NTFS boundary. It has no replace or direct-write-to-destination operation.
internal static class NewFileCommit
{
    internal static void Commit(string destination, byte[] bytes, string hash, CancellationToken token, CommitFaults? faults = null)
    {
        token.ThrowIfCancellationRequested();
        var path = ValidatePath(destination);
        using var directories = new DirectoryLocks(Path.GetDirectoryName(path)!);
        if (File.Exists(path) || Directory.Exists(path)) throw Reject("DESTINATION_EXISTS", "Choose a new filename. Existing files cannot be replaced.");
        var stage = Path.Combine(Path.GetDirectoryName(path)!, ".tseksheet-" + Guid.NewGuid().ToString("N") + ".tmp");
        using var handle = Open(stage, 0xC0010000, 1, 1, 0x80); // READ/WRITE/DELETE; share READ; CREATE_NEW
        FileStream? stream = null;
        bool committed = false;
        try
        {
            stream = new FileStream(handle, FileAccess.ReadWrite);
            stream.Write(bytes); stream.Flush(true); stream.Position = 0;
            if (Convert.ToHexString(SHA256.HashData(stream)) != hash) throw Reject("CANDIDATE_REJECTED", "Staged workbook differs from the validated candidate.");
            var identity = Information(handle);
            faults?.AfterStage?.Invoke(stage);
            token.ThrowIfCancellationRequested();
            faults?.BeforeCommit?.Invoke();
            token.ThrowIfCancellationRequested();
            try { RenameNew(handle, path); }
            catch
            {
                // An unexpected native failure must never cause cleanup to delete a file
                // that may already have moved. Retain the object unless its old name is proven.
                committed = true;
                if (HasPath(handle, stage)) committed = false;
                throw;
            }
            committed = true; // No cancellation or deletion after the commit boundary.
            faults?.AfterCommit?.Invoke();
            using var final = Open(path, 0x80000000, 7, 3, 0x00200000); // Compatible read handle; original still excludes writes/deletes.
            var actual = Information(final);
            if (identity.Volume != actual.Volume || identity.IndexHigh != actual.IndexHigh || identity.IndexLow != actual.IndexLow || (actual.Attributes & 0x400) != 0)
                throw new IOException("Committed file identity differs.");
            using var read = new FileStream(final, FileAccess.Read);
            if (Convert.ToHexString(SHA256.HashData(read)) != hash) throw new IOException("Committed file bytes differ.");
        }
        catch (Exception) when (committed)
        {
            throw Reject("SAVE_UNCERTAIN", "The save outcome could not be verified. Inspect the selected file and any staging files before retrying. Unsaved state is retained.");
        }
        finally
        {
            try
            {
                if (!committed && (faults?.CleanupFailure == true || !DeleteOnClose(handle)))
                    throw Reject("TEMP_CLEANUP_FAILED", "An owned staging file could not be removed. Inspect the selected folder for .tseksheet-*.tmp files. The destination was not committed.");
            }
            finally
            {
                try { stream?.Dispose(); }
                catch (Exception) when (committed) { throw Reject("SAVE_UNCERTAIN", "The save outcome could not be verified after closing the file. Inspect the selected file before retrying."); }
            }
        }
    }

    internal static string ValidatePath(string destination)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || !System.Text.RegularExpressions.Regex.IsMatch(destination, @"^[A-Za-z]:\\"))
            throw Reject("UNSUPPORTED_DESTINATION", "Choose an absolute path on a local fixed NTFS drive.");
        var path = Path.GetFullPath(destination);
        if (path.Length > 220 || !Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw Reject("INVALID_DESTINATION", "Choose a new .xlsx filename with a path of at most 220 characters.");
        foreach (var part in destination[3..].Split('\\'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.') || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || System.Text.RegularExpressions.Regex.IsMatch(part.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant))
                throw Reject("INVALID_DESTINATION", "Choose a regular filename without device names, streams or path aliases.");
        }
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.DriveType != DriveType.Fixed || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw Reject("UNSUPPORTED_DESTINATION", "Saving supports local fixed NTFS drives only.");
        return path;
    }

    private sealed class DirectoryLocks : IDisposable
    {
        private readonly List<SafeFileHandle> handles = [];
        internal DirectoryLocks(string parent)
        {
            try
            {
                var root = Path.GetPathRoot(parent)!;
                var paths = new List<string> { root }; var current = root;
                foreach (var segment in parent[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries)) { current = Path.Combine(current, segment); paths.Add(current); }
                foreach (var path in paths)
                {
                    var handle = Open(path, 0x80, 1, 3, 0x02200000); handles.Add(handle); // READ_ATTRIBUTES; no WRITE/DELETE sharing
                    var info = Information(handle);
                    if ((info.Attributes & 0x410) != 0x10) throw Reject("UNSUPPORTED_DESTINATION", "Linked or redirected destination folders are unsupported.");
                    if (!HasPath(handle, path))
                        throw Reject("UNSUPPORTED_DESTINATION", "Aliased or redirected destination folders are unsupported.");
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var handle in handles) handle.Dispose(); handles.Clear(); }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RenameInfo { public uint Flags; public IntPtr Root; public uint Length; public char Name; }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    private static FileInformation Information(SafeFileHandle handle)
    {
        if (!Native.GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info;
    }
    private static bool HasPath(SafeFileHandle handle, string path)
    {
        var resolved = new StringBuilder(1024);
        var length = Native.GetFinalPathNameByHandleW(handle, resolved, (uint)resolved.Capacity, 0);
        return length > 0 && length < resolved.Capacity && resolved.ToString().TrimEnd('\\').Equals((@"\\?\" + path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
    private static SafeFileHandle Open(string path, uint access, uint share, uint disposition, uint flags)
    {
        var handle = Native.CreateFileW(path, access, share, IntPtr.Zero, disposition, flags, IntPtr.Zero);
        if (handle.IsInvalid) { var error = Marshal.GetLastWin32Error(); handle.Dispose(); throw new Win32Exception(error); }
        return handle;
    }
    private static void RenameNew(SafeFileHandle handle, string destination)
    {
        if ((int)Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.Name)) != 20 || (int)Marshal.OffsetOf<RenameInfo>(nameof(RenameInfo.Root)) != 8)
            throw Reject("UNSUPPORTED_DESTINATION", "Unexpected Windows rename structure layout.");
        var name = Encoding.Unicode.GetBytes(destination);
        var buffer = new byte[20 + name.Length + sizeof(char)]; // NUL required by Win32 path conversion; length excludes it.
        BitConverter.GetBytes(name.Length).CopyTo(buffer, 16); name.CopyTo(buffer, 20);
        var pointer = Marshal.AllocHGlobal(buffer.Length);
        try
        {
            Marshal.Copy(buffer, 0, pointer, buffer.Length);
            if (!Native.SetFileInformationByHandle(handle, 3, pointer, (uint)buffer.Length))
            {
                var error = Marshal.GetLastWin32Error();
                if (error is 80 or 183) throw Reject("DESTINATION_EXISTS", "A file appeared at the destination. Nothing was overwritten. Choose a new filename.");
                throw new Win32Exception(error);
            }
        }
        finally { Marshal.FreeHGlobal(pointer); }
    }
    private static bool DeleteOnClose(SafeFileHandle handle)
    {
        var pointer = Marshal.AllocHGlobal(sizeof(int));
        try { Marshal.WriteInt32(pointer, 1); return Native.SetFileInformationByHandle(handle, 4, pointer, sizeof(int)); }
        finally { Marshal.FreeHGlobal(pointer); }
    }
    private static WorkbookException Reject(string code, string message) => new(code, message);
    private static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetFileInformationByHandle(SafeFileHandle file, int kind, IntPtr info, uint size);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    }
}
