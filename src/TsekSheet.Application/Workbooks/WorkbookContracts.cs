namespace TsekSheet.Application.Workbooks;

public sealed record CompatibilityFinding(string Code, string Message);
public sealed record WorksheetInfo(Guid Id, string Name, int Order, string Visibility, int LastRow, int LastColumn);
public sealed record WorkbookInfo(Guid SessionId, Guid? ParentSessionId, string DisplayName, string SourceSha256,
    int Revision, bool IsDirty, bool EditingAllowed, string Creator, string Title,
    IReadOnlyList<WorksheetInfo> Worksheets, IReadOnlyList<CompatibilityFinding> Findings, int? LastExportedRevision = null, string? LastExportedSha256 = null);
public sealed record CellFormat(string NumberFormat, int NumberFormatId, bool Bold, bool Italic, string FillColor);
public sealed record CellInfo(int Row, int Column, string Value, string Kind, string? Formula, bool Truncated, CellFormat Format);
public sealed record CellViewport(Guid SessionId, Guid WorksheetId, int StartRow, int StartColumn, int Rows, int Columns, IReadOnlyList<CellInfo> Cells);
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
}

public sealed class WorkbookException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
