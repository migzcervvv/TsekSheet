using System.IO;
using System.IO.Compression;

namespace TsekSheet.TestFixtures;

// Artificial values only; literal OOXML expectations independent of the inspector.
public sealed class InspectionFixture : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-interpretation-" + Guid.NewGuid().ToString("N") + ".xlsx");
    public InspectionFixture()
    {
        using var zip = ZipFile.Open(Path, ZipArchiveMode.Create);
        void Put(string name, string xml) { using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml); }
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string rel = "http://schemas.openxmlformats.org/package/2006/relationships";
        const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        Put("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
        Put("_rels/.rels", $"<Relationships xmlns=\"{rel}\"><Relationship Id=\"root\" Type=\"{office}/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Put("xl/workbook.xml", $"<workbook xmlns=\"{ns}\" xmlns:r=\"{office}\"><sheets><sheet name=\"Dates\" sheetId=\"1\" r:id=\"s1\"/><sheet name=\"Hidden context\" sheetId=\"2\" r:id=\"s2\" state=\"hidden\"/></sheets></workbook>");
        Put("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{rel}\"><Relationship Id=\"s1\" Type=\"{office}/worksheet\" Target=\"worksheets/s1.xml\"/><Relationship Id=\"s2\" Type=\"{office}/worksheet\" Target=\"worksheets/s2.xml\"/><Relationship Id=\"styles\" Type=\"{office}/styles\" Target=\"styles.xml\"/></Relationships>");
        Put("xl/styles.xml", $"<styleSheet xmlns=\"{ns}\"><numFmts><numFmt numFmtId=\"164\" formatCode=\"[Red]yyyy-mm-dd\"/></numFmts><cellXfs><xf numFmtId=\"14\"/><xf numFmtId=\"164\"/><xf numFmtId=\"0\"/></cellXfs></styleSheet>");
        var sheet = $"<worksheet xmlns=\"{ns}\"><sheetData><row r=\"1\"><c r=\"A1\" s=\"0\"><v>61</v></c><c r=\"B1\" s=\"2\"><f t=\"shared\" si=\"0\" ref=\"B1:B2\">A1+$A$1</f><v>122</v></c><c r=\"C1\" s=\"1\"><v>61</v></c></row><row r=\"2\"><c r=\"B2\" s=\"2\"><f t=\"shared\" si=\"0\"/><v>123</v></c></row><row r=\"900000\"><c r=\"A900000\" s=\"0\"><v>61</v></c></row></sheetData></worksheet>";
        Put("xl/worksheets/s1.xml", sheet); Put("xl/worksheets/s2.xml", sheet);
    }
    public void Dispose() => File.Delete(Path);
}
