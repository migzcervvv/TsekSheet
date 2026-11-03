using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace TsekSheet.ExcelNative.Poc;

public sealed record PartDifference(string Part, string Kind, string? BeforeSha256, string? AfterSha256);
public sealed record XmlDifference(string Part, string Location, string? Before, string? After);
public sealed record ComparisonReport(bool Qualified, bool InsertionAllowed, int PartDifferences, int XmlDifferences,
    IReadOnlyList<PartDifference> Parts, IReadOnlyList<XmlDifference> Changes, string Policy);

// Independent ZIP/XML evidence; no COM/library edit object or formula translator.
public sealed class PackageEvidence
{
    public Dictionary<string, byte[]> Parts { get; } = new(StringComparer.Ordinal);
    public Dictionary<string, XDocument> Documents { get; } = new(StringComparer.Ordinal);
    public static PackageEvidence Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > 4 * 1024 * 1024) throw new ExperimentException("PACKAGE_FILE_LIMIT");
        var tail = new byte[(int)Math.Min(file.Length,65557)]; file.Position = file.Length-tail.Length; file.ReadExactly(tail);
        var end = -1;
        for(var i=tail.Length-22;i>=0;i--)
            if(BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i))==0x06054b50 && i+22+BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i+20))==tail.Length){end=i;break;}
        if(end<0 || BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end+4))!=0 || BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end+6))!=0
            || BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end+8))!=BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end+10))
            || BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(end+10))>128) throw new ExperimentException("PACKAGE_DIRECTORY_LIMIT");
        file.Position=0; using var zip = new ZipArchive(file, ZipArchiveMode.Read);
        if(zip.Entries.Count>128 || zip.Entries.Sum(e=>e.Length)>16*1024*1024)throw new ExperimentException("PACKAGE_EXPANSION_LIMIT");
        var result = new PackageEvidence(); var elements=0; var cells=0; long expanded=0;
        var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var entry in zip.Entries)
        {
            var name=entry.FullName; var lower=name.ToLowerInvariant();
            if(name.Length>512 || name.Contains('%')||name.Contains('\\')||name.Contains(':')||name.StartsWith('/')||name.Split('/').Any(p=>p is "." or "..")||!names.Add(name))throw new ExperimentException("PACKAGE_PATH_REJECTED");
            if(entry.Length>2*1024*1024 || !(lower.EndsWith(".xml")||lower.EndsWith(".rels")) || lower.Contains("vba")||lower.Contains("macro")||lower.Contains("activex")||lower.Contains("embedding")||lower.StartsWith("_xmlsignatures/"))throw new ExperimentException("ACTIVE_OR_UNQUALIFIED_PART");
            using var content=entry.Open(); using var copy=new MemoryStream(); var buffer=new byte[8192]; int count;
            while((count=content.Read(buffer))>0){expanded+=count;if(copy.Length+count>2*1024*1024||expanded>16*1024*1024)throw new ExperimentException("ACTUAL_EXPANSION_LIMIT");copy.Write(buffer,0,count);}
            if(copy.Length!=entry.Length)throw new ExperimentException("PACKAGE_SIZE_MISMATCH");
            var bytes=copy.ToArray(); result.Parts.Add(name,bytes);
            var settings=new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=2*1024*1024 };
            using(var check=XmlReader.Create(new MemoryStream(bytes,false),settings))
                while(check.Read())
                {
                    if(check.Depth>64 || check.NodeType==XmlNodeType.Element&&++elements>200000)throw new ExperimentException("XML_LIMIT");
                    if(lower.StartsWith("xl/worksheets/",StringComparison.Ordinal)&&check.NodeType==XmlNodeType.Element&&check.LocalName=="c"&&check.NamespaceURI==SyntheticFixture.Main&&++cells>10000)throw new ExperimentException("CELL_LIMIT");
                }
            using var reader=XmlReader.Create(new MemoryStream(bytes,false),settings);result.Documents.Add(name,XDocument.Load(reader));
        }
        foreach(var (name,document) in result.Documents.Where(p=>p.Key.EndsWith(".rels",StringComparison.Ordinal)))
        {
            if(document.Root?.Name!=XName.Get("Relationships",SyntheticFixture.Relationship))throw new ExperimentException("RELATIONSHIPS_INVALID");
            var ids=new HashSet<string>(StringComparer.Ordinal);
            var source=name=="_rels/.rels"?"":name.Replace("/_rels/","/")[..^5];
            foreach(var relationship in document.Root.Elements())
            {
                var target=(string?)relationship.Attribute("Target")??"";var id=(string?)relationship.Attribute("Id")??"";
                if(!ids.Add(id)||id.Length==0||relationship.Attribute("TargetMode")?.Value=="External"||target.Contains(':')||target.Contains('%')||target.Contains('\\'))throw new ExperimentException("EXTERNAL_OR_UNQUALIFIED_RELATIONSHIP");
                var uri=new Uri(new Uri("https://package/"+source),target);
                if(uri.Host!="package"||uri.Query.Length>0||uri.Fragment.Length>0||!result.Parts.ContainsKey(uri.AbsolutePath.TrimStart('/')))throw new ExperimentException("BROKEN_RELATIONSHIP");
            }
        }
        return result;
    }
    public void RequireSynthetic()
    {
        XNamespace custom="http://schemas.openxmlformats.org/officeDocument/2006/custom-properties";
        if(!Documents.TryGetValue("docProps/custom.xml",out var properties)||properties.Root?.Elements(custom+"property").SingleOrDefault(p=>(string?)p.Attribute("name")=="SyntheticOrigin")?.Value!=SyntheticFixture.Marker)throw new ExperimentException("SYNTHETIC_ORIGIN_REQUIRED");
        XNamespace s=SyntheticFixture.Main;
        if(!Documents.TryGetValue("xl/workbook.xml",out var workbook)||!workbook.Descendants(s+"sheet").Select(e=>(string?)e.Attribute("name")).SequenceEqual(SyntheticFixture.SheetNames))throw new ExperimentException("FIVE_SHEET_FIXTURE_REQUIRED");
        var types=Documents["[Content_Types].xml"].Descendants().Attributes("ContentType").Select(a=>a.Value);
        if(types.Any(t=>t.Contains("macro",StringComparison.OrdinalIgnoreCase)||t.Contains("active",StringComparison.OrdinalIgnoreCase)))throw new ExperimentException("ACTIVE_CONTENT_TYPE");
    }
    public SortedDictionary<string,string> Facts(string part)
    {
        var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
        void Walk(XElement element,string path)
        {
            foreach(var attribute in element.Attributes().Where(a=>!a.IsNamespaceDeclaration))result[path+"/@"+attribute.Name]=attribute.Value;
            if(!element.HasElements)result[path+"/#text"]=element.Value;
            var ordinal=new Dictionary<XName,int>();
            foreach(var child in element.Elements()){var index=ordinal.GetValueOrDefault(child.Name)+1;ordinal[child.Name]=index;Walk(child,path+"/"+child.Name+"["+index+"]");}
        }
        Walk(Documents[part].Root!,"/"+Documents[part].Root!.Name+"[1]"); return result;
    }
    public static ComparisonReport Compare(PackageEvidence before,PackageEvidence after)
    {
        var parts=new List<PartDifference>();var changes=new List<XmlDifference>();
        static string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
        foreach(var name in before.Parts.Keys.Union(after.Parts.Keys).Order(StringComparer.Ordinal))
        {
            before.Parts.TryGetValue(name,out var old);after.Parts.TryGetValue(name,out var current);
            if(old!=null&&current!=null&&old.AsSpan().SequenceEqual(current))continue;
            parts.Add(new(name,old==null?"added":current==null?"removed":"changed",old==null?null:Hash(old),current==null?null:Hash(current)));
            var oldFacts=old==null?new SortedDictionary<string,string>(StringComparer.Ordinal):before.Facts(name);
            var newFacts=current==null?new SortedDictionary<string,string>(StringComparer.Ordinal):after.Facts(name);
            foreach(var key in oldFacts.Keys.Union(newFacts.Keys).Order(StringComparer.Ordinal))
            {
                oldFacts.TryGetValue(key,out var a);newFacts.TryGetValue(key,out var b);
                if(a!=b)changes.Add(new(name,key,a,b));
                if(changes.Count>2000)throw new ExperimentException("COMPARISON_REPORT_LIMIT");
            }
        }
        // Deliberately no blanket Excel normalization exemption. A finite reviewed
        // expected-difference policy must precede any qualification/insertion.
        return new(parts.Count==0,false,parts.Count,changes.Count,parts,changes,"strict-v1; every changed/added/removed part requires explicit review; insertion unavailable");
    }
}
