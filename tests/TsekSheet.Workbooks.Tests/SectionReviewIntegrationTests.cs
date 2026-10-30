using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Workbooks.Tests;
[TestClass]
public sealed class SectionReviewIntegrationTests
{
    [TestMethod]
    public async Task RealInspectionWorkspaceReviewLifecycleAndSourcePreservation()
    {
        using var f=new SectionReviewFixture();string Hash()=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(f.Path)));var before=Hash();
        using var w=new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(f.Path),selectSectionProfile:_=>Task.FromResult<string?>(SectionReviewFixture.ProfileJson));
        var info=(await w.OpenAsync(true)).Workbook!;var sheet=info.Worksheets[0].Id;var r=(await w.StartSectionReviewAsync(info.SessionId,sheet,0))!;
        Assert.AreEqual(0,w.ReviewDecisionCount);
        r=await w.DecideSectionAsync(new(r.Binding,new(Guid.NewGuid(),"identity",5,null,null,4,3,"Artificial evidence explicitly reviewed")));
        Assert.AreEqual(0,r.After.UncertainIdentities);Assert.IsFalse(r.CanInsert);Assert.IsFalse(w.HasPendingChanges);
        Assert.AreEqual(1,w.ReviewDecisionCount);
        await w.ReadAsync(info.SessionId,info.Worksheets[1].Id,1,1,20,6);
        Assert.AreEqual(0,w.ReviewDecisionCount);
        await Assert.ThrowsExactlyAsync<WorkbookException>(()=>w.DecideSectionAsync(new(r.Binding,new(Guid.NewGuid(),"missing",5,null,null,null,null,"Stale sheet"))));
        r=(await w.StartSectionReviewAsync(info.SessionId,sheet,0))!;Assert.IsEmpty(r.Decisions);w.CancelSectionReview(info.SessionId,sheet);
        await Assert.ThrowsExactlyAsync<WorkbookException>(()=>w.RetractSectionAsync(new(r.Binding,Guid.NewGuid())));
        r=(await w.StartSectionReviewAsync(info.SessionId,sheet,0))!;await w.CloseAsync(info.SessionId);
        await Assert.ThrowsExactlyAsync<WorkbookException>(()=>w.DecideSectionAsync(new(r.Binding,new(Guid.NewGuid(),"missing",5,null,null,null,null,"Closed"))));
        Assert.AreEqual(before,Hash());
    }
    [TestMethod]
    public async Task BridgeReviewCommandsValidateNestedPayloadBindingReplayAndNoPaths()
    {
        using var f=new SectionReviewFixture();var messages=new List<JsonElement>();
        using var b=new BridgeSession(json=>messages.Add(JsonDocument.Parse(json).RootElement.Clone()),_=>Task.FromResult(new FileSelection("cancelled")),workbooks:new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(f.Path),selectSectionProfile:_=>Task.FromResult<string?>(SectionReviewFixture.ProfileJson)));
        var id=0;async Task<JsonElement> Send(string command,object payload){await b.HandleAsync(BridgePolicy.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId="review-"+(id++),command,payload},BridgeSession.JsonOptions));return messages[^1];}
        await Send("foundation.handshake",new{supportedVersions=new[]{1}});var opened=await Send("workbook.inspect",new{});
        var info=opened.GetProperty("result").GetProperty("workbook").Deserialize<WorkbookInfo>(BridgeSession.JsonOptions)!;var sheet=info.Worksheets[0].Id;
        var bad=await Send("sectionReview.start",new{sessionId=info.SessionId,worksheetId=sheet,expectedRevision=0,path="forbidden.json"});Assert.IsFalse(bad.GetProperty("ok").GetBoolean());
        var started=await Send("sectionReview.start",new{sessionId=info.SessionId,worksheetId=sheet,expectedRevision=0});Assert.IsTrue(started.GetProperty("ok").GetBoolean());
        var r=started.GetProperty("result").GetProperty("review").Deserialize<SectionReviewSnapshot>(BridgeSession.JsonOptions)!;
        var request=new SectionReviewRequest(r.Binding,new(Guid.NewGuid(),"identity",5,null,null,4,3,"Artificial evidence reviewed"));
        var valid=await Send("sectionReview.decide",request);Assert.IsTrue(valid.GetProperty("ok").GetBoolean());Assert.IsFalse(valid.GetProperty("result").GetProperty("canInsert").GetBoolean());
        var replay=await Send("sectionReview.decide",request);Assert.AreEqual("SECTION_REVIEW",replay.GetProperty("error").GetProperty("code").GetString());
        var forged=await Send("sectionReview.decide",new{binding=r.Binding,decision=new{id=Guid.NewGuid(),kind="missing",row=5,sectionId=(string?)null,value=(string?)null,evidenceRow=(int?)null,evidenceColumn=(int?)null,reason="Artificial",approved=true}});Assert.AreEqual("INVALID_REQUEST",forged.GetProperty("error").GetProperty("code").GetString());
        Assert.IsFalse(messages.Any(m=>m.GetRawText().Contains(f.Path,StringComparison.Ordinal)));
        await Send("sectionReview.cancel",new{sessionId=info.SessionId,worksheetId=sheet});
        var stale=await Send("sectionReview.decide",request);Assert.AreEqual("SECTION_REVIEW",stale.GetProperty("error").GetProperty("code").GetString());
    }
    [TestMethod]
    public async Task ProfileSelectionCancellationAndMissingConfigurationReleaseState()
    {
        using var f=new SectionReviewFixture();using var w=new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(f.Path));var info=(await w.OpenAsync(true)).Workbook!;
        Assert.AreEqual("PROFILE_REQUIRED",(await Assert.ThrowsExactlyAsync<WorkbookException>(()=>w.StartSectionReviewAsync(info.SessionId,info.Worksheets[0].Id,0))).Code);
        using var cts=new CancellationTokenSource();var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellable=new WorkbookWorkspace(new WorkbookEngine(),_=>Task.FromResult<string?>(f.Path),selectSectionProfile:async token=>{entered.SetResult();await Task.Delay(Timeout.Infinite,token);return null;});
        var second=(await cancellable.OpenAsync(true)).Workbook!;var pending=cancellable.StartSectionReviewAsync(second.SessionId,second.Worksheets[0].Id,0);await entered.Task;
        cancellable.CancelSectionReview(second.SessionId,second.Worksheets[0].Id);await Assert.ThrowsExactlyAsync<TaskCanceledException>(async()=>await pending);
        Assert.IsFalse(cancellable.HasPendingChanges);
    }
}
