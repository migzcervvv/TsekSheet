using Microsoft.VisualBasic.FileIO;
using System.Text;

namespace TsekSheet.OcrEval;

public static class Csv
{
    public static string[][] Read(string path)
    {
        using var reader = new TextFieldParser(path, Encoding.UTF8) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        reader.SetDelimiters(",");
        var rows = new List<string[]>();
        while (!reader.EndOfData)
        {
            var fields = reader.ReadFields()!;
            if (fields.Any(f => f.Length > 1_000_000) || rows.Count > 100_000) throw new InvalidDataException("CSV limits exceeded.");
            rows.Add(fields);
        }
        return rows.ToArray();
    }
    // Reports may be opened in Excel: untrusted OCR text is always literal.
    public static string Line(IEnumerable<string> fields) => string.Join(",", fields.Select(value =>
    {
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }));
    public static Truth[] Truths(string directory)
    {
        var rows = Read(Path.Combine(directory, "truth.csv"));
        if (rows.Length < 2 || !rows[0].SequenceEqual(new[] { "file", "vendor", "tin", "invoice", "date", "amount" }))
            throw new InvalidDataException("truth.csv must contain file,vendor,tin,invoice,date,amount.");
        var result = rows.Skip(1).Select(r => r.Length == 6 && r.Skip(1).All(v => v.Length <= 512) ? new Truth(r[0], new(r[1], r[2], r[3], r[4], r[5])) : throw new InvalidDataException("Invalid truth row or field over 512 characters.")).ToArray();
        if (result.Select(t => t.File).Distinct(StringComparer.OrdinalIgnoreCase).Count() != result.Length) throw new InvalidDataException("Duplicate truth file.");
        foreach (var row in result) InputPath(directory, row.File);
        return result;
    }
    public static string InputPath(string directory, string file)
    {
        var root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, file));
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !File.Exists(path) ||
            !new[] { ".pdf", ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant()))
            throw new InvalidDataException("Truth names must identify PDF/PNG/JPG files inside the input folder.");
        var part = Path.GetDirectoryName(path);
        while (part is not null && part.Length >= root.TrimEnd(Path.DirectorySeparatorChar).Length)
        {
            if ((File.GetAttributes(part) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked input folders are unavailable.");
            part = Path.GetDirectoryName(part);
        }
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Linked input files are unavailable.");
        return path;
    }
}
