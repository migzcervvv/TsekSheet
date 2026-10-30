namespace TsekSheet.Application.Workbooks;

public sealed record CompatibilityFinding(string Code, string Message);
public sealed record WorksheetInfo(Guid Id, string Name, int Order, string Visibility, int LastRow, int LastColumn);
public sealed record WorkbookInfo(Guid SessionId, Guid? ParentSessionId, string DisplayName, string SourceSha256,
    int Revision, bool IsDirty, bool EditingAllowed, string Creator, string Title,
    IReadOnlyList<WorksheetInfo> Worksheets, IReadOnlyList<CompatibilityFinding> Findings, int? LastExportedRevision = null, string? LastExportedSha256 = null,
    WorkbookCompatibilityReport? Compatibility = null);
public sealed record CellFormat(string NumberFormat, int NumberFormatId, bool Bold, bool Italic, string FillColor);
public sealed record CellInterpretation(string RawValue, string DisplayValue, string ResolvedType, string Confidence, string Warning, string FormulaStatus);
public sealed record CellInfo(int Row, int Column, string Value, string Kind, string? Formula, bool Truncated, CellFormat Format, CellInterpretation? Interpretation = null);
public sealed record CellViewport(Guid SessionId, Guid WorksheetId, int StartRow, int StartColumn, int Rows, int Columns, IReadOnlyList<CellInfo> Cells, WorksheetScan? Scan = null);
public sealed record WorkbookCapability(string Operation, bool Allowed, string Reason);
public sealed record PackageCategory(string Category, int Count);
public sealed record WorkbookCompatibilityReport(long FileBytes, long CompressedPartBytes, long ExpandedPartBytes, int PartCount, int DefinedNames,
    int Relationships, bool ExternalLinks, IReadOnlyList<PackageCategory> Categories, IReadOnlyList<WorkbookCapability> Capabilities, string CountStatus);
public sealed record WorksheetScan(string DeclaredDimension, string DimensionReliability, long StoredCells, long NonemptyValues, long Formulas, long StyledCells,
    int LastRow, int LastColumn, bool PotentialInflation, IReadOnlyList<string> Warnings);
public sealed record CellEdit(Guid WorksheetId, int Row, int Column, string Kind, string Value, int ExpectedRevision);
public sealed record CellChange(Guid WorksheetId, int Row, int Column, string Kind, string Value, int Revision);
public sealed record WorkingCopyInfo(Guid SessionId, Guid SourceWorksheetId, int BaseRevision, int Revision, bool IsDirty, IReadOnlyList<CellChange> Changes);
public sealed record SaveReceipt(string Status, Guid SessionId, int SavedRevision, bool SourceStillDirty, bool Rebound, bool CancellationAfterCommit, string DestinationSha256, string DisplayName);
public enum WorksheetIntegrationOperation { Merge, Replace, CreateNew }
public sealed record WorksheetIntegrationRequest(Guid WorkingCopyId, Guid DestinationWorksheetId,
    int ExpectedCopyRevision, int ExpectedDestinationRevision, int ExpectedBaseRevision,
    WorksheetIntegrationOperation Operation = WorksheetIntegrationOperation.Merge);
public sealed record WorksheetIntegrationPreview(WorksheetIntegrationRequest Request, string DestinationName,
    int PendingCells, bool CanApply, IReadOnlyList<CompatibilityFinding> Conflicts);
public sealed record WorksheetIntegrationResult(WorkbookInfo Workbook, int AppliedCells);

public interface IWorkbookSession : IDisposable
{
    WorkbookInfo Info { get; }
    CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns);
    IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default);
    IWorkbookSession CreateWorkingCopy(Guid worksheetId, int expectedRevision, CancellationToken cancellationToken = default);
    WorkbookInfo Edit(CellEdit edit, CancellationToken cancellationToken = default);
    WorkingCopyInfo? WorkingCopy { get; }
    TsekSheet.Application.Workflows.StructuralCandidateReview PrepareCandidateReview(IWorkbookSession destination, TsekSheet.Application.Workflows.WorkflowPlanRequest request, int expectedDestinationRevision, CancellationToken token = default)
        => throw new WorkbookException("CANDIDATE_REVIEW_UNAVAILABLE", "Candidate review is unavailable.");
    TsekSheet.Application.Workflows.StructuralCandidateReview ApproveCandidateReview(IWorkbookSession destination, TsekSheet.Application.Workflows.CandidateReviewApproval request, CancellationToken token = default)
        => throw new WorkbookException("CANDIDATE_REVIEW_UNAVAILABLE", "Candidate approval is unavailable.");
    void CancelCandidateReview(Guid candidateId, int expectedRevision) => throw new WorkbookException("CANDIDATE_REVIEW_UNAVAILABLE", "Candidate cancellation is unavailable.");
    TsekSheet.Application.Workflows.WorkflowPlan InspectStructuralInsertion(TsekSheet.Application.Workflows.WorkflowPlan plan, CancellationToken token = default) => plan;
    WorkbookInfo InsertWorkflowRows(TsekSheet.Application.Workflows.WorkflowPlan plan, bool approved, bool duplicatesReviewed, CancellationToken cancellationToken = default)
        => TsekSheet.Application.Workflows.WorkflowPlanning.RejectStructuralInsertion(this, plan, approved, duplicatesReviewed, cancellationToken);
    WorksheetIntegrationPreview PreviewIntegration(IWorkbookSession copy, WorksheetIntegrationRequest request, CancellationToken cancellationToken = default)
        => throw new WorkbookException("UNSUPPORTED_OPERATION", "Worksheet integration is unavailable.");
    WorksheetIntegrationResult Integrate(IWorkbookSession copy, WorksheetIntegrationRequest request, CancellationToken cancellationToken = default)
        => throw new WorkbookException("UNSUPPORTED_OPERATION", "Worksheet integration is unavailable.");
    SaveReceipt SaveAsNew(string destination, int expectedRevision, CancellationToken cancellationToken = default);
}

public interface IWorkbookEngine
{
    IWorkbookSession Open(string path, CancellationToken cancellationToken = default);
    IWorkbookSession Inspect(string path, CancellationToken cancellationToken = default)
        => throw new WorkbookException("INSPECTION_UNAVAILABLE", "Read-only package inspection is unavailable.");
}

public sealed class WorkbookException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
