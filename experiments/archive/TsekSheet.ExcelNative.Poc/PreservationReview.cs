using System.Xml.Linq;

namespace TsekSheet.ExcelNative.Poc;

public sealed record FontIdentity(int Index,string Declared,string Scheme,string EffectiveLatin,string Color,string Size,string Definition);
public sealed record PreservationFinding(string Classification,string Code,string Location,string Explanation);
public sealed record PreservationReviewReport(string Policy,bool Qualified,bool InsertionAuthorized,
    IReadOnlyList<FontIdentity> BeforeFonts,IReadOnlyList<FontIdentity> AfterFonts,
    IReadOnlyList<PreservationFinding> Findings,int AcceptedSerialization,int RequiresReview,int AlwaysFail,
    bool? NativeReadbackEqual,string VisualVerification);

// Additional EXPERIMENTAL review only. The exact package comparator remains
// strict and the production oracle is neither referenced nor changed.
public static class PreservationReview
{
    private static readonly XNamespace S=SyntheticFixture.Main;
    private static readonly XNamespace A="http://schemas.openxmlformats.org/drawingml/2006/main";
    public static IReadOnlyList<FontIdentity> Fonts(PackageEvidence package)
    {
        var theme=Theme(package);var scheme=theme?.Root?.Element(A+"themeElements")?.Element(A+"fontScheme");
        return package.Documents["xl/styles.xml"].Root!.Element(S+"fonts")!.Elements(S+"font").Select((font,index)=>
        {
            string Value(string name)=>font.Element(S+name)?.Attribute("val")?.Value??"";
            var reference=Value("scheme");var literal=Value("name");
            var effective=reference is "major" or "minor"?scheme?.Element(A+(reference+"Font"))?.Element(A+"latin")?.Attribute("typeface")?.Value??"<unresolved-theme-font>":literal;
            var color=font.Element(S+"color");string resolved="<unspecified>";
            if(color?.Attribute("rgb") is{} rgb)resolved=rgb.Value;
            else if(color?.Attribute("theme") is{} themeColor)
            {
                var names=new[]{"lt1","dk1","lt2","dk2","accent1","accent2","accent3","accent4","accent5","accent6","hlink","folHlink"};
                if(!int.TryParse(themeColor.Value,out var number)||number<0||number>=names.Length)throw new ExperimentException("FONT_THEME_COLOR_INVALID");
                var definition=theme?.Root?.Element(A+"themeElements")?.Element(A+"clrScheme")?.Element(A+names[number])?.Elements().SingleOrDefault();
                // System colors are environmental. lastClr is a fallback, not a
                // promise that another Windows theme renders that RGB value.
                resolved=definition?.Name.LocalName switch
                {
                    "srgbClr"=>"FF"+(string?)definition.Attribute("val"),
                    "sysClr"=>"system:"+(string?)definition.Attribute("val")+";fallback:"+(string?)definition.Attribute("lastClr"),
                    _=>"<unresolved-theme-color>"
                };
                if(color.Attribute("tint") is{} tint)resolved+=";tint:"+tint.Value;
            }
            else if(color?.Attribute("indexed") is{} indexed)resolved="indexed:"+indexed.Value;
            return new FontIdentity(index,literal,reference,effective,resolved,Value("sz"),SemanticEvidence.Canonical(font));
        }).ToArray();
    }
    public static XDocument? Theme(PackageEvidence package)
    {
        var relations=package.Documents["xl/_rels/workbook.xml.rels"].Root!.Elements().Where(e=>(string?)e.Attribute("Type")==SyntheticFixture.Office+"/theme").ToArray();
        if(relations.Length>1)throw new ExperimentException("MULTIPLE_THEMES_REJECTED");
        if(relations.Length==0)return null;
        var uri=new Uri(new Uri("https://package/xl/workbook.xml"),(string)relations[0].Attribute("Target")!);
        return package.Documents[uri.AbsolutePath.TrimStart('/')];
    }
    public static SortedSet<string> Relationships(PackageEvidence package)
    {
        var result=new SortedSet<string>(StringComparer.Ordinal);
        foreach(var (part,doc) in package.Documents.Where(p=>p.Key.EndsWith(".rels",StringComparison.Ordinal)))
        {
            var source=part=="_rels/.rels"?"":part.Replace("/_rels/","/",StringComparison.Ordinal)[..^5];
            foreach(var rel in doc.Root!.Elements())
            {
                var target=new Uri(new Uri("https://package/"+source),(string)rel.Attribute("Target")!);
                result.Add(source+"|"+(string?)rel.Attribute("Type")+"|"+target.AbsolutePath.TrimStart('/'));
            }
        }
        return result;
    }
    public static PreservationReviewReport Compare(PackageEvidence before,PackageEvidence after,NativeAppearanceReport? nativeBefore=null,NativeAppearanceReport? nativeAfter=null)
    {
        var raw=PackageEvidence.Compare(before,after);var semantic=SemanticEvidence.Compare(before,after);
        var oldFonts=Fonts(before);var fonts=Fonts(after);var findings=new List<PreservationFinding>();
        void Add(string kind,string code,string location,string reason)=>findings.Add(new(kind,code,location,reason));
        if(oldFonts.Any(f=>f.EffectiveLatin.Contains("<unresolved",StringComparison.Ordinal)||f.Color.Contains("<unresolved",StringComparison.Ordinal)))
            Add("always-fail","SOURCE_THEME_UNRESOLVED","xl/styles.xml","The source does not define its effective theme font/color; no stable appearance baseline exists.");
        if(oldFonts.Count!=fonts.Count)Add("always-fail","FONT_TABLE_CHANGED","xl/styles.xml","Font resource count changed.");
        foreach(var pair in oldFonts.Zip(fonts))
        {
            if(pair.First.Declared!=pair.Second.Declared||pair.First.Scheme!=pair.Second.Scheme||pair.First.EffectiveLatin!=pair.Second.EffectiveLatin||pair.First.Color!=pair.Second.Color||pair.First.Size!=pair.Second.Size||pair.First.Definition!=pair.Second.Definition)
                Add("always-fail","FONT_IDENTITY_OR_THEME_CHANGED","font/"+pair.First.Index,"Declared font, scheme, resolved Latin font, color, size or font definition changed.");
        }
        var oldRelations=Relationships(before);var relations=Relationships(after);
        foreach(var relation in oldRelations.Except(relations))Add("always-fail","RELATIONSHIP_LOST",relation,"An existing dependency edge disappeared; relationship ID renumbering does not remove edges.");
        foreach(var relation in relations.Except(oldRelations))Add("requires-review","RELATIONSHIP_ADDED",relation,"Resolve and justify this added package dependency; presence is not automatic permission.");
        foreach(var difference in semantic.Changes)
        {
            var layout=difference.Category is "row-layout" or "default-row-layout" or "column-layout";
            var descent=layout&&RemoveDescent(difference.Before)==RemoveDescent(difference.After);
            var calculation=difference.Category=="calculation-settings"&&OnlyFalseCalculationFlagsOmitted(before,after);
            Add(descent||calculation?"requires-review":"always-fail",descent?"ROW_DESCENT_VISUAL_REVIEW":calculation?"CALCULATION_SETTINGS_REVIEW":"SEMANTIC_CHANGE",difference.Location,
                descent?"x14ac:dyDescent specifies baseline distance and can set customHeight true even when false. Font geometry readback does not prove glyph placement, clipping or later auto-height behavior.":calculation?"Explicit/absent calculation flags differ. Compare documented defaults and future calculation behavior; unchanged formula caches are insufficient.":"Value/formula/cache/style/name/reference/identity/dimension or explicit layout differs.");
        }
        // Precisely demonstrated inline/shared-string equivalence, with separate
        // raw evidence and independent style/identity checks. Not a part exemption.
        if(before.Documents.Values.Any(d=>d.Descendants(S+"c").Any(c=>(string?)c.Attribute("t")=="inlineStr"))&&after.Documents.ContainsKey("xl/sharedStrings.xml")&&
            !semantic.Changes.Any(c=>c.Category=="cell")&&PlainStrings(before)&&PlainStrings(after))
            Add("accepted-serialization","STRING_ENCODING_EQUIVALENT","xl/sharedStrings.xml","Decoded text and every stored cell address/type/value match; inline text was serialized through a shared string table. Other changes in these parts remain reviewed independently.");
        foreach(var part in raw.Parts)
        {
            var deltas=raw.Changes.Where(c=>c.Part==part.Part).ToArray();
            if(part.Kind=="changed")
            {
                static IEnumerable<string> Annotations(XDocument document)=>document.DescendantNodes().Where(n=>n is XComment or XProcessingInstruction).Select(n=>n.ToString());
                var prior=Annotations(before.Documents[part.Part]).GroupBy(n=>n).ToDictionary(g=>g.Key,g=>g.Count());
                var next=Annotations(after.Documents[part.Part]).GroupBy(n=>n).ToDictionary(g=>g.Key,g=>g.Count());
                if(prior.Any(p=>next.GetValueOrDefault(p.Key)<p.Value))Add("always-fail","XML_ANNOTATION_LOST",part.Part,"An unrelated XML comment or processing instruction was lost; XML fact equivalence does not waive it.");
            }
            if(part.Kind=="removed")Add("always-fail","PACKAGE_PART_LOST",part.Part,"An unrelated package part was removed.");
            else if(part.Kind=="changed"&&deltas.Length==0&&SerializationEquivalent(before.Documents[part.Part],after.Documents[part.Part]))
                Add("accepted-serialization","XML_SERIALIZATION_ONLY",part.Part,"Expanded names, ordered elements, text/attributes and existing namespace bindings match; no comments, processing instructions or mixed content are waived. Raw encoding/attribute-order differences remain reported.");
            else
                Add("requires-review","RAW_PART_DIFFERENCE",part.Part,"Complete raw/XML delta requires a finite reviewed contract; no whole-part normalization exception is granted.");
        }
        bool? nativeEqual=null;
        if(nativeBefore!=null&&nativeAfter!=null)
        {
            nativeEqual=nativeBefore.Sheets.SequenceEqual(nativeAfter.Sheets)&&nativeBefore.Cells.SequenceEqual(nativeAfter.Cells);
            if(nativeEqual!=true)Add("always-fail","NATIVE_APPEARANCE_CHANGED","native-readback","Font or measured cell/row/column geometry changed after native save/reopen.");
            if((nativeBefore.OutstandingRcwCounts?.Count??0)>0||(nativeAfter.OutstandingRcwCounts?.Count??0)>0)
                Add("always-fail","NATIVE_RCW_UNRELEASED","native-readback","Readback reported outstanding acquired COM references.");
        }
        var accepted=findings.Count(f=>f.Classification=="accepted-serialization");var review=findings.Count(f=>f.Classification=="requires-review");var failed=findings.Count(f=>f.Classification=="always-fail");
        return new("experimental-review-v3; raw strict oracle unchanged",review==0&&failed==0,false,oldFonts,fonts,findings,accepted,review,failed,nativeEqual,
            "Independent rendered appearance not qualified; native readback is supporting evidence only.");
    }
    private static bool PlainStrings(PackageEvidence package)=>package.Documents.Values.SelectMany(d=>d.Descendants().Where(e=>e.Name==S+"is"||e.Name==S+"si"))
        .All(e=>e.Elements().All(c=>c.Name==S+"t"&&!c.HasElements));
    private static string? RemoveDescent(string? value)
    {
        if(value==null)return null;
        return System.Text.RegularExpressions.Regex.Replace(value,@"(?:\||;)?\{http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac\}dyDescent(?:=|:\d+:)[^;>\|]+;?","").TrimEnd(';');
    }
    private static bool OnlyFalseCalculationFlagsOmitted(PackageEvidence before,PackageEvidence after)
    {
        var old=before.Documents["xl/workbook.xml"].Root!.Element(S+"calcPr");var current=after.Documents["xl/workbook.xml"].Root!.Element(S+"calcPr");
        if(old==null||current==null)return false;
        var normalized=new XElement(old);
        foreach(var flag in new[]{"fullCalcOnLoad","forceFullCalc"})
            if((string?)normalized.Attribute(flag) is "0" or "false"&&current.Attribute(flag)==null)normalized.Attribute(flag)!.Remove();
        return SemanticEvidence.Canonical(normalized)==SemanticEvidence.Canonical(current);
    }
    private static bool SerializationEquivalent(XDocument before,XDocument after)
    {
        if(SemanticEvidence.Canonical(before.Root!)!=SemanticEvidence.Canonical(after.Root!))return false;
        var old=before.Root!.DescendantsAndSelf().ToArray();var current=after.Root!.DescendantsAndSelf().ToArray();
        bool Unsupported(XDocument document)=>document.DescendantNodes().Any(n=>n is XComment or XProcessingInstruction)||
            document.Descendants().Any(e=>e.HasElements&&e.Nodes().OfType<XText>().Any(t=>!string.IsNullOrWhiteSpace(t.Value)));
        if(Unsupported(before)||Unsupported(after)||old.Length!=current.Length)return false;
        var prefixes=old.SelectMany(e=>e.Attributes().Where(a=>a.IsNamespaceDeclaration)).Select(a=>a.Name.LocalName=="xmlns"?"":a.Name.LocalName).Distinct().ToArray();
        for(var i=0;i<old.Length;i++)foreach(var prefix in prefixes)
            if(prefix.Length==0?old[i].GetDefaultNamespace()!=current[i].GetDefaultNamespace():old[i].GetNamespaceOfPrefix(prefix)!=current[i].GetNamespaceOfPrefix(prefix))return false;
        var added=current.SelectMany(e=>e.Attributes().Where(a=>a.IsNamespaceDeclaration)).Select(a=>a.Name.LocalName).Except(prefixes);
        foreach(var prefix in added)
            if(current.SelectMany(e=>e.Attributes().Where(a=>!a.IsNamespaceDeclaration)).Any(a=>System.Text.RegularExpressions.Regex.IsMatch(a.Value,@"\b"+System.Text.RegularExpressions.Regex.Escape(prefix)+@"\b")))return false;
        return true;
    }
}
