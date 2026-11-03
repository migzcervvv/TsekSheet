using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.ExcelNative.Poc.Tests;

[TestClass]
public sealed class ExperimentalSafetyTests
{
    private static string Fixture(){var root=ExperimentPaths.CreateRun();SyntheticFixture.Create(root);return root;}
    private static string Mutate(string root,string part,Func<string,string> change)
    {
        var path=Path.Combine(root,"tampered.xlsx");File.Copy(Path.Combine(root,"baseline.xlsx"),path,false);
        using var zip=ZipFile.Open(path,ZipArchiveMode.Update);var entry=zip.GetEntry(part)!;
        string text;using(var reader=new StreamReader(entry.Open()))text=reader.ReadToEnd();entry.Delete();
        using(var writer=new StreamWriter(zip.CreateEntry(part).Open(),new UTF8Encoding(false)))writer.Write(change(text));return path;
    }
    [TestMethod]
    public void LiteralFixtureHasFiveExactTabsAndDeterministicArtificialContents()
    {
        var first=Fixture();var second=Fixture();var path=Path.Combine(first,"baseline.xlsx");
        Assert.AreEqual(ExperimentPaths.Hash(path),ExperimentPaths.Hash(Path.Combine(second,"baseline.xlsx")));
        Assert.AreEqual(SyntheticFixture.ExpectedSha256(),ExperimentPaths.Hash(path));
        var evidence=PackageEvidence.Read(path);evidence.RequireSynthetic();XNamespace s=SyntheticFixture.Main;
        CollectionAssert.AreEqual(SyntheticFixture.SheetNames,evidence.Documents["xl/workbook.xml"].Descendants(s+"sheet").Select(e=>(string)e.Attribute("name")!).ToArray());
        Assert.AreEqual(18,evidence.Documents.Values.Sum(d=>d.Descendants(s+"c").Count()));
        var main=evidence.Documents["xl/worksheets/sheet1.xml"];
        Assert.AreEqual("SUM(B3:B4)",main.Descendants(s+"c").Single(c=>(string?)c.Attribute("r")=="B5").Element(s+"f")!.Value);
        Assert.AreEqual("30",main.Descendants(s+"c").Single(c=>(string?)c.Attribute("r")=="B5").Element(s+"v")!.Value);
        Assert.IsTrue(main.Descendants(s+"row").Where(r=>new[]{"3","4","5"}.Contains((string?)r.Attribute("r"))).All(r=>(string?)r.Attribute("ht")=="21"&&(string?)r.Attribute("customHeight")=="1"));
        Assert.ThrowsExactly<IOException>(()=>SyntheticFixture.Create(first));
    }
    [TestMethod]
    public void IdenticalEvidenceDoesNotConferInsertionAuthority()
    {
        var root=Fixture();var evidence=PackageEvidence.Read(Path.Combine(root,"baseline.xlsx"));
        var comparison=PackageEvidence.Compare(evidence,evidence);
        Assert.IsTrue(comparison.Qualified);Assert.IsFalse(comparison.InsertionAllowed);Assert.AreEqual(0,comparison.XmlDifferences);
        var semantic=SemanticEvidence.Compare(evidence,evidence);Assert.AreEqual(0,semantic.Differences);Assert.AreEqual(18,semantic.BeforeCells);
    }
    [TestMethod]
    [DataRow("value")][DataRow("formula")][DataRow("cache")][DataRow("height")][DataRow("style")][DataRow("font")][DataRow("identity")]
    public void IndependentOracleRejectsUnapprovedCellFormulaCacheLayoutStyleAndIdentityChanges(string fault)
    {
        var root=Fixture();var source=Path.Combine(root,"baseline.xlsx");var hash=ExperimentPaths.Hash(source);
        var part=fault is "style" or "font"?"xl/styles.xml":fault=="identity"?"xl/workbook.xml":"xl/worksheets/sheet1.xml";
        var changed=Mutate(root,part,text=>fault switch
        {
            "value"=>text.Replace("<v>10</v>","<v>11</v>",StringComparison.Ordinal),
            "formula"=>text.Replace("SUM(B3:B4)","SUM(B3:B3)",StringComparison.Ordinal),
            "cache"=>text.Replace("<v>30</v>","<v>31</v>",StringComparison.Ordinal),
            "height"=>text.Replace("ht=\"21\"","ht=\"22\"",StringComparison.Ordinal),
            "style"=>text.Replace("numFmtId=\"2\"","numFmtId=\"4\"",StringComparison.Ordinal),
            "font"=>text.Replace("Calibri","Aptos Narrow",StringComparison.Ordinal),
            _=>text.Replace("Unpaid RFPs - SEP","Unpaid RFPs - OTHER",StringComparison.Ordinal)
        });
        var before=PackageEvidence.Read(source);var after=PackageEvidence.Read(changed);
        var comparison=PackageEvidence.Compare(before,after);Assert.IsFalse(comparison.Qualified);Assert.IsFalse(comparison.InsertionAllowed);Assert.IsTrue(comparison.XmlDifferences>0);
        Assert.IsTrue(SemanticEvidence.Compare(before,after).Differences>0);Assert.AreEqual(hash,ExperimentPaths.Hash(source));
    }
    [TestMethod]
    public void UnknownAddedPartsFailRatherThanBecomingExcelNormalizationExemptions()
    {
        var root=Fixture();var source=Path.Combine(root,"baseline.xlsx");var target=Path.Combine(root,"tampered.xlsx");File.Copy(source,target);
        using(var zip=ZipFile.Open(target,ZipArchiveMode.Update))using(var writer=new StreamWriter(zip.CreateEntry("xl/unknown.xml").Open()))writer.Write("<unknown/>");
        var result=PackageEvidence.Compare(PackageEvidence.Read(source),PackageEvidence.Read(target));
        Assert.IsFalse(result.Qualified);Assert.IsTrue(result.Parts.Any(p=>p.Part=="xl/unknown.xml"&&p.Kind=="added"));
    }
    [TestMethod]
    public void ExperimentalWorksheetBudgetDoesNotDoubleCountCalculationChainEntries()
    {
        var root=Fixture();var source=Path.Combine(root,"baseline.xlsx");var target=Path.Combine(root,"chain.xlsx");File.Copy(source,target);
        using(var zip=ZipFile.Open(target,ZipArchiveMode.Update))using(var writer=new StreamWriter(zip.CreateEntry("xl/calcChain.xml").Open()))
            writer.Write("<calcChain xmlns=\""+SyntheticFixture.Main+"\">"+string.Concat(Enumerable.Repeat("<c r=\"B5\" i=\"1\"/>",10000))+"</calcChain>");
        var evidence=PackageEvidence.Read(target);
        Assert.AreEqual(18,SemanticEvidence.Compare(evidence,evidence).BeforeCells);
        Assert.AreEqual("COMPARISON_REPORT_LIMIT",Assert.ThrowsExactly<ExperimentException>(()=>PackageEvidence.Compare(PackageEvidence.Read(source),evidence)).Code);
        Assert.ThrowsExactly<ExperimentException>(()=>SyntheticFixture.RequireBaselineHash(ExperimentPaths.Hash(target)));
    }
    [TestMethod]
    [DataRow("origin")][DataRow("external")][DataRow("dangling")][DataRow("dtd")][DataRow("macro")]
    public void UnsafeOrForeignPackagesRejectBeforeCom(string mode)
    {
        var root=Fixture();var part=mode=="origin"?"docProps/custom.xml":mode=="dtd"?"xl/workbook.xml":"xl/_rels/workbook.xml.rels";
        var target=Mutate(root,part,text=>mode switch
        {
            "origin"=>text.Replace(SyntheticFixture.Marker,"FOREIGN",StringComparison.Ordinal),
            "external"=>text.Replace("Target=\"styles.xml\"","Target=\"file:///NEVER-OPEN\" TargetMode=\"External\"",StringComparison.Ordinal),
            "dangling"=>text.Replace("Target=\"styles.xml\"","Target=\"missing.xml\"",StringComparison.Ordinal),
            "dtd"=>"<!DOCTYPE workbook [<!ENTITY unsafe SYSTEM 'file:///NEVER-OPEN'>]>"+text,
            _=>text
        });
        if(mode=="macro")using(var zip=ZipFile.Open(target,ZipArchiveMode.Update))using(var writer=new StreamWriter(zip.CreateEntry("xl/vbaProject.bin").Open()))writer.Write("not executable");
        if(mode=="dtd")Assert.ThrowsExactly<System.Xml.XmlException>(()=>PackageEvidence.Read(target));
        else
        {
            var failure=Assert.ThrowsExactly<ExperimentException>(()=>PackageEvidence.Read(target).RequireSynthetic());
            Assert.AreEqual(mode switch{"origin"=>"SYNTHETIC_ORIGIN_REQUIRED","external"=>"EXTERNAL_OR_UNQUALIFIED_RELATIONSHIP","dangling"=>"BROKEN_RELATIONSHIP",_=>"ACTIVE_OR_UNQUALIFIED_PART"},failure.Code);
        }
        Assert.ThrowsExactly<ExperimentException>(()=>SyntheticFixture.RequireBaselineHash(ExperimentPaths.Hash(target)));
    }
    [TestMethod]
    [DataRow("file")][DataRow("part")][DataRow("entries")][DataRow("depth")]
    public void AllocationAndParsingBudgetsRejectBeforeModelLoading(string budget)
    {
        var root=ExperimentPaths.CreateRun();var path=Path.Combine(root,"oversized.xlsx");
        if(budget=="file"){using var file=File.Create(path);file.SetLength(4*1024*1024+1);}
        else
        {
            using var zip=ZipFile.Open(path,ZipArchiveMode.Create);
            var entries=budget=="entries"?129:1;
            for(var i=0;i<entries;i++)using(var writer=new StreamWriter(zip.CreateEntry("part"+i+".xml").Open()))writer.Write(budget=="part"?"<root>"+new string('x',2*1024*1024)+"</root>":budget=="depth"?string.Concat(Enumerable.Repeat("<a>",70))+string.Concat(Enumerable.Repeat("</a>",70)):"<root/>");
        }
        Assert.ThrowsExactly<ExperimentException>(()=>PackageEvidence.Read(path));
    }
    [TestMethod]
    public void ProductionPathsAndUnimplementedEditingCommandsAreNotAccepted()
    {
        Assert.ThrowsExactly<ExperimentException>(()=>ExperimentPaths.ValidateRoot(ExperimentPaths.RepositoryRoot));
        Assert.ThrowsExactly<ExperimentException>(()=>ExperimentPaths.ValidateRoot(Path.GetTempPath()));
        var root=ExperimentPaths.CreateRun();
        Assert.ThrowsExactly<ExperimentException>(()=>ExperimentPaths.Validate(new(1,"insert",root)));
        Assert.ThrowsExactly<ExperimentException>(()=>ExperimentPaths.Validate(new(1,"noop",root)));
        Assert.ThrowsExactly<ExperimentException>(()=>ExperimentPaths.Validate(new(2,"probe",root)));
    }
    [TestMethod]
    public void UnknownFailedOrUnownedOutcomesCannotAdvanceToWorkbookOpening()
    {
        var ownership=new ExcelOwnership(123,1,1,1,[]);
        var worker=new WorkerResult("completed","COMPLETED",true,true,true,false,false,ownership,"security",1,1,InstanceSettingsRestored:true);
        var result=new SupervisorResult(worker,false,"COMPLETED",0,1,1);
        Assert.IsTrue(GateProgression.AllowsNoop(result));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{OutcomeUnknown=true}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{ExitCode=2}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{Worker=worker with{OwnershipVerified=false}}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{Worker=worker with{CleanupConfirmed=false}}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{Worker=worker with{InstanceSettingsRestored=false}}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{Worker=worker with{Status="invalidated"}}));
        Assert.IsFalse(GateProgression.AllowsNoop(result with{Worker=worker with{Ownership=ownership with{ExistingProcessIds=[123]}}}));
    }
    [TestMethod]
    public async Task CancellationBeforeLaunchDoesNotCreateARequestOrExcelInstance()
    {
        var root=ExperimentPaths.CreateRun();using var cancel=new CancellationTokenSource();cancel.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(()=>WorkerSupervisor.Run(new(1,"probe",root),token:cancel.Token));
        Assert.IsFalse(File.Exists(Path.Combine(root,"request.json")));Assert.IsFalse(File.Exists(Path.Combine(root,"ownership.json")));
    }
    [TestMethod]
    [DataRow("source")][DataRow("working")][DataRow("candidate")][DataRow("cancel")]
    public void PreflightFailureNeverReachesNativeActivationOrOverwritesOutput(string fault)
    {
        var root=Fixture();var source=Path.Combine(root,"baseline.xlsx");var expected=ExperimentPaths.Hash(source);
        string? protectedOutput=null;
        if(fault=="source")
        {
            using var file=new FileStream(source,FileMode.Open,FileAccess.Write);file.Position=10;file.WriteByte(42);
        }
        else if(fault=="cancel")File.WriteAllText(Path.Combine(root,"cancel"),"cancel");
        else{protectedOutput=Path.Combine(root,fault+".xlsx");File.WriteAllText(protectedOutput,"protected artificial output");}
        var before=ExperimentPaths.Hash(source);WorkerResult? result=null;Exception? error=null;
        var thread=new Thread(()=>{try{result=new ExcelProvider().Execute(new(1,"noop",root,expected));}catch(Exception failure){error=failure;}}){IsBackground=true};
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(5)),"Preflight unexpectedly blocked.");
        Assert.IsNull(error);Assert.IsNotNull(result);Assert.IsNull(result.Ownership);Assert.IsFalse(result.OwnershipVerified);Assert.IsTrue(result.CleanupConfirmed);
        Assert.AreEqual(fault switch{"source"=>"STALE_SOURCE","working"=>"IO_FAILED","candidate"=>"CANDIDATE_EXISTS",_=>"CANCELLED"},result.Code);
        Assert.AreEqual(before,ExperimentPaths.Hash(source));
        if(protectedOutput!=null)Assert.AreEqual("protected artificial output",File.ReadAllText(protectedOutput));
        Assert.IsFalse(File.Exists(Path.Combine(root,"ownership.json")));
    }
}
