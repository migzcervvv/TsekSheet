using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;
using static TsekSheet.Application.Documents.LocalDocumentSources;

namespace TsekSheet.Application.Documents;

// Narrow read-only fixed-NTFS adapter. No file write, deletion, staging or decode.
internal sealed class WindowsDocumentReader : ILocalDocumentReader
{
    internal Action<int>? AfterChunk { get; init; }

    public DocumentObservation Read(string selectedPath, long maxBytes, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            var path = ValidatePath(selectedPath);
            using var directories = new DirectoryReads(Path.GetDirectoryName(path)!, token);
            using var file = Open(path, 0x80000000, 0x00200000); // GENERIC_READ; OPEN_REPARSE_POINT
            var before = Information(file);
            if ((before.Attributes & 0x410) != 0 || !HasPath(file, path))
                throw Error("UNSUPPORTED_SOURCE", "Select a regular local document without links or aliases.");
            var length = ((long)before.SizeHigh << 32) | before.SizeLow;
            if (length < 8 || length > maxBytes)
                throw Error("SOURCE_SIZE_LIMIT", "Select a nonempty document within the configured byte limit.");
            using var stream = new FileStream(file, FileAccess.Read);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            Span<byte> header = stackalloc byte[8];
            var headerLength = 0;
            long total = 0;
            while (true)
            {
                token.ThrowIfCancellationRequested();
                var read = stream.Read(buffer, 0, (int)Math.Min(buffer.Length, maxBytes - total + 1));
                if (read == 0) break;
                total += read;
                if (total > maxBytes) throw Error("SOURCE_SIZE_LIMIT", "The document exceeds the configured byte limit.");
                var take = Math.Min(8 - headerLength, read);
                buffer.AsSpan(0, take).CopyTo(header[headerLength..]);
                headerLength += take;
                hash.AppendData(buffer, 0, read);
                AfterChunk?.Invoke(read);
            }
            token.ThrowIfCancellationRequested();
            var after = Information(file);
            if (total != length || Identity(before) != Identity(after) || Time(before.Write) != Time(after.Write))
                throw Error("SOURCE_CHANGED", "The selected document changed during its read.");
            var kind = Kind(path, header, headerLength);
            return new(Identity(after), Convert.ToHexString(hash.GetHashAndReset()), total, kind, Time(after.Write));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception
            or ArgumentException or NotSupportedException)
        {
            // Never retain the platform exception: its message may include paths.
            throw Error("SOURCE_UNAVAILABLE", "The selected local document could not be read. Check the file and select it again.");
        }
    }

    private static string ValidatePath(string selectedPath)
    {
        if (!OperatingSystem.IsWindows() || !Environment.Is64BitProcess || selectedPath == null
            || !Regex.IsMatch(selectedPath, @"^[A-Za-z]:\\") || selectedPath.Length > 240)
            throw Error("UNSUPPORTED_SOURCE", "Choose an absolute file path on a local fixed NTFS drive.");
        foreach (var part in selectedPath[3..].Split('\\'))
        {
            if (part.Length == 0 || part is "." or ".." || part.EndsWith(' ') || part.EndsWith('.')
                || part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                || Regex.IsMatch(part.Split('.')[0], "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                throw Error("UNSUPPORTED_SOURCE", "Choose a regular document path without streams, device names or aliases.");
        }
        var path = Path.GetFullPath(selectedPath);
        if (Path.GetExtension(path).ToLowerInvariant() is not (".pdf" or ".png" or ".jpg" or ".jpeg"))
            throw Error("UNSUPPORTED_SOURCE_TYPE", "Select a PDF, PNG or JPEG document.");
        var drive = new DriveInfo(Path.GetPathRoot(path)!);
        if (drive.DriveType != DriveType.Fixed || !drive.DriveFormat.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            throw Error("UNSUPPORTED_SOURCE", "Choose a document on a local fixed NTFS drive.");
        return path;
    }

    private static LocalDocumentKind Kind(string path, ReadOnlySpan<byte> header, int length)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        if (length == 8)
        {
            if (extension == ".pdf" && header[..5].SequenceEqual("%PDF-"u8)) return LocalDocumentKind.Pdf;
            if (extension == ".png" && header.SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return LocalDocumentKind.Png;
            if (extension is ".jpg" or ".jpeg" && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff) return LocalDocumentKind.Jpeg;
        }
        throw Error("UNSUPPORTED_SOURCE_CONTENT", "The document signature does not match its supported extension.");
    }

    private sealed class DirectoryReads : IDisposable
    {
        private readonly List<SafeFileHandle> _handles = [];
        internal DirectoryReads(string parent, CancellationToken token)
        {
            try
            {
                var root = Path.GetPathRoot(parent)!;
                var paths = new List<string> { root };
                var current = root;
                foreach (var segment in parent[root.Length..].Split('\\', StringSplitOptions.RemoveEmptyEntries))
                { current = Path.Combine(current, segment); paths.Add(current); }
                foreach (var path in paths)
                {
                    token.ThrowIfCancellationRequested();
                    var handle = Open(path, 0x80, 0x02200000); // READ_ATTRIBUTES; BACKUP_SEMANTICS | OPEN_REPARSE_POINT
                    _handles.Add(handle);
                    if ((Information(handle).Attributes & 0x410) != 0x10 || !HasPath(handle, path))
                        throw Error("UNSUPPORTED_SOURCE", "Linked or aliased source directories are unsupported.");
                }
            }
            catch { Dispose(); throw; }
        }
        public void Dispose() { foreach (var handle in _handles) handle.Dispose(); _handles.Clear(); }
    }

    private static SafeFileHandle Open(string path, uint access, uint flags)
    {
        var handle = Native.CreateFileW(path, access, 1, IntPtr.Zero, 3, flags, IntPtr.Zero); // share READ only; OPEN_EXISTING
        if (!handle.IsInvalid) return handle;
        var error = Marshal.GetLastWin32Error();
        handle.Dispose();
        throw new Win32Exception(error);
    }
    private static FileInformation Information(SafeFileHandle handle)
    {
        if (!Native.GetFileInformationByHandle(handle, out var info)) throw new Win32Exception(Marshal.GetLastWin32Error());
        return info;
    }
    private static bool HasPath(SafeFileHandle handle, string path)
    {
        var resolved = new StringBuilder(512);
        var size = Native.GetFinalPathNameByHandleW(handle, resolved, (uint)resolved.Capacity, 0);
        return size > 0 && size < resolved.Capacity && resolved.ToString().TrimEnd('\\')
            .Equals((@"\\?\" + path).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    }
    private static DocumentFileIdentity Identity(FileInformation info) =>
        new(info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow, Time(info.Creation));
    private static long Time(System.Runtime.InteropServices.ComTypes.FILETIME time) =>
        ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
        public uint Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    private static class Native
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileHandle CreateFileW(string name, uint access, uint share, IntPtr security, uint disposition, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint size, uint flags);
    }
}
