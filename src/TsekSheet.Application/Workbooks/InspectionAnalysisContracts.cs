namespace TsekSheet.Application.Workbooks;

// Separate from the editable workflow/snapshot contracts. A capture grants no mutation permission.
public sealed record InspectionRegionRequest(Guid SessionId, Guid WorksheetId, int ExpectedRevision, string SourceSha256,
    int StartRow, int EndRow, IReadOnlyList<int> Columns);
public sealed record InspectionRegion(Guid SessionId, Guid WorksheetId, int Revision, string SourceSha256,
    int StartRow, int EndRow, IReadOnlyList<int> Columns, IReadOnlyList<CellInfo> Cells,
    WorksheetScan Scan, IReadOnlyList<int> HiddenRows, long AccountedBytes);
public interface IInspectionAnalysisSession
{
    InspectionRegion CaptureInspectionRegion(InspectionRegionRequest request, CancellationToken token = default);
}
