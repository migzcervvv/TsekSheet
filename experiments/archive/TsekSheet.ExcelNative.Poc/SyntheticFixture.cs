using System.IO.Compression;
using System.Text;

namespace TsekSheet.ExcelNative.Poc;

public static class SyntheticFixture
{
    public const string IncompleteTheme = "incomplete-theme-v1";
    public const string ExplicitTheme = "explicit-theme-v1";
    public const string LiteralFont = "literal-font-v1";
    public static readonly string[] Variants = [IncompleteTheme, ExplicitTheme, LiteralFont];
    public const string Marker = "TSEKSHEET-EXCEL-NATIVE-SYNTHETIC-V1";
    public static readonly string[] SheetNames = ["Unpaid RFPs - SEP", "Unpaid RFPs - AUG", "Unpaid RFPs - JUL", "Unpaid RFPs - JUN", "Unpaid RFPs - MAY"];
    public const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    public const string Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    public const string Relationship = "http://schemas.openxmlformats.org/package/2006/relationships";
    public static void Create(string root, string variant = IncompleteTheme)
    {
        ExperimentPaths.ValidateRoot(root);
        if(!Variants.Contains(variant,StringComparer.Ordinal))throw new ExperimentException("FIXTURE_VARIANT_REJECTED");
        using var output = new FileStream(Path.Combine(root, "baseline.xlsx"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        WriteVariant(output,variant);
    }
    public static string ExpectedSha256(string variant = IncompleteTheme)
    {
        using var output=new MemoryStream();WriteVariant(output,variant);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(output.ToArray()));
    }
    public static void RequireBaselineHash(string hash)
    {
        if(!Variants.Any(v=>string.Equals(hash,ExpectedSha256(v),StringComparison.Ordinal)))throw new ExperimentException("FIXTURE_VERSION_OR_CONTENT_REJECTED");
    }
    private static void WriteVariant(Stream output,string variant)
    {
        if(variant==IncompleteTheme){Write(output);return;}
        if(variant is not(ExplicitTheme or LiteralFont))throw new ExperimentException("FIXTURE_VARIANT_REJECTED");
        using var original=new MemoryStream();Write(original);original.Position=0;
        using var source=new ZipArchive(original,ZipArchiveMode.Read);
        using var target=new ZipArchive(output,ZipArchiveMode.Create,true);
        void Put(string name,string text)
        {
            var entry=target.CreateEntry(name,CompressionLevel.Optimal);entry.LastWriteTime=new DateTimeOffset(2020,1,1,0,0,0,TimeSpan.Zero);
            using var writer=new StreamWriter(entry.Open(),new UTF8Encoding(false));writer.Write(text);
        }
        foreach(var entry in source.Entries)
        {
            using var reader=new StreamReader(entry.Open());var text=reader.ReadToEnd();
            if(variant==ExplicitTheme)
            {
                if(entry.FullName=="[Content_Types].xml")text=text.Replace("</Types>","<Override PartName=\"/xl/theme/theme1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.theme+xml\"/></Types>",StringComparison.Ordinal);
                if(entry.FullName=="xl/_rels/workbook.xml.rels")text=text.Replace("</Relationships>",$"<Relationship Id=\"theme\" Type=\"{Office}/theme\" Target=\"theme/theme1.xml\"/></Relationships>",StringComparison.Ordinal);
            }
            if(variant==LiteralFont&&entry.FullName=="xl/styles.xml")text=text.Replace("<color theme=\"1\"/>","<color rgb=\"FF000000\"/>",StringComparison.Ordinal).Replace("<scheme val=\"minor\"/>","",StringComparison.Ordinal);
            Put(entry.FullName,text);
        }
        if(variant==ExplicitTheme)
        {
            using var resource=typeof(SyntheticFixture).Assembly.GetManifestResourceStream("TsekSheet.ExcelNative.Poc.Fixtures.explicit-calibri-theme.xml")??throw new ExperimentException("THEME_RESOURCE_MISSING");
            using var reader=new StreamReader(resource);Put("xl/theme/theme1.xml",reader.ReadToEnd());
        }
    }
    private static void Write(Stream output)
    {
        using var zip = new ZipArchive(output, ZipArchiveMode.Create,true);
        void Put(string name, string xml)
        {
            var entry = zip.CreateEntry(name, CompressionLevel.Optimal); entry.LastWriteTime = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false)); writer.Write(xml);
        }
        Put("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/docProps/custom.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.custom-properties+xml\"/>" + string.Concat(Enumerable.Range(1,5).Select(i => $"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
        Put("_rels/.rels", $"<Relationships xmlns=\"{Relationship}\"><Relationship Id=\"book\" Type=\"{Office}/officeDocument\" Target=\"xl/workbook.xml\"/><Relationship Id=\"custom\" Type=\"{Office}/custom-properties\" Target=\"docProps/custom.xml\"/></Relationships>");
        Put("docProps/custom.xml", $"<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/custom-properties\" xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"><property fmtid=\"{{D5CDD505-2E9C-101B-9397-08002B2CF9AE}}\" pid=\"2\" name=\"SyntheticOrigin\"><vt:lpwstr>{Marker}</vt:lpwstr></property></Properties>");
        Put("xl/workbook.xml", $"<workbook xmlns=\"{Main}\" xmlns:r=\"{Office}\"><workbookPr date1904=\"0\"/><bookViews><workbookView activeTab=\"0\"/></bookViews><sheets>" + string.Concat(SheetNames.Select((name,i) => $"<sheet name=\"{name}\" sheetId=\"{i+1}\" r:id=\"s{i+1}\"/>")) + "</sheets><calcPr calcId=\"191029\" calcMode=\"manual\" fullCalcOnLoad=\"0\" forceFullCalc=\"0\"/></workbook>");
        Put("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{Relationship}\">" + string.Concat(Enumerable.Range(1,5).Select(i=>$"<Relationship Id=\"s{i}\" Type=\"{Office}/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>")) + $"<Relationship Id=\"styles\" Type=\"{Office}/styles\" Target=\"styles.xml\"/></Relationships>");
        Put("xl/styles.xml", $"<styleSheet xmlns=\"{Main}\"><fonts count=\"1\"><font><sz val=\"11\"/><color theme=\"1\"/><name val=\"Calibri\"/><family val=\"2\"/><scheme val=\"minor\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"2\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"2\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs><cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles></styleSheet>");
        for (var i=1;i<=5;i++)
        {
            var data = i == 1
                ? "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Artificial section</t></is></c></row><row r=\"3\" ht=\"21\" customHeight=\"1\"><c r=\"A3\" t=\"inlineStr\"><is><t>SYNTHETIC-001</t></is></c><c r=\"B3\" s=\"1\"><v>10</v></c></row><row r=\"4\" ht=\"21\" customHeight=\"1\"><c r=\"A4\" t=\"inlineStr\"><is><t>SYNTHETIC-002</t></is></c><c r=\"B4\" s=\"1\"><v>20</v></c><c r=\"C4\" s=\"1\"/></row><row r=\"5\" ht=\"21\" customHeight=\"1\"><c r=\"A5\" t=\"inlineStr\"><is><t>Artificial subtotal</t></is></c><c r=\"B5\" s=\"1\"><f>SUM(B3:B4)</f><v>30</v></c></row><row r=\"7\"><c r=\"A7\" t=\"inlineStr\"><is><t>Unrelated lower content</t></is></c><c r=\"B7\" s=\"1\"><f>B5</f><v>30</v></c></row>"
                : $"<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>Artificial context {i}</t></is></c><c r=\"B1\"><v>{i}</v></c></row>";
            Put($"xl/worksheets/sheet{i}.xml", $"<worksheet xmlns=\"{Main}\"><dimension ref=\"A1:{(i==1?"C7":"B1")}\"/><sheetViews><sheetView workbookViewId=\"0\"/></sheetViews><sheetFormatPr defaultRowHeight=\"15\"/><sheetData>{data}</sheetData><pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");
        }
    }
}
