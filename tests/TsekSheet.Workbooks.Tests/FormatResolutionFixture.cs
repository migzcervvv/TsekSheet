using System.IO.Compression;
using System.Xml.Linq;
using ClosedXML.Excel;

namespace TsekSheet.Workbooks.Tests;

internal sealed record FormatResolutionCase(string Id, int FormatId, string? Code, string Raw, string? ExpectedDisplay,
    int Parent = 0, int BaseFormat = 0, string? Apply = null, string? DuplicateCode = null, bool Missing = false);
internal static class FormatResolutionFixture
{
    // Artificial values and literal expectations, independent of formatting implementation/native output.
    internal static readonly FormatResolutionCase[] Cases = [
        new("builtin-two", 2, null, "2468.5", "2468.50"),
        new("low-two-agreement", 2, "0.00", "2468.5", "2468.50"),
        new("low-two-conflict", 2, "0.000", "2468.5", null),
        new("low-forty-three", 43, "0.00", "2468.5", null),
        new("custom-accounting", 164, "#,##0.00;(#,##0.00)", "-2468.5", "(2,468.50)"),
        new("custom-currency", 164, "\"$\"#,##0.00", "2468.5", "$2,468.50"),
        new("custom-percent", 165, "0.00%", "0.125", "12.50%"),
        new("custom-date", 164, "yyyy-mm-dd", "61", "1900-03-01"),
        new("custom-time", 164, "hh:mm:ss", "0.25", "06:00:00"),
        new("inherit-agreement", 164, "0.00", "2468.5", "2468.50", 1, 164),
        new("inherit-disabled-agreement", 164, "0.00", "2468.5", "2468.50", 1, 164, "0"),
        new("inherit-conflict", 164, "0.00", "2468.5", null, 1, 0, "0"),
        new("explicit-override", 164, "0.00", "2468.5", "2468.50", 1, 0, "1"),
        new("missing-parent", 164, "0.00", "2468.5", null, 9, 0, "1"),
        new("missing-custom", 200, null, "2468.5", null, Missing:true),
        new("duplicate-same", 164, "0.00", "2468.5", null, DuplicateCode:"0.00"),
        new("duplicate-conflict", 164, "0.00", "2468.5", null, DuplicateCode:"0.000"),
        new("locale-expression", 164, "[$-409]0.00", "2468.5", null),
        new("over-precision", 164, "0.00", "1.234567890123456", null),
    ];
    internal static void Create(string path, FormatResolutionCase spec)
    {
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("FormatProbe"); sheet.Cell("A1").Value = 2468.5; sheet.Column(1).Width = 48;
            using var file = new FileStream(path, FileMode.CreateNew); workbook.SaveAs(file);
        }
        XNamespace s = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        XDocument Read(string part) { using var stream = zip.GetEntry(part)!.Open(); return XDocument.Load(stream); }
        void Write(string part, XDocument doc) { zip.GetEntry(part)!.Delete(); using var stream = zip.CreateEntry(part).Open(); doc.Save(stream, System.Xml.Linq.SaveOptions.DisableFormatting); }
        var styles = Read("xl/styles.xml");
        var definitions = new XElement(s + "numFmts");
        if (spec.Code != null && !spec.Missing) definitions.Add(new XElement(s + "numFmt", new XAttribute("numFmtId", spec.FormatId), new XAttribute("formatCode", spec.Code)));
        if (spec.DuplicateCode != null) definitions.Add(new XElement(s + "numFmt", new XAttribute("numFmtId", spec.FormatId), new XAttribute("formatCode", spec.DuplicateCode)));
        definitions.SetAttributeValue("count", definitions.Elements().Count());
        styles.Root!.Element(s + "numFmts")?.Remove(); styles.Root.AddFirst(definitions);
        XElement Xf(int fmt) => new(s + "xf", new XAttribute("numFmtId", fmt), new XAttribute("fontId", 0), new XAttribute("fillId", 0), new XAttribute("borderId", 0));
        styles.Root.Element(s + "cellStyleXfs")!.ReplaceWith(new XElement(s + "cellStyleXfs", new XAttribute("count", 2), Xf(0), Xf(spec.BaseFormat)));
        var cell = Xf(spec.FormatId); cell.SetAttributeValue("xfId", spec.Parent); if (spec.Apply != null) cell.SetAttributeValue("applyNumberFormat", spec.Apply);
        var normal = Xf(0); normal.SetAttributeValue("xfId", 0);
        styles.Root.Element(s + "cellXfs")!.ReplaceWith(new XElement(s + "cellXfs", new XAttribute("count", 2), normal, cell));
        Write("xl/styles.xml", styles);
        var sheetDoc = Read("xl/worksheets/sheet1.xml"); var a1 = sheetDoc.Descendants(s + "c").Single(c => (string?)c.Attribute("r") == "A1");
        a1.SetAttributeValue("s", 1); a1.Attribute("t")?.Remove(); a1.ReplaceNodes(new XElement(s + "v", spec.Raw)); Write("xl/worksheets/sheet1.xml", sheetDoc);
    }
}
