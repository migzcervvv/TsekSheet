namespace TsekSheet.Application.Workflows;

// Each lifecycle fact is distinct. Planning/preflight can never imply approval or adoption.
public sealed record WorkflowReviewState(string Planner, string Preflight, string Generation, string Oracle,
    string Approval, string WorkingCopyAdoption, string Integration, string Export)
{
    public static WorkflowReviewState Planning(bool ready, string structuralStatus) => new(
        ready ? "resolved" : "choices-required", structuralStatus, "not-generated", "not-run", "not-approved",
        "not-adopted", "production-disabled", "unmerged-changes-excluded");
}

public sealed record StructuralOperationReview(int InsertionBoundary, int RowCount, int TemplateRow,
    int FirstDataRow, int SubtotalRow, string SubtotalPolicy);

public sealed record StructuralCandidateReview(Guid CandidateId, string ReviewSha256, string CandidateSha256,
    string ManifestSha256, string WorkbookName, string WorksheetName, WorkflowPlan Proposal,
    WorkflowReviewState Lifecycle, IReadOnlyList<string> FormulaImpacts, IReadOnlyList<string> StyleAssumptions,
    StructuralOperationReview Operation, IReadOnlyList<string> RejectionReasons, int IntendedWorkingCopyRevision, int IntendedDestinationRevision)
{
    public Guid? DestinationSessionId { get; init; }
    public int ExpectedDestinationRevision { get; init; }
    public bool ReviewOnly { get; init; }
    public CandidateOracleReview? OracleReport { get; init; }
}
public sealed record CandidateOracleFinding(string Category, string Location, string Reason);
public sealed record CandidateOracleReview(bool Accepted, IReadOnlyList<string> AllowedChanges, IReadOnlyList<CandidateOracleFinding> Findings);
public sealed record CandidateReviewApproval(Guid SessionId, Guid DestinationSessionId, Guid WorksheetId, Guid CandidateId,
    string ReviewSha256, string CandidateSha256, string ManifestSha256, string SourceSha256, int ExpectedCopyRevision,
    int ExpectedDestinationRevision, string ProfileId, string ProfileHash, bool DuplicatesReviewed);

public sealed record StructuralApproval(Guid CandidateId, string ReviewSha256, string CandidateSha256,
    int ExpectedCopyRevision, bool DuplicatesReviewed);

public sealed record StructuralIntegrationRequest(Guid CandidateId, string ReviewSha256, string CandidateSha256,
    Guid WorkingCopyId, Guid WorksheetId, int ExpectedCopyRevision, int ExpectedDestinationRevision, int ExpectedBaseRevision);
