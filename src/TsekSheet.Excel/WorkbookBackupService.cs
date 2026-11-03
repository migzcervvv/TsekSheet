using System.Security.Cryptography;
using TsekSheet.Application.Excel;

namespace TsekSheet.Excel;

internal sealed class WorkbookBackupService(string? backupFolder)
{
    public BackupReceipt Create(string original)
    {
        var folder = backupFolder ?? Path.GetDirectoryName(original)!;
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, $"{Path.GetFileNameWithoutExtension(original)}.tseksheet-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}{Path.GetExtension(original)}");
        using (var source = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var copy = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
            source.CopyTo(copy);
            copy.Flush(true);
        }
        static string Hash(string p)
        {
            using var file = new FileStream(p, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return Convert.ToHexString(SHA256.HashData(file));
        }
        var hash = Hash(path);
        if (hash != Hash(original)) throw new IOException("The workbook changed while its backup was being verified. No cells were written.");
        return new(path, hash);
    }
}
