using System.IO;
using System.IO.Compression;
using System.Text;

namespace TsekSheet.TestFixtures;

// Entirely artificial accounting-shaped packages. Never derived from private inputs.
public sealed class AccountingCompatibilityFixture : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-accounting-" + Guid.NewGuid().ToString("N") + ".xlsx");
    public AccountingCompatibilityFixture(string feature)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
        using var zip = ZipFile.Open(Path, ZipArchiveMode.Create);
        void Put(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml); }
        var sheets = feature == "sheets" ? 33 : 2;
        var extraPart = feature switch { "image" => "xl/media/image.png", "vml" => "xl/drawings/comments.vml", "printer" => "xl/printerSettings/printerSettings1.bin", "externalLink" => "xl/externalLinks/externalLink1.xml", _ => null };
        Put("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" + string.Concat(Enumerable.Range(1, sheets).Select(i => $"<Override PartName=\"/xl/worksheets/s{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
        Put("_rels/.rels", feature == "broken" ? "<broken" : $"<Relationships xmlns=\"{rel}\"><Relationship Id=\"root\" Type=\"{office}officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Put("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"{office.TrimEnd('/')}\"><sheets>" + string.Concat(Enumerable.Range(1, sheets).Select(i => $"<sheet name=\"Ledger {i}\" sheetId=\"{i}\" r:id=\"r{i}\"{(i == sheets ? " state=\"hidden\"" : "")}/>")) + "</sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{rel}\">" + string.Concat(Enumerable.Range(1, sheets).Select(i => $"<Relationship Id=\"r{i}\" Type=\"{office}worksheet\" Target=\"worksheets/s{i}.xml\"/>")) + (feature == "external" ? $"<Relationship Id=\"external\" Type=\"{office}externalLink\" Target=\"file:///NEVER-FOLLOW.xlsx\" TargetMode=\"External\"/>" : "") + "</Relationships>");
        for (var i = 1; i <= sheets; i++)
        {
            var rows = new StringBuilder();
            var count = feature == "cells" && i == 1 ? 50001 : 1;
            for (var row = 1; row <= count; row++) rows.Append($"<row r=\"{row}\"><c r=\"A{row}\"><v>{row}</v></c></row>");
            var suffix = feature == "protection" ? "<sheetProtection sheet=\"1\"/>" : feature == "advanced" ? "<conditionalFormatting sqref=\"A1\"><cfRule type=\"expression\" priority=\"1\"><formula>A1&gt;0</formula></cfRule></conditionalFormatting>" : "";
            Put($"xl/worksheets/s{i}.xml", feature == "badSheet" && i == 2 ? "<broken" : $"<worksheet xmlns=\"{ns}\"><sheetData>{rows}</sheetData>{suffix}</worksheet>");
        }
        if (extraPart != null) Put(extraPart, feature == "externalLink" ? $"<externalLink xmlns=\"{ns}\"/>" : "synthetic opaque bytes; never rendered");
    }
    public void Dispose() => File.Delete(Path);
}
