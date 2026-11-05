using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;

namespace WorkbookLibrary.Evaluation;

public sealed record Snapshot(SortedDictionary<string, string> Values, SortedDictionary<string, string> PartHashes, string[] SchemaErrors);

// Independent of ClosedXML/NPOI: inspect saved ZIP/XML, not their in-memory object models.
public static class PackageInspector
{
    private static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public static string Formula(string formula) => Regex.Replace(formula.TrimStart('='), "'([A-Za-z_][A-Za-z_0-9]*)'!", "$1!");
    public static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    public static string ShiftAddress(string address, int boundary, int count) => Regex.Replace(address, "[0-9]+$", m => (int.Parse(m.Value, CultureInfo.InvariantCulture) >= boundary ? int.Parse(m.Value, CultureInfo.InvariantCulture) + count : int.Parse(m.Value, CultureInfo.InvariantCulture)).ToString(CultureInfo.InvariantCulture));

    public static Snapshot Read(string path)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        using var zip = ZipFile.OpenRead(path);
        XDocument Xml(string name) { using var input = zip.GetEntry(name)!.Open(); return XDocument.Load(input); }
        foreach (var entry in zip.Entries) { using var stream = entry.Open(); hashes[entry.FullName] = Convert.ToHexString(SHA256.HashData(stream)); }
        var workbook = Xml("xl/workbook.xml");
        var relations = Xml("xl/_rels/workbook.xml.rels").Root!.Elements().ToDictionary(e => (string)e.Attribute("Id")!, e => (string)e.Attribute("Target")!);
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? [] : Xml("xl/sharedStrings.xml").Root!.Elements(S + "si").Select(si => string.Concat(si.Descendants(S + "t").Select(t => t.Value))).ToArray();
        var styles = Xml("xl/styles.xml");
        var formats = (styles.Root!.Element(S + "numFmts")?.Elements(S + "numFmt") ?? []).ToDictionary(e => (int)e.Attribute("numFmtId")!, e => (string)e.Attribute("formatCode")!);
        var cellFormats = styles.Root!.Element(S + "cellXfs")!.Elements().ToArray();
        var fonts = styles.Root!.Element(S + "fonts")!.Elements().ToArray();
        var fills = styles.Root!.Element(S + "fills")!.Elements().ToArray();
        string Style(int index)
        {
            var style = cellFormats[index]; var numberId = (int?)style.Attribute("numFmtId") ?? 0;
            var number = formats.GetValueOrDefault(numberId, numberId switch { 0 => "General", 2 => "0.00", _ => "builtin:" + numberId });
            var font = fonts[(int?)style.Attribute("fontId") ?? 0];
            var bold = font.Element(S + "b") != null && (string?)font.Element(S + "b")!.Attribute("val") is not ("0" or "false");
            var pattern = fills[(int?)style.Attribute("fillId") ?? 0].Element(S + "patternFill");
            var foreground = pattern?.Element(S + "fgColor");
            var color = (string?)foreground?.Attribute("rgb") ?? ((string?)foreground?.Attribute("indexed") == "10" ? "FFFF0000" : "");
            return $"number={number};bold={bold};fill={(string?)pattern?.Attribute("patternType") ?? "none"}:{color}";
        }
        var sheetNames = new List<string>();
        foreach (var sheet in workbook.Descendants(S + "sheet"))
        {
            var name = (string)sheet.Attribute("name")!; sheetNames.Add(name);
            values[$"sheet/{name}/state"] = (string?)sheet.Attribute("state") ?? "visible";
            var target = relations[(string)sheet.Attribute(R + "id")!];
            var part = new Uri(new Uri("http://package/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
            var xml = Xml(part);
            foreach (var cell in xml.Descendants(S + "c"))
            {
                var address = (string)cell.Attribute("r")!;
                var formula = cell.Element(S + "f");
                var raw = cell.Element(S + "v")?.Value ?? "";
                var type = (string?)cell.Attribute("t");
                var text = type == "s" ? strings[int.Parse(raw, CultureInfo.InvariantCulture)] : type == "inlineStr" ? string.Concat(cell.Descendants(S + "t").Select(t => t.Value)) : raw;
                if (formula != null) values[$"cell/{name}/{address}/content"] = "formula:" + Formula(formula.Value);
                else if (text.Length > 0) values[$"cell/{name}/{address}/content"] = type is "s" or "inlineStr" or "str" ? "text:" + text : "number:" + (double.TryParse(text, CultureInfo.InvariantCulture, out var n) ? n.ToString("G17", CultureInfo.InvariantCulture) : text);
                if (text.Length > 0 || formula != null) values[$"cell/{name}/{address}/style"] = Style((int?)cell.Attribute("s") ?? 0);
            }
            values[$"sheet/{name}/merges"] = string.Join(";", xml.Descendants(S + "mergeCell").Select(e => (string)e.Attribute("ref")!).Order());
            foreach (var row in xml.Descendants(S + "row").Where(e => e.Attribute("ht") != null)) values[$"height/{name}/{(string)row.Attribute("r")!}"] = (string)row.Attribute("ht")!;
            foreach (var column in xml.Descendants(S + "col"))
                for (var i = (int)column.Attribute("min")!; i <= Math.Min((int)column.Attribute("max")!, 4); i++)
                    values[$"column/{name}/{i}"] = (string?)column.Attribute("width") ?? "";
            var relName = part[..part.LastIndexOf('/')] + "/_rels/" + part[(part.LastIndexOf('/') + 1)..] + ".rels";
            if (zip.GetEntry(relName) != null)
                foreach (var relation in Xml(relName).Root!.Elements().Where(e => ((string?)e.Attribute("Type"))?.EndsWith("/table") == true))
                {
                    var tablePath = new Uri(new Uri("http://package/" + part), (string)relation.Attribute("Target")!).AbsolutePath.TrimStart('/');
                    var table = Xml(tablePath).Root!;
                    values[$"table/{name}/{(string)table.Attribute("name")!}"] = (string)table.Attribute("ref")!;
                }
        }
        values["workbook/sheetOrder"] = string.Join(";", sheetNames);
        values["workbook/date1904"] = (string?)workbook.Root!.Element(S + "workbookPr")?.Attribute("date1904") is "1" or "true" ? "true" : "false";
        foreach (var name in workbook.Descendants(S + "definedName")) values[$"name/{(string)name.Attribute("name")!}/{(string?)name.Attribute("localSheetId") ?? "global"}"] = Formula(name.Value);
        if (zip.GetEntry("docProps/core.xml") != null)
            foreach (var element in Xml("docProps/core.xml").Root!.Elements().Where(e => e.Name.LocalName is "creator" or "title")) values["metadata/" + element.Name.LocalName] = element.Value;
        if (zip.GetEntry("docProps/custom.xml") != null)
            foreach (var property in Xml("docProps/custom.xml").Root!.Elements()) values["custom/" + (string)property.Attribute("name")!] = property.Value;
        values["opaque/present"] = zip.Entries.Where(e => e.FullName.Contains("customXml/") && e.FullName.EndsWith(".xml")).Any(e => { using var reader = new StreamReader(e.Open()); return reader.ReadToEnd().Contains("preserve-opaque-payload"); }).ToString();
        using var document = SpreadsheetDocument.Open(path, false);
        var errors = new OpenXmlValidator().Validate(document).Select(e => $"{e.Part?.Uri}: {e.Description}").ToArray();
        return new(values, hashes, errors);
    }
}
