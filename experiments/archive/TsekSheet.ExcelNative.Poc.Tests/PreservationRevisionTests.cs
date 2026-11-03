using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.ExcelNative.Poc.Tests;

[TestClass]
public sealed class PreservationRevisionTests
{
    private static (string Root,PackageEvidence Package) Fixture(string variant=SyntheticFixture.ExplicitTheme)
    {
        var root=ExperimentPaths.CreateRun();SyntheticFixture.Create(root,variant);
        return(root,PackageEvidence.Read(Path.Combine(root,"baseline.xlsx")));
    }
    private static PackageEvidence Change(string root,string part,Action<XDocument> mutate)
    {
        var output=Path.Combine(root,"fault.xlsx");File.Copy(Path.Combine(root,"baseline.xlsx"),output,false);
        using(var zip=ZipFile.Open(output,ZipArchiveMode.Update))
        {
            var entry=zip.GetEntry(part)!;XDocument document;using(var stream=entry.Open())document=XDocument.Load(stream);
            mutate(document);entry.Delete();using var writer=new StreamWriter(zip.CreateEntry(part).Open(),new UTF8Encoding(false));writer.Write(document.ToString(SaveOptions.DisableFormatting));
        }
        return PackageEvidence.Read(output);
    }
    [TestMethod]
    [DataRow(SyntheticFixture.ExplicitTheme)][DataRow(SyntheticFixture.LiteralFont)][DataRow(SyntheticFixture.IncompleteTheme)]
    public void ControlledFixturesAreSchemaValidDeterministicAndHashBound(string variant)
    {
        var(root,package)=Fixture(variant);package.RequireSynthetic();
        Assert.AreEqual(SyntheticFixture.ExpectedSha256(variant),ExperimentPaths.Hash(Path.Combine(root,"baseline.xlsx")));
        SyntheticFixture.RequireBaselineHash(SyntheticFixture.ExpectedSha256(variant));
        using var workbook=SpreadsheetDocument.Open(Path.Combine(root,"baseline.xlsx"),false,new OpenSettings{AutoSave=false,MaxCharactersInPart=2*1024*1024});
        var errors=new OpenXmlValidator(FileFormatVersions.Microsoft365){MaxNumberOfErrors=50}.Validate(workbook).ToArray();
        Assert.AreEqual(0,errors.Length,string.Join("\n",errors.Select(e=>e.Id+"|"+e.Path?.XPath+"|"+e.Description)));
    }
    [TestMethod]
    public void OriginalFailedFixtureBytesAndUnresolvedThemeRemainANegativeControl()
    {
        Assert.AreEqual("CE86AA36A022D766D5C91027E4F315F353449DC6CAEE2FABDE9CF2E66CB9D5AB",SyntheticFixture.ExpectedSha256());
        var(_,source)=Fixture(SyntheticFixture.IncompleteTheme);var font=PreservationReview.Fonts(source).Single();
        Assert.AreEqual("Calibri",font.Declared);Assert.AreEqual("minor",font.Scheme);Assert.AreEqual("<unresolved-theme-font>",font.EffectiveLatin);
        var review=PreservationReview.Compare(source,source);Assert.IsFalse(review.Qualified);
        Assert.IsTrue(review.Findings.Any(f=>f.Code=="SOURCE_THEME_UNRESOLVED"&&f.Classification=="always-fail"));
    }
    [TestMethod]
    [DataRow(SyntheticFixture.ExplicitTheme)][DataRow(SyntheticFixture.LiteralFont)]
    public void ControlledBaselinesResolveCalibriWithoutChangingExpectedFont(string variant)
    {
        var(_,source)=Fixture(variant);var font=PreservationReview.Fonts(source).Single();
        Assert.AreEqual("Calibri",font.Declared);Assert.AreEqual("Calibri",font.EffectiveLatin);Assert.AreEqual("11",font.Size);
        Assert.AreEqual(variant==SyntheticFixture.LiteralFont?"":"minor",font.Scheme);
        Assert.AreEqual(variant==SyntheticFixture.LiteralFont?"FF000000":"system:windowText;fallback:000000",font.Color);
        Assert.IsTrue(PreservationReview.Compare(source,source).Qualified);
    }
    [TestMethod]
    [DataRow("declared-font")][DataRow("theme-font")][DataRow("height")][DataRow("formula")][DataRow("cache")][DataRow("value")][DataRow("name")][DataRow("dimension")][DataRow("target")][DataRow("lost-relationship")][DataRow("calculation")]
    public void HardPreservationDefectsCannotBeReclassifiedAsSerialization(string fault)
    {
        var(root,source)=Fixture();XNamespace s=SyntheticFixture.Main;XNamespace a="http://schemas.openxmlformats.org/drawingml/2006/main";
        var part=fault switch{"declared-font"=>"xl/styles.xml","theme-font"=>"xl/theme/theme1.xml","name" or "target" or "calculation"=>"xl/workbook.xml","lost-relationship"=>"xl/_rels/workbook.xml.rels",_=>"xl/worksheets/sheet1.xml"};
        var changed=Change(root,part,document=>
        {
            if(fault=="declared-font")document.Descendants(s+"font").First().Element(s+"name")!.SetAttributeValue("val","Aptos Narrow");
            else if(fault=="theme-font")document.Descendants(a+"minorFont").Single().Element(a+"latin")!.SetAttributeValue("typeface","Aptos Narrow");
            else if(fault=="height")document.Descendants(s+"row").Single(r=>(string?)r.Attribute("r")=="3").SetAttributeValue("ht","22");
            else if(fault=="formula")document.Descendants(s+"f").First().Value="SUM(B3:B3)";
            else if(fault=="cache")document.Descendants(s+"c").Single(c=>(string?)c.Attribute("r")=="B5").Element(s+"v")!.Value="31";
            else if(fault=="value")document.Descendants(s+"c").Single(c=>(string?)c.Attribute("r")=="B3").Element(s+"v")!.Value="11";
            else if(fault=="name")document.Root!.Element(s+"sheets")!.AddAfterSelf(new XElement(s+"definedNames",new XElement(s+"definedName",new XAttribute("name","UnexpectedName"),"'Unpaid RFPs - SEP'!$B$5")));
            else if(fault=="dimension")document.Descendants(s+"dimension").Single().SetAttributeValue("ref","A1:C8");
            else if(fault=="target")document.Descendants(s+"sheet").First().SetAttributeValue(XName.Get("id",SyntheticFixture.Office),"s2");
            else if(fault=="calculation")document.Descendants(s+"calcPr").Single().SetAttributeValue("calcMode","auto");
            else document.Root!.Elements().Single(e=>(string?)e.Attribute("Id")=="styles").Remove();
        });
        var review=PreservationReview.Compare(source,changed);Assert.IsFalse(review.Qualified);Assert.IsTrue(review.AlwaysFail>0,fault);
        Assert.IsFalse(review.InsertionAuthorized);Assert.IsFalse(PackageEvidence.Compare(source,changed).Qualified);
        if(fault=="lost-relationship")Assert.IsTrue(review.Findings.Any(f=>f.Code=="RELATIONSHIP_LOST"));
        if(fault=="theme-font")Assert.IsTrue(review.Findings.Any(f=>f.Code=="FONT_IDENTITY_OR_THEME_CHANGED"));
    }
    [TestMethod]
    public void DescentRequiresVisualReviewAndExplicitHeightStillFails()
    {
        var(root,source)=Fixture();XNamespace s=SyntheticFixture.Main;XNamespace ac="http://schemas.microsoft.com/office/spreadsheetml/2009/9/ac";
        var changed=Change(root,"xl/worksheets/sheet1.xml",document=>
        {
            document.Descendants(s+"sheetFormatPr").Single().SetAttributeValue(ac+"dyDescent","0.25");
            foreach(var row in document.Descendants(s+"row"))row.SetAttributeValue(ac+"dyDescent","0.25");
        });
        var review=PreservationReview.Compare(source,changed);Assert.IsFalse(review.Qualified);Assert.AreEqual(0,review.AlwaysFail);
        Assert.AreEqual(6,review.Findings.Count(f=>f.Code=="ROW_DESCENT_VISUAL_REVIEW"));
    }
    [TestMethod]
    public void EncodingEquivalenceIsNarrowAndRawDifferencesRemainVisible()
    {
        var(root,source)=Fixture();var output=Path.Combine(root,"fault.xlsx");File.Copy(Path.Combine(root,"baseline.xlsx"),output);
        XNamespace s=SyntheticFixture.Main;
        using(var zip=ZipFile.Open(output,ZipArchiveMode.Update))
        {
            var strings=new List<XElement>();
            foreach(var path in Enumerable.Range(1,5).Select(i=>$"xl/worksheets/sheet{i}.xml"))
            {
                var entry=zip.GetEntry(path)!;XDocument doc;using(var stream=entry.Open())doc=XDocument.Load(stream);
                foreach(var cell in doc.Descendants(s+"c").Where(c=>(string?)c.Attribute("t")=="inlineStr"))
                {
                    strings.Add(new XElement(s+"si",cell.Element(s+"is")!.Nodes()));cell.Element(s+"is")!.Remove();cell.SetAttributeValue("t","s");cell.Add(new XElement(s+"v",strings.Count-1));
                }
                entry.Delete();using var writer=new StreamWriter(zip.CreateEntry(path).Open());writer.Write(doc.ToString(SaveOptions.DisableFormatting));
            }
            using var shared=new StreamWriter(zip.CreateEntry("xl/sharedStrings.xml").Open());shared.Write(new XDocument(new XElement(s+"sst",strings)).ToString(SaveOptions.DisableFormatting));
        }
        var candidate=PackageEvidence.Read(output);var review=PreservationReview.Compare(source,candidate);
        Assert.IsTrue(review.Findings.Any(f=>f.Code=="STRING_ENCODING_EQUIVALENT"&&f.Classification=="accepted-serialization"));
        Assert.AreEqual(0,review.AlwaysFail);Assert.IsFalse(review.Qualified,"Other raw part/content type/relationship changes are not approved by string equivalence.");
        Assert.IsFalse(PackageEvidence.Compare(source,candidate).Qualified);
    }
    [TestMethod]
    public void PrefixAndAttributeOrderChangesRetainRawEvidenceWithoutSemanticChange()
    {
        var(root,source)=Fixture();var candidate=Change(root,"xl/workbook.xml",d=>d.Root!.SetAttributeValue(XNamespace.Xmlns+"differentPrefix","urn:unused"));
        var raw=PackageEvidence.Compare(source,candidate);Assert.IsFalse(raw.Qualified);Assert.AreEqual(0,raw.XmlDifferences);Assert.AreEqual(1,raw.PartDifferences);
        var review=PreservationReview.Compare(source,candidate);Assert.AreEqual(0,review.AlwaysFail);Assert.AreEqual(0,review.RequiresReview);
        Assert.IsTrue(review.Findings.Any(f=>f.Code=="XML_SERIALIZATION_ONLY"));Assert.IsFalse(review.InsertionAuthorized);
    }
    [TestMethod]
    public void OnlyKnownFalseCalculationFlagOmissionIsReviewable()
    {
        var(root,source)=Fixture();XNamespace s=SyntheticFixture.Main;
        var candidate=Change(root,"xl/workbook.xml",d=>{var calc=d.Root!.Element(s+"calcPr")!;calc.Attribute("fullCalcOnLoad")!.Remove();calc.Attribute("forceFullCalc")!.Remove();});
        var review=PreservationReview.Compare(source,candidate);Assert.AreEqual(0,review.AlwaysFail);Assert.IsFalse(review.Qualified);
        Assert.IsTrue(review.Findings.Any(f=>f.Code=="CALCULATION_SETTINGS_REVIEW"));
    }
    [TestMethod]
    [DataRow("comment")][DataRow("binding")][DataRow("element-order")]
    public void EqualPositionalFactsCannotWaiveLostContentNamespaceMeaningOrElementOrder(string fault)
    {
        var(root,source)=Fixture();XNamespace s=SyntheticFixture.Main;
        // These are unknown raw changes, not approved encoding equivalences.
        var candidate=Change(root,"xl/workbook.xml",d=>
        {
            if(fault=="comment")d.Root!.AddFirst(new XComment("Artificial unrelated comment"));
            else if(fault=="binding")d.Root!.SetAttributeValue(XNamespace.Xmlns+"r","urn:changed-binding");
            else{var calc=d.Root!.Element(s+"calcPr")!;calc.Remove();d.Root.AddFirst(calc);}
        });
        var review=PreservationReview.Compare(source,candidate);Assert.IsFalse(review.Qualified);Assert.IsTrue(review.RequiresReview>0);
        Assert.IsFalse(review.Findings.Any(f=>f.Code=="XML_SERIALIZATION_ONLY"));
    }
    [TestMethod]
    [DataRow("part")][DataRow("comment")]
    public void UnrelatedPackageAndXmlContentCannotDisappear(string kind)
    {
        var(root,_)=Fixture();var source=Path.Combine(root,"baseline.xlsx");var saved=Path.Combine(root,"unchanged-original.xlsx");File.Copy(source,saved);
        using(var zip=ZipFile.Open(source,ZipArchiveMode.Update))
        {
            if(kind=="part")using(var writer=new StreamWriter(zip.CreateEntry("xl/artificial-unrelated.xml").Open()))writer.Write("<artificial/>");
            else
            {
                var entry=zip.GetEntry("xl/workbook.xml")!;XDocument doc;using(var stream=entry.Open())doc=XDocument.Load(stream);
                doc.Root!.AddFirst(new XComment("Retain unrelated synthetic annotation"));entry.Delete();
                using var writer=new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open());writer.Write(doc.ToString(SaveOptions.DisableFormatting));
            }
        }
        var review=PreservationReview.Compare(PackageEvidence.Read(source),PackageEvidence.Read(saved));
        Assert.IsFalse(review.Qualified);Assert.IsTrue(review.Findings.Any(f=>f.Classification=="always-fail"&&f.Code==(kind=="part"?"PACKAGE_PART_LOST":"XML_ANNOTATION_LOST")));
    }
    [TestMethod]
    public void GateBRequiresBothIndependentOraclesAndAllNativePreservationChecks()
    {
        var(_,source)=Fixture();var raw=PackageEvidence.Compare(source,source);var review=PreservationReview.Compare(source,source);
        var owner=new ExcelOwnership(123,1,1,1,[]);
        var worker=new WorkerResult("completed","COMPLETED",true,true,true,true,true,owner,"reopen",1,1,InstanceSettingsRestored:true);
        var native=new SupervisorResult(worker,false,"COMPLETED",0,1,1);
        Assert.IsTrue(GateProgression.AllowsPreservedNoop(native,raw,review));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native,raw,null));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native,raw,review with{Qualified=false}));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native,raw with{Qualified=false},review));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native with{OutcomeUnknown=true},raw,review));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native with{Worker=worker with{SourceUnchanged=false}},raw,review));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native with{Worker=worker with{ExactWorkingCopy=false}},raw,review));
        Assert.IsFalse(GateProgression.AllowsPreservedNoop(native with{Worker=worker with{CandidateReopened=false}},raw,review));
    }
}
