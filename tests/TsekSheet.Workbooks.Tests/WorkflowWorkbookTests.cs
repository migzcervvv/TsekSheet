using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class WorkflowWorkbookTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.Get("synthetic-payables");
    private static WorkflowRecord Record => new("one",new Dictionary<string,string>{["vendor"]="Other Tools Ltd",["rfp"]="NEW",["invoice"]="NEW-I",["amount"]="123.45"});
    private static WorkflowPlanRequest Request(IWorkbookSession session, int? revision = null) => new(session.Info.SessionId, session.Info.Worksheets[0].Id,
        revision ?? session.Info.Revision,Profile.Id,WorkflowProfiles.Hash(Profile),[Record]);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [TestMethod]
    [DataRow(15)] [DataRow(45)]
    public void PlanningPreservesRealWorkbookReferencesFormattingAndSourceBytes(int heading)
    {
        using var fixture=new PayablesFixture(heading); var hash=Hash(fixture.Path);
        using var session=new WorkbookEngine().Open(fixture.Path); var before=session.Info;
        Assert.IsTrue(before.EditingAllowed,string.Join("; ",before.Findings.Select(f=>f.Code+": "+f.Message)));
        var sheet=before.Worksheets[0].Id; var cell=session.ReadViewport(sheet,6,1,5,6).Cells.ToArray();
        var plan=WorkflowPlanning.Prepare(session,Request(session));
        Assert.IsTrue(plan.ReadyForReview); Assert.IsFalse(plan.CanApply); Assert.AreEqual(8,plan.Records[0].BaseInsertionRow);
        var missing=Request(session) with {Records=[Record with {Values=new Dictionary<string,string>{["vendor"]="Novel Company",["rfp"]="NEW",["amount"]="25"}}]};
        Assert.AreEqual(heading+7,WorkflowPlanning.Prepare(session,missing).Records[0].BaseInsertionRow);
        Assert.AreEqual(before,session.Info); CollectionAssert.AreEqual(cell,session.ReadViewport(sheet,6,1,5,6).Cells.ToArray()); Assert.AreEqual(hash,Hash(fixture.Path));
        using var copy=session.CreateWorkingCopy(sheet,0);
        var copyPlan=WorkflowPlanning.Prepare(copy,Request(copy));
        Assert.AreEqual("ROW_INSERTION_DISABLED",Assert.ThrowsExactly<WorkbookException>(()=>copy.InsertWorkflowRows(copyPlan with {CanApply=true},true,true)).Code);
        Assert.AreEqual(0,copy.Info.Revision); Assert.IsFalse(copy.Info.IsDirty); Assert.AreEqual(0,copy.WorkingCopy!.Changes.Count);
        Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("missing-subtotal")] [DataRow("duplicate-subtotal")] [DataRow("outside-record")]
    public void UnsafeSyntheticBoundariesNeverProduceAProposalOrChangeSource(string scenario)
    {
        using var fixture=new PayablesFixture(scenario:scenario); var hash=Hash(fixture.Path); using var session=new WorkbookEngine().Open(fixture.Path);
        Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Prepare(session,Request(session)));
        Assert.IsFalse(session.Info.IsDirty); Assert.AreEqual(0,session.Info.Revision); Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    public void ReadOnlyPlanningDoesNotRelaxCalculationChainCompatibilityRejection()
    {
        using var fixture=new PayablesFixture(scenario:"calculation-chain"); var hash=Hash(fixture.Path); using var session=new WorkbookEngine().Open(fixture.Path);
        Assert.IsFalse(session.Info.EditingAllowed); Assert.IsFalse(WorkflowPlanning.Prepare(session,Request(session)).CanApply);
        Assert.ThrowsExactly<WorkbookException>(()=>session.CreateWorkingCopy(session.Info.Worksheets[0].Id,0));
        Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    public void StaleProfilesRevisionsAndForgedApprovalCannotBypassInsertionGate()
    {
        using var fixture=new PayablesFixture(); using var session=new WorkbookEngine().Open(fixture.Path); var hash=Hash(fixture.Path);
        var plan=WorkflowPlanning.Prepare(session,Request(session));
        Assert.AreEqual("REVIEW_REQUIRED",Assert.ThrowsExactly<WorkbookException>(()=>session.InsertWorkflowRows(plan,false,false)).Code);
        Assert.AreEqual("INVALID_PROPOSAL",Assert.ThrowsExactly<WorkbookException>(()=>session.InsertWorkflowRows(plan with {WorksheetId=Guid.NewGuid()},true,true)).Code);
        Assert.AreEqual("STALE_PROFILE",Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Prepare(session,Request(session) with {ProfileHash="wrong"})).Code);
        session.Edit(new(session.Info.Worksheets[0].Id,6,2,"text","Deliberate synthetic edit",0));
        Assert.AreEqual("STALE_REVISION",Assert.ThrowsExactly<WorkbookException>(()=>session.InsertWorkflowRows(plan,true,true)).Code);
        Assert.AreEqual("STALE_REVISION",Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Prepare(session,Request(session,0))).Code);
        Assert.AreEqual(1,session.Info.Revision); Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    public void CancellationDuringCaptureRetainsBothCopyAndOriginalState()
    {
        using var fixture=new PayablesFixture(); var hash=Hash(fixture.Path); using var session=new WorkbookEngine().Open(fixture.Path);
        using var copy=session.CreateWorkingCopy(session.Info.Worksheets[0].Id,0); using var cts=new CancellationTokenSource();
        var reader=new ObservedSession(copy,()=>cts.Cancel());
        Assert.ThrowsExactly<OperationCanceledException>(()=>WorkflowPlanning.Prepare(reader,Request(copy),cts.Token));
        Assert.IsFalse(copy.Info.IsDirty); Assert.IsFalse(session.Info.IsDirty); Assert.AreEqual(0,copy.Info.Revision); Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    public void ConcurrentRevisionChangeAndOversizedWorksheetRejectInsteadOfPartialSearch()
    {
        using var fixture=new PayablesFixture(); using var session=new WorkbookEngine().Open(fixture.Path);
        var called=false;
        var reader=new ObservedSession(session,()=>{if(!called){called=true;session.Edit(new(session.Info.Worksheets[0].Id,6,2,"text","Concurrent edit",0));}});
        Assert.AreEqual("STALE_REVISION",Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Prepare(reader,Request(session))).Code);
        using var large=new PayablesFixture(2100); using var largeSession=new WorkbookEngine().Open(large.Path);
        var capture=new ObservedSession(largeSession,()=>Assert.Fail("Oversized sheet must be rejected before reading any cells."));
        Assert.AreEqual("ANALYSIS_LIMIT",Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Prepare(capture,Request(largeSession))).Code);
    }

    [TestMethod]
    public void PlanningDoesNotAlterMergeOrNewFileSaveAsSemantics()
    {
        using var fixture=new PayablesFixture(); var hash=Hash(fixture.Path); var engine=new WorkbookEngine(); using var main=engine.Open(fixture.Path);
        var sheet=main.Info.Worksheets[0].Id; using var copy=main.CreateWorkingCopy(sheet,0);
        var oldPlan=WorkflowPlanning.Prepare(main,Request(main)); copy.Edit(new(sheet,6,2,"text","Approved existing-cell edit",0));
        var copyPlan=WorkflowPlanning.Prepare(copy,Request(copy)); Assert.IsFalse(copyPlan.CanApply);
        main.Integrate(copy,new(copy.Info.SessionId,sheet,1,0,0));
        Assert.AreEqual("STALE_REVISION",Assert.ThrowsExactly<WorkbookException>(()=>main.InsertWorkflowRows(oldPlan,true,true)).Code);
        Assert.IsFalse(main.PreviewIntegration(copy,new(copy.Info.SessionId,sheet,1,1,0)).CanApply);
        var output=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(fixture.Path)!,"planned-export.xlsx");
        try
        {
            main.SaveAsNew(output,1); using var reopened=engine.Open(output);
            Assert.AreEqual("Approved existing-cell edit",reopened.ReadViewport(reopened.Info.Worksheets[0].Id,6,2,1,1).Cells[0].Value);
            Assert.AreEqual("DESTINATION_EXISTS",Assert.ThrowsExactly<WorkbookException>(()=>main.SaveAsNew(output,1)).Code);
        }
        finally { File.Delete(output); }
        Assert.AreEqual(hash,Hash(fixture.Path));
    }

    [TestMethod]
    public async Task ActualBridgePlansButRejectsPathsAndUnfinishedApplyCommands()
    {
        using var fixture=new PayablesFixture(); var hash=Hash(fixture.Path); var messages=new List<JsonElement>();
        using var workspace=new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(fixture.Path));
        using var bridge=new BridgeSession(s=>messages.Add(JsonDocument.Parse(s).RootElement.Clone()),_=>Task.FromResult(new FileSelection("cancelled")),workbooks:workspace);
        var count=0; async Task<JsonElement> Send(string command,object payload)
        {await bridge.HandleAsync(BridgePolicy.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId="workflow-"+count++,command,payload}));return messages[^1];}
        await Send("foundation.handshake",new{supportedVersions=new[]{1}});
        var catalog=await Send("workflow.profiles",new{}); Assert.AreEqual(2,catalog.GetProperty("result").GetProperty("profiles").GetArrayLength());
        var info=(await Send("workbook.open",new{})).GetProperty("result").GetProperty("workbook");
        var sessionId=info.GetProperty("sessionId").GetGuid(); var worksheetId=info.GetProperty("worksheets")[0].GetProperty("id").GetGuid();
        var request=new WorkflowPlanRequest(sessionId,worksheetId,0,Profile.Id,WorkflowProfiles.Hash(Profile),[Record]);
        var response=await Send("workbook.planRecords",JsonSerializer.SerializeToElement(request,BridgeSession.JsonOptions));
        Assert.IsTrue(response.GetProperty("ok").GetBoolean()); Assert.IsFalse(response.GetProperty("result").GetProperty("canApply").GetBoolean());
        Assert.IsFalse(response.GetRawText().Contains(System.IO.Path.GetDirectoryName(fixture.Path)!,StringComparison.Ordinal));
        var bad=await Send("workbook.planRecords",new{sessionId,worksheetId,expectedRevision=0,profileId=Profile.Id,profileHash=WorkflowProfiles.Hash(Profile),records=new[]{Record},path=fixture.Path});
        Assert.AreEqual("INVALID_REQUEST",bad.GetProperty("error").GetProperty("code").GetString());
        Assert.AreEqual("UNSUPPORTED_COMMAND",(await Send("workbook.applyRecords",request)).GetProperty("error").GetProperty("code").GetString());
        Assert.IsFalse(workspace.HasPendingChanges); Assert.AreEqual(hash,Hash(fixture.Path));
    }

    private sealed class ObservedSession(IWorkbookSession inner,Action afterRead) : IWorkbookSession
    {
        public WorkbookInfo Info=>inner.Info; public WorkingCopyInfo? WorkingCopy=>inner.WorkingCopy;
        public CellViewport ReadViewport(Guid id,int row,int column,int rows,int columns){var result=inner.ReadViewport(id,row,column,rows,columns);afterRead();return result;}
        public IWorkbookSession CreateSnapshot(CancellationToken token=default)=>throw new NotSupportedException();
        public IWorkbookSession CreateWorkingCopy(Guid id,int revision,CancellationToken token=default)=>throw new NotSupportedException();
        public WorkbookInfo Edit(CellEdit edit,CancellationToken token=default)=>throw new NotSupportedException();
        public SaveReceipt SaveAsNew(string path,int revision,CancellationToken token=default)=>throw new NotSupportedException();
        public void Dispose(){}
    }
}
