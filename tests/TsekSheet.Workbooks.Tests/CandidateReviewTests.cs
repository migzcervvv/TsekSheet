using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class CandidateReviewTests
{
    private sealed class Context : IDisposable
    {
        internal readonly string Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-review-" + Guid.NewGuid().ToString("N") + ".xlsx");
        internal readonly WorkbookEngine Engine = new();
        internal readonly IWorkbookSession Main;
        internal readonly WorkbookSession Copy;
        internal readonly string Hash;
        internal Context(string height="21", string feature="plain")
        {
            var source = File.ReadAllBytes(System.IO.Path.Combine(ReferenceCorpusTests.Root,"tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel/"+(feature=="name"?"defined-name":"before-subtotal")+".input.xlsx"));
            source = ExcelFormattingReferenceTests.Rewrite(source,"xl/worksheets/sheet1.xml",doc=> {foreach(var row in doc.Descendants(StructuralPreservationOracle.S+"row").Where(r=>r.Attribute("ht")!=null))row.SetAttributeValue("ht",height);});
            using(var file=new FileStream(Path,FileMode.CreateNew))file.Write(source);
            Hash=ReferenceCorpusFixture.Hash(Path);Main=Engine.Open(Path);Copy=(WorkbookSession)Main.CreateWorkingCopy(Main.Info.Worksheets[0].Id,0);
        }
        internal WorkflowPlanRequest Request(bool batch=false,bool duplicate=false)
        {
            var profile=WorkflowProfiles.Get("synthetic-payables");
            WorkflowRecord Record(string id,string vendor)=>new(id,new Dictionary<string,string>{["vendor"]=vendor,["rfp"]=duplicate?"R2":"NEW-"+id,["amount"]="25",["date"]="2026-10-29"});
            return new(Copy.Info.SessionId,Copy.Info.Worksheets[0].Id,Copy.Info.Revision,profile.Id,WorkflowProfiles.Hash(profile),batch?[Record("one","Novel Company"),Record("two","Distinct Supplier")]:[Record("one","Other Tools Ltd")]);
        }
        internal CandidateReviewApproval Approval(StructuralCandidateReview review,bool duplicate=false)=>new(Copy.Info.SessionId,Main.Info.SessionId,Copy.Info.Worksheets[0].Id,
            review.CandidateId,review.ReviewSha256,review.CandidateSha256,review.ManifestSha256,Hash,review.Proposal.Revision,review.ExpectedDestinationRevision,review.Proposal.ProfileId,review.Proposal.ProfileHash,duplicate);
        public void Dispose(){Copy.Dispose();Main.Dispose();Assert.AreEqual(Hash,ReferenceCorpusFixture.Hash(Path));File.Delete(Path);}
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void UserReviewAndApprovalNeverAdoptEvenWhenExperimentalFlagsChange(bool batch)
    {
        using var c=new Context();var before=c.Copy.Info;var review=c.Copy.PrepareCandidateReview(c.Main,c.Request(batch),0);
        Assert.IsTrue(review.ReviewOnly);Assert.IsTrue(review.OracleReport!.Accepted);Assert.IsEmpty(review.RejectionReasons);Assert.AreEqual(batch?2:1,review.Operation.RowCount);
        Assert.AreEqual(before,c.Copy.Info);var approved=c.Copy.ApproveCandidateReview(c.Main,c.Approval(review));Assert.AreEqual("approved",approved.Lifecycle.Approval);
        Assert.AreEqual(before,c.Copy.Info);Assert.IsFalse(c.Main.Info.IsDirty);Assert.IsFalse(c.Copy.Info.IsDirty);
        Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review)));
        c.Engine.StructuralExperimentsEnabled=true;c.Engine.StructuralIntegrationExperimentsEnabled=true;
        Assert.AreEqual("PRODUCTION_APPLY_DISABLED",Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.AdoptStructuralReview(new(review.CandidateId,review.ReviewSha256,review.CandidateSha256,0,true))).Code);
        Assert.ThrowsExactly<WorkbookException>(()=>((IWorkbookSession)c.Copy).InsertWorkflowRows(review.Proposal,true,true));
        c.Copy.CancelCandidateReview(review.CandidateId,0);Assert.IsNull(c.Copy.CurrentStructuralReview);
        var retry=c.Copy.PrepareCandidateReview(c.Main,c.Request(batch),0);Assert.AreNotEqual(review.CandidateId,retry.CandidateId);
    }
    [TestMethod][DataRow("22")][DataRow("20")][DataRow("21.95")][DataRow("24")]
    public void NativeHeightPolicyIsExactAndBlocksUnqualifiedApproval(string height)
    {
        using var c=new Context(height);var review=c.Copy.PrepareCandidateReview(c.Main,c.Request(),0);Assert.IsNotEmpty(review.RejectionReasons);
        Assert.AreEqual("NATIVE_FORMATTING_UNVERIFIED",Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review))).Code);
        Assert.AreEqual(0,c.Copy.Info.Revision);Assert.IsFalse(c.Copy.Info.IsDirty);
    }
    [TestMethod][DataRow("candidate")][DataRow("review")][DataRow("bytes")][DataRow("manifest")][DataRow("source")][DataRow("profile")][DataRow("section")][DataRow("copy-revision")][DataRow("destination-revision")]
    public void EveryMaterialApprovalBindingRejectsTampering(string field)
    {
        using var c=new Context();var review=c.Copy.PrepareCandidateReview(c.Main,c.Request(),0);var approval=c.Approval(review);
        approval=field switch {"candidate"=>approval with{CandidateId=Guid.NewGuid()},"review"=>approval with{ReviewSha256="forged"},"bytes"=>approval with{CandidateSha256="forged"},"manifest"=>approval with{ManifestSha256="forged"},"source"=>approval with{SourceSha256="forged"},"profile"=>approval with{ProfileHash="forged"},"section"=>approval with{WorksheetId=Guid.NewGuid()},"copy-revision"=>approval with{ExpectedCopyRevision=1},_=>approval with{ExpectedDestinationRevision=1}};
        Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,approval));Assert.AreEqual("not-approved",c.Copy.CurrentStructuralReview!.Lifecycle.Approval);
        Assert.AreEqual("approved",c.Copy.ApproveCandidateReview(c.Main,c.Approval(review)).Lifecycle.Approval);
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void DestinationChangeBeforeOrAfterApprovalInvalidatesReview(bool alreadyApproved)
    {
        using var c=new Context();var review=c.Copy.PrepareCandidateReview(c.Main,c.Request(),0);if(alreadyApproved)c.Copy.ApproveCandidateReview(c.Main,c.Approval(review));
        c.Main.Edit(new(c.Main.Info.Worksheets[0].Id,25,1,"text","Destination diverged",0));
        Assert.AreEqual("STALE_DESTINATION",Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review))).Code);
        Assert.AreEqual(0,c.Copy.Info.Revision);
    }
    [TestMethod]
    public void DuplicateAcknowledgmentAndSourceEditsAreRevalidated()
    {
        using var c=new Context();var review=c.Copy.PrepareCandidateReview(c.Main,c.Request(duplicate:true),0);
        Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review)));
        c.Copy.ApproveCandidateReview(c.Main,c.Approval(review,true));
        c.Copy.Edit(new(c.Copy.Info.Worksheets[0].Id,25,1,"text","Changed after approval",0));
        Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review,true)));
    }
    [TestMethod]
    public void UnsupportedWorkbookCannotGenerateReview()
    {using var c=new Context(feature:"name");Assert.ThrowsExactly<WorkbookException>(()=>c.Copy.PrepareCandidateReview(c.Main,c.Request(),0));Assert.IsNull(c.Copy.CurrentStructuralReview);}
    [TestMethod][DataRow(0)][DataRow(1)]
    public void GenerationAndApprovalCancellationRetainStateAndPermitRetry(int stage)
    {
        using var c=new Context();using var cts=new CancellationTokenSource();StructuralCandidateReview? review=null;if(stage==1)review=c.Copy.PrepareCandidateReview(c.Main,c.Request(),0);
        c.Engine.StructuralLifecycleFaultForTesting=current=>{if((int)current==stage)cts.Cancel();};
        if(stage==0)Assert.ThrowsExactly<OperationCanceledException>(()=>c.Copy.PrepareCandidateReview(c.Main,c.Request(),0,cts.Token));
        else Assert.ThrowsExactly<OperationCanceledException>(()=>c.Copy.ApproveCandidateReview(c.Main,c.Approval(review!),cts.Token));
        Assert.AreEqual(0,c.Copy.Info.Revision);c.Engine.StructuralLifecycleFaultForTesting=null;
        review??=c.Copy.PrepareCandidateReview(c.Main,c.Request(),0);c.Copy.ApproveCandidateReview(c.Main,c.Approval(review));
    }
    [TestMethod][DataRow("height")][DataRow("custom-height")][DataRow("number-format")][DataRow("format-definition")][DataRow("border")][DataRow("fill")][DataRow("defaults")][DataRow("vendor")][DataRow("rfp")][DataRow("invoice")]
    public void OracleRejectsExactFormattingDefects(string defect)
    {
        using var c=new Context();var before=File.ReadAllBytes(c.Path);var m=new StructuralInsertionManifest("Ledger",8,6,6,10,[new StructuralRecord([new(1,"text","Other Tools Ltd"),new(3,"text","NEW"),new(5,"decimal","25")])]);
        var bytes=StructuralRowCandidate.Build(before,m,_=>{},default);var s=StructuralPreservationOracle.S;
        var part=defect is "format-definition" or "border" or "fill"?"xl/styles.xml":"xl/worksheets/sheet1.xml";
        bytes=ExcelFormattingReferenceTests.Rewrite(bytes,part,doc=>{
            if(defect=="height")doc.Descendants(s+"row").Single(r=>(int)r.Attribute("r")! ==8).SetAttributeValue("ht","20.95");
            if(defect=="custom-height")doc.Descendants(s+"row").Single(r=>(int)r.Attribute("r")! ==8).SetAttributeValue("customHeight","0");
            if(defect=="number-format")doc.Descendants(s+"c").Single(e=>(string?)e.Attribute("r")=="E8").SetAttributeValue("s","0");
            if(defect=="format-definition")doc.Descendants(s+"numFmt").First().SetAttributeValue("formatCode","0.000000");
            if(defect=="border")doc.Descendants(s+"bottom").First().SetAttributeValue("style","double");
            if(defect=="fill")doc.Descendants(s+"fgColor").First().SetAttributeValue("rgb","FFFF0000");
            if(defect=="defaults")doc.Descendants(s+"sheetFormatPr").First().SetAttributeValue("defaultRowHeight","16");
            if(defect is "vendor" or "rfp" or "invoice")
            {
                var address=defect=="vendor"?"A8":defect=="rfp"?"C8":"D8";var value=defect=="vendor"?"Acme Trading Incorporated":defect=="rfp"?"R2":"INV-R2";
                var cell=doc.Descendants(s+"c").Single(e=>(string?)e.Attribute("r")==address);cell.RemoveNodes();cell.SetAttributeValue("t","inlineStr");cell.Add(new XElement(s+"is",new XElement(s+"t",value)));
            }
        });
        Assert.IsFalse(StructuralPreservationOracle.Evaluate(before,m,bytes).Accepted);
    }
    [TestMethod]
    public void OracleRejectsReorderedReviewedBatchValues()
    {
        using var c=new Context();var before=File.ReadAllBytes(c.Path);var m=new StructuralInsertionManifest("Ledger",22,16,16,22,
            [new StructuralRecord([new(1,"text","Novel Company"),new(3,"text","FIRST"),new(5,"decimal","25")]),new StructuralRecord([new(1,"text","Distinct Supplier"),new(3,"text","SECOND"),new(5,"decimal","25")])]);
        var bytes=StructuralRowCandidate.Build(before,m,_=>{},default);var s=StructuralPreservationOracle.S;
        bytes=ExcelFormattingReferenceTests.Rewrite(bytes,"xl/worksheets/sheet1.xml",doc=>{
            var first=doc.Descendants(s+"c").Single(e=>(string?)e.Attribute("r")=="C22").Descendants(s+"t").Single();
            var second=doc.Descendants(s+"c").Single(e=>(string?)e.Attribute("r")=="C23").Descendants(s+"t").Single();(first.Value,second.Value)=(second.Value,first.Value);
        });Assert.IsFalse(StructuralPreservationOracle.Evaluate(before,m,bytes).Accepted);
    }
    [TestMethod]
    public async Task RealBridgeGeneratesAndApprovesReviewButRejectsPathsAndAdoption()
    {
        using var c=new Context();var messages=new List<JsonElement>();using var workspace=new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(c.Path));
        using var bridge=new BridgeSession(text=>messages.Add(JsonDocument.Parse(text).RootElement.Clone()),_=>Task.FromResult(new FileSelection("cancelled")),workbooks:workspace);
        var count=0;async Task<JsonElement> Send(string command,object payload){await bridge.HandleAsync(BridgePolicy.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId="review-"+(++count),command,payload},BridgeSession.JsonOptions));return messages[^1];}
        await Send("foundation.handshake",new{supportedVersions=new[]{1}});var open=(await Send("workbook.open",new{})).GetProperty("result").GetProperty("workbook").Deserialize<WorkbookInfo>(BridgeSession.JsonOptions)!;
        var copy=(await Send("workbook.createCopy",new{sessionId=open.SessionId,worksheetId=open.Worksheets[0].Id,expectedRevision=0})).GetProperty("result").Deserialize<WorkingCopySummary>(BridgeSession.JsonOptions)!;
        var request=c.Request() with{SessionId=copy.SessionId,WorksheetId=open.Worksheets[0].Id};var fields=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(JsonSerializer.Serialize(request,BridgeSession.JsonOptions))!;
        fields["destinationSessionId"]=JsonSerializer.SerializeToElement(open.SessionId);fields["expectedDestinationRevision"]=JsonSerializer.SerializeToElement(0);
        var response=await Send("workbook.generateCandidate",fields);Assert.IsTrue(response.GetProperty("ok").GetBoolean(),response.ToString());var review=response.GetProperty("result").Deserialize<StructuralCandidateReview>(BridgeSession.JsonOptions)!;
        var approval=new CandidateReviewApproval(copy.SessionId,open.SessionId,open.Worksheets[0].Id,review.CandidateId,review.ReviewSha256,review.CandidateSha256,review.ManifestSha256,open.SourceSha256,0,0,review.Proposal.ProfileId,review.Proposal.ProfileHash,false);
        Assert.IsTrue((await Send("workbook.approveCandidate",approval)).GetProperty("ok").GetBoolean());Assert.IsFalse(workspace.HasPendingChanges);
        Assert.AreEqual("APPROVAL_REPLAY",(await Send("workbook.approveCandidate",approval)).GetProperty("error").GetProperty("code").GetString());
        fields["path"]=JsonSerializer.SerializeToElement(c.Path);Assert.AreEqual("INVALID_REQUEST",(await Send("workbook.generateCandidate",fields)).GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual("UNSUPPORTED_COMMAND",(await Send("workbook.adoptCandidate",approval)).GetProperty("error").GetProperty("code").GetString());
        Assert.IsTrue((await Send("workbook.cancelCandidate",new{sessionId=copy.SessionId,candidateId=review.CandidateId,expectedRevision=0})).GetProperty("ok").GetBoolean());
    }
}
