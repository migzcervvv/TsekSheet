using System.Text;
using System.Xml.Linq;

namespace TsekSheet.ExcelNative.Poc;

public sealed record SemanticDifference(string Category,string Location,string? Before,string? After);
public sealed record SemanticReport(int BeforeCells,int AfterCells,int Differences,IReadOnlyList<SemanticDifference> Changes,
    bool FormulaTextPreserved,bool CachedResultsPreserved,bool WorksheetIdentityPreserved,bool RowLayoutPreserved);

// Read-only diagnostic projection, not a normalization permission or an insertion
// oracle. Complete raw XML/part evidence remains mandatory, including fields below
// not interpreted by this deliberately small projection.
public static class SemanticEvidence
{
    public static SortedDictionary<string,string> Capture(PackageEvidence package)
    {
        XNamespace s=SyntheticFixture.Main;XNamespace r=SyntheticFixture.Office;
        var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
        var wb=package.Documents["xl/workbook.xml"];
        var relationships=package.Documents["xl/_rels/workbook.xml.rels"].Root!.Elements().ToDictionary(e=>(string)e.Attribute("Id")!,e=>(string)e.Attribute("Target")!);
        var strings=package.Documents.TryGetValue("xl/sharedStrings.xml",out var shared)?shared.Root!.Elements(s+"si").Select(e=>Text(e,s)).ToArray():[];
        var styles=package.Documents["xl/styles.xml"].Root!;
        var xfs=styles.Element(s+"cellXfs")!.Elements(s+"xf").ToArray();
        result["defined-names/all"]=string.Join("\n",wb.Descendants(s+"definedName").Select(Canonical));
        result["date-system/1904"]=((string?)wb.Root!.Element(s+"workbookPr")?.Attribute("date1904") is "1" or "true").ToString();
        // Keep explicit/absent calculation flags visible. Do not infer all
        // application defaults or silently approve their deletion.
        result["calculation-settings/raw"]=wb.Root.Element(s+"calcPr") is{} calc?Canonical(calc):"<absent>";
        var index=0;
        foreach(var sheet in wb.Descendants(s+"sheet"))
        {
            var name=(string)sheet.Attribute("name")!;var prefix="sheet/"+(++index)+"/";
            result["identity/"+index]=name+"|"+sheet.Attribute("sheetId")!.Value+"|"+((string?)sheet.Attribute("state")??"visible");
            var uri=new Uri(new Uri("https://package/xl/workbook.xml"),relationships[(string)sheet.Attribute(r+"id")!]);
            var xml=package.Documents[uri.AbsolutePath.TrimStart('/')];
            result["worksheet-target/"+index]=uri.AbsolutePath.TrimStart('/');
            result["dimensions/"+index]=(string?)xml.Root!.Element(s+"dimension")?.Attribute("ref")??"";
            result["print/"+index]=string.Join("\n",xml.Root.Elements().Where(e=>e.Name.LocalName is "pageMargins" or "pageSetup" or "printOptions" or "headerFooter").Select(Canonical));
            result["merges/"+index]=string.Join(";",xml.Descendants(s+"mergeCell").Select(e=>(string?)e.Attribute("ref")));
            result["column-layout/"+index]=xml.Root.Element(s+"cols") is{} cols?Canonical(cols):"";
            result["default-row-layout/"+index]=xml.Root.Element(s+"sheetFormatPr") is{} defaults?Canonical(defaults):"";
            foreach(var row in xml.Descendants(s+"row"))
            {
                var number=(string)row.Attribute("r")!;
                result["row-layout/"+index+"/"+number]=string.Join(";",row.Attributes().Where(a=>!a.IsNamespaceDeclaration&&a.Name.LocalName is not("r" or "spans")).OrderBy(a=>a.Name.ToString(),StringComparer.Ordinal).Select(a=>a.Name+"="+a.Value));
                foreach(var cell in row.Elements(s+"c"))
                {
                    var address=(string)cell.Attribute("r")!;var key=prefix+address;
                    var kind=(string?)cell.Attribute("t")??"n";var value=(string?)cell.Element(s+"v");
                    var formula=cell.Element(s+"f");
                    var literal=kind=="s"?strings[int.Parse(value!,System.Globalization.CultureInfo.InvariantCulture)]:kind=="inlineStr"?Text(cell.Element(s+"is")!,s):value??"<absent>";
                    result["cell/"+key]=(kind is "s" or "inlineStr"?"text":kind)+"|"+literal;
                    if(formula!=null)
                    {
                        result["formula/"+key]=Canonical(formula);
                        result["formula-cache/"+key]=kind+"|"+(value??"<absent>");
                    }
                    var style=int.Parse((string?)cell.Attribute("s")??"0",System.Globalization.CultureInfo.InvariantCulture);
                    if(style<0||style>=xfs.Length)throw new ExperimentException("STYLE_REFERENCE_INVALID");
                    var xf=xfs[style];var styleFacts=new StringBuilder(Canonical(xf));
                    foreach(var resource in new[]{("fontId","fonts"),("fillId","fills"),("borderId","borders")})
                    {
                        var resourceIndex=int.Parse((string?)xf.Attribute(resource.Item1)??"0",System.Globalization.CultureInfo.InvariantCulture);
                        styleFacts.Append('|').Append(resource.Item2).Append('|').Append(Canonical(styles.Element(s+resource.Item2)!.Elements().ElementAt(resourceIndex)));
                    }
                    var format=(string?)xf.Attribute("numFmtId")??"0";
                    styleFacts.Append("|number-format|").Append(styles.Element(s+"numFmts")?.Elements(s+"numFmt").SingleOrDefault(e=>(string?)e.Attribute("numFmtId")==format)?.Attribute("formatCode")?.Value??"builtin:"+format);
                    result["style/"+key]=styleFacts.ToString();
                }
            }
        }
        return result;
    }
    private static string Text(XElement element,XNamespace s)=>string.Concat(element.Descendants(s+"t").Where(e=>!e.Ancestors(s+"rPh").Any()).Select(e=>e.Value));
    public static string Canonical(XElement element)
    {
        var result=new StringBuilder();
        void Append(XElement e)
        {
            result.Append('<').Append(e.Name);
            foreach(var attribute in e.Attributes().Where(a=>!a.IsNamespaceDeclaration).OrderBy(a=>a.Name.ToString(),StringComparer.Ordinal))result.Append('|').Append(attribute.Name).Append(':').Append(attribute.Value.Length).Append(':').Append(attribute.Value);
            result.Append('>');if(!e.HasElements)result.Append(e.Value.Length).Append(':').Append(e.Value);
            foreach(var child in e.Elements())Append(child);result.Append("</").Append(e.Name).Append('>');
        }
        Append(element);return result.ToString();
    }
    public static SemanticReport Compare(PackageEvidence before,PackageEvidence after)
    {
        var old=Capture(before);var current=Capture(after);var changes=new List<SemanticDifference>();
        foreach(var key in old.Keys.Union(current.Keys).Order(StringComparer.Ordinal))
        {
            old.TryGetValue(key,out var a);current.TryGetValue(key,out var b);
            if(a!=b)changes.Add(new(key.Split('/')[0],key,a,b));
        }
        bool Same(string category)=>changes.All(c=>c.Category!=category);
        return new(old.Keys.Count(k=>k.StartsWith("cell/",StringComparison.Ordinal)),current.Keys.Count(k=>k.StartsWith("cell/",StringComparison.Ordinal)),changes.Count,changes,
            Same("formula"),Same("formula-cache"),Same("identity"),Same("row-layout")&&Same("default-row-layout")&&Same("column-layout"));
    }
}
