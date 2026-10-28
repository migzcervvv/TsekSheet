using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

// A deliberately small subset: numeric literals and cell references joined by arithmetic.
internal static partial class CellCandidate
{
    private const string Token = @"(?:\d+(?:\.\d+)?|(?:(?:'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?\$?[A-Za-z]{1,3}\$?[1-9]\d{0,6})";
    private static readonly Regex Formula = new(@"^\s*[+-]?" + Token + @"(?:\s*[+\-*/]\s*[+-]?" + Token + @")*\s*$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly Regex Reference = new(@"(?:(?<sheet>'(?:[^']|'')+'|[A-Za-z_][A-Za-z0-9_.]*)!)?\$?(?<column>[A-Za-z]{1,3})\$?(?<row>[1-9]\d{0,6})", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    internal static string Validate(CellEdit edit, CellInfo original, IReadOnlyList<WorksheetInfo> sheets)
    {
        if (edit.Value == null || edit.Value.Length > 1024) throw PackageGuard.Reject("INVALID_VALUE", "Cell values must contain at most 1,024 characters.");
        if (original.Formula != null && edit.Kind != "formula") throw PackageGuard.Reject("FORMULA_PROTECTED", "This cell contains a formula. Choose an explicit formula edit instead.");
        switch (edit.Kind)
        {
            case "text":
                try { XmlConvert.VerifyXmlChars(edit.Value); } catch (XmlException) { throw PackageGuard.Reject("INVALID_VALUE", "Text contains unsupported control characters."); }
                return edit.Value;
            case "number":
                if (!double.TryParse(edit.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw PackageGuard.Reject("INVALID_VALUE", "Enter a finite number using a decimal point.");
                return number.ToString("R", CultureInfo.InvariantCulture);
            case "date":
                if (!DateOnly.TryParseExact(edit.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) || date < new DateOnly(1900, 3, 1)) throw PackageGuard.Reject("INVALID_VALUE", "Enter a date in YYYY-MM-DD format on or after 1900-03-01.");
                if (original.Kind != "DateTime") throw PackageGuard.Reject("UNSUPPORTED_CELL", "Date edits require a cell already formatted as a date.");
                return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            case "formula":
                var formula = edit.Value.StartsWith('=') ? edit.Value[1..] : edit.Value;
                if (!Formula.IsMatch(formula)) throw PackageGuard.Reject("UNSUPPORTED_FORMULA", "Use cell references and numbers with +, -, * or /. Functions, external links and other expressions are unsupported.");
                foreach (Match match in Reference.Matches(formula))
                {
                    var name = match.Groups["sheet"].Value;
                    if (name.StartsWith('\'')) name = name[1..^1].Replace("''", "'");
                    if (name.Length > 0 && !sheets.Any(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) throw PackageGuard.Reject("UNSUPPORTED_FORMULA", "Formula refers to a worksheet that is not in this workbook.");
                    var column = 0; foreach (var c in match.Groups["column"].Value.ToUpperInvariant()) column = column * 26 + c - 'A' + 1;
                    if (column > 16384 || int.Parse(match.Groups["row"].Value, CultureInfo.InvariantCulture) > 1048576) throw PackageGuard.Reject("UNSUPPORTED_FORMULA", "Formula contains a reference outside worksheet bounds.");
                }
                return formula;
            default: throw PackageGuard.Reject("INVALID_VALUE", "Choose text, number, date or formula.");
        }
    }

    internal static (byte[] Bytes, string PartName, string Address) Apply(byte[] source, string sheetName, CellEdit edit, string value, CancellationToken token)
    {
        using var stream = new MemoryStream(); stream.Write(source); stream.Position = 0;
        string partName; string address;
        using (var doc = SpreadsheetDocument.Open(stream, true, new OpenSettings { AutoSave = false }))
        {
            var wb = doc.WorkbookPart!;
            var sheet = wb.Workbook!.Sheets!.Elements<S.Sheet>().Single(s => s.Name!.Value == sheetName);
            var part = (WorksheetPart)wb.GetPartById(sheet.Id!); partName = part.Uri.OriginalString.TrimStart('/');
            address = ColumnName(edit.Column) + edit.Row.ToString(CultureInfo.InvariantCulture);
            var cell = part.Worksheet!.Descendants<S.Cell>().SingleOrDefault(c => c.CellReference?.Value == address)
                ?? throw PackageGuard.Reject("UNSUPPORTED_CELL", "Only existing populated cells can be edited in this milestone.");
            if (cell.GetAttributes().Any(a => a.LocalName is not ("r" or "s" or "t")) || cell.ChildElements.Any(c => c is not (S.CellFormula or S.CellValue or S.InlineString)))
                throw PackageGuard.Reject("UNSUPPORTED_CELL", "This cell has unsupported metadata.");
            cell.RemoveAllChildren(); cell.DataType = null;
            switch (edit.Kind)
            {
                case "text": cell.DataType = S.CellValues.InlineString; cell.Append(new S.InlineString(new S.Text(value) { Space = SpaceProcessingModeValues.Preserve })); break;
                case "number": cell.Append(new S.CellValue(value)); break;
                case "date":
                    var date = DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue);
                    var serial = date.ToOADate() - ((wb.Workbook.WorkbookProperties?.Date1904?.Value ?? false) ? 1462 : 0);
                    if (serial < 0) throw PackageGuard.Reject("INVALID_VALUE", "Date is before this workbook's date system.");
                    cell.Append(new S.CellValue(serial.ToString("R", CultureInfo.InvariantCulture))); break;
                case "formula": cell.Append(new S.CellFormula(value)); break;
            }
            token.ThrowIfCancellationRequested(); part.Worksheet.Save();
        }
        return (stream.ToArray(), partName, address);
    }

    internal static void ValidateUnchangedParts(byte[] original, byte[] candidate, string partName, string address)
    {
        using var a = new ZipArchive(new MemoryStream(original, false), ZipArchiveMode.Read);
        using var b = new ZipArchive(new MemoryStream(candidate, false), ZipArchiveMode.Read);
        var left = a.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
        var right = b.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
        if (!left.Keys.Order(StringComparer.Ordinal).SequenceEqual(right.Keys.Order(StringComparer.Ordinal))) throw PackageGuard.Reject("CANDIDATE_REJECTED", "Candidate changed workbook package parts.");
        foreach (var name in left.Keys)
        {
            using var first = left[name].Open(); using var second = right[name].Open();
            if (name != partName)
            {
                if (!SHA256.HashData(first).SequenceEqual(SHA256.HashData(second))) throw PackageGuard.Reject("CANDIDATE_REJECTED", "Candidate changed unaffected workbook content.");
                continue;
            }
            var before = XDocument.Load(first); var after = XDocument.Load(second);
            MaskCell(before, address); MaskCell(after, address);
            if (!XNode.DeepEquals(Canonical(before.Root!), Canonical(after.Root!))) throw PackageGuard.Reject("CANDIDATE_REJECTED", "Candidate changed worksheet content outside the selected value.");
        }
    }

    private static void MaskCell(XDocument document, string address)
    {
        var cell = document.Descendants(Spreadsheet + "c").Single(c => (string?)c.Attribute("r") == address);
        cell.Attribute("t")?.Remove(); cell.RemoveNodes();
    }

    private static XElement Canonical(XElement element) => new(element.Name,
        element.Attributes().Where(a => !a.IsNamespaceDeclaration).OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => new XAttribute(a.Name, a.Value)),
        element.Nodes().Select(n => n switch { XElement child => (object)Canonical(child), XText text => new XText(text.Value), XComment comment => new XComment(comment.Value), XProcessingInstruction instruction => new XProcessingInstruction(instruction.Target, instruction.Data), _ => throw PackageGuard.Reject("CANDIDATE_REJECTED", "Worksheet contains unsupported XML nodes.") }));

    private static string ColumnName(int column)
    {
        var result = ""; while (column > 0) { column--; result = (char)('A' + column % 26) + result; column /= 26; } return result;
    }
}
