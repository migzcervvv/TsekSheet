using System.Text.Json;

namespace TsekSheet.ExcelNative.Poc;

public sealed record GateResult(string Gate,bool Passed,bool InsertionAuthorized,SupervisorResult Outcome,ComparisonReport? Comparison,PreservationReviewReport? Review=null);

public static class GateProgression
{
    public static bool AllowsNoop(SupervisorResult result)=>!result.OutcomeUnknown&&result.ExitCode==0&&result.Worker is
        {Status:"completed",Code:"COMPLETED",OwnershipVerified:true,CleanupConfirmed:true,InstanceSettingsRestored:true,Ownership:{ProcessId:>0,StartUtcTicks:>0,WindowHandle:not 0,SessionId:>=0} identity}
        &&!identity.ExistingProcessIds.Contains(identity.ProcessId);
    public static bool AllowsPreservedNoop(SupervisorResult result,ComparisonReport? raw,PreservationReviewReport? review)=>
        AllowsNoop(result)&&result.Worker is{SourceUnchanged:true,ExactWorkingCopy:true,CandidateReopened:true}
        &&raw is{Qualified:true,InsertionAllowed:false}&&review is{Qualified:true,InsertionAuthorized:false};
    public static void RequireGateA(string root)
    {
        ExperimentPaths.ValidateRoot(root);
        var path=Path.Combine(root,"gate-result.json");
        if(new FileInfo(path).Length>262144)throw new ExperimentException("GATE_A_EVIDENCE_LIMIT");
        var result=JsonSerializer.Deserialize<GateResult>(File.ReadAllText(path));
        if(result is not{Gate:"gate-a",Passed:true,InsertionAuthorized:false}||!AllowsNoop(result.Outcome))throw new ExperimentException("GATE_A_REQUIRED");
    }
}
