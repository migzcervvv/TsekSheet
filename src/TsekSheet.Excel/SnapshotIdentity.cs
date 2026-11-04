using System.Security.Cryptography;
using System.Text;
using TsekSheet.Application.Excel;

namespace TsekSheet.Excel;

internal static class SnapshotIdentity
{
    // Fingerprint of plain Excel readback for stale-preview detection, never workbook bytes/XML.
    internal static string Hash(PreservationSnapshot snapshot)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        writer.Write(snapshot.Date1904);
        foreach (var sheet in snapshot.Sheets)
        {
            writer.Write(sheet.Name); writer.Write(sheet.Data.Range.ToString());
            foreach (var value in sheet.Data.Values) { writer.Write(value?.GetType().FullName ?? "null"); writer.Write(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? ""); }
            foreach (var value in sheet.Data.Formulas) writer.Write(value ?? "");
            foreach (var value in sheet.Data.NumberFormats) writer.Write(value);
            foreach (var value in sheet.MergedAreas) writer.Write(value);
            foreach (var table in sheet.Tables) { writer.Write(table.Name); writer.Write(table.Address); }
            writer.Write(sheet.ValidationCells); writer.Write(sheet.ValidationAreas); writer.Write(sheet.ConditionalFormats);
        }
        foreach (var name in snapshot.Names) { writer.Write(name.Name); writer.Write(name.RefersTo); }
        writer.Flush(); return Convert.ToHexString(SHA256.HashData(stream.ToArray()));
    }
}
