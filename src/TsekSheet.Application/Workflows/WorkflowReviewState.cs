namespace TsekSheet.Application.Workflows;

// Each lifecycle fact is distinct. Planning/preflight can never imply approval or adoption.
public sealed record WorkflowReviewState(string Planner, string Preflight, string Generation, string Oracle,
    string Approval, string WorkingCopyAdoption, string Integration, string Export)
{
    public static WorkflowReviewState Planning(bool ready, string structuralStatus) => new(
        ready ? "resolved" : "choices-required", structuralStatus, "not-generated", "not-run", "not-approved",
        "not-adopted", "production-disabled", "unmerged-changes-excluded");
}
