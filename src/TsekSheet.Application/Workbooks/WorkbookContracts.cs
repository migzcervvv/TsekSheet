namespace TsekSheet.Application.Workbooks;

public sealed record CompatibilityFinding(string Code, string Message);
public sealed record WorksheetInfo(Guid Id, string Name, int Order, string Visibility, int LastRow, int LastColumn);
public sealed record WorkbookInfo(Guid SessionId, Guid? ParentSessionId, string DisplayName, string SourceSha256,
    int Revision, bool IsDirty, bool EditingAllowed, string Creator, string Title,
    IReadOnlyList<WorksheetInfo> Worksheets, IReadOnlyList<CompatibilityFinding> Findings, int? LastExportedRevision = null, string? LastExportedSha256 = null,
    WorkbookCompatibilityReport? Compatibility = null);
public sealed record CellFormat(string NumberFormat, int NumberFormatId, bool Bold, bool Italic, string FillColor,
    int? EffectiveNumberFormatId = null, string? FormatResolution = null, string? FormatClassification = null);
public sealed record CellInterpretation(string RawValue, string DisplayValue, string ResolvedType, string Confidence, string Warning, string FormulaStatus,
    string Classification = "", string FormatResolution = "");
public sealed record CellInfo(int Row, int Column, string Value, string Kind, string? Formula, bool Truncated, CellFormat Format, CellInterpretation? Interpretation = null);
public sealed record CellViewport(Guid SessionId, Guid WorksheetId, int StartRow, int StartColumn, int Rows, int Columns, IReadOnlyList<CellInfo> Cells, WorksheetScan? Scan = null);
public sealed record WorkbookCapability(string Operation, bool Allowed, string Reason);
public sealed record PackageCategory(string Category, int Count);
public sealed record WorkbookCompatibilityReport(long FileBytes, long CompressedPartBytes, long ExpandedPartBytes, int PartCount, int DefinedNames,
    int Relationships, bool ExternalLinks, IReadOnlyList<PackageCategory> Categories, IReadOnlyList<WorkbookCapability> Capabilities, string CountStatus);
public sealed record WorksheetScan(string DeclaredDimension, string DimensionReliability, long StoredCells, long NonemptyValues, long Formulas, long StyledCells,
    int LastRow, int LastColumn, bool PotentialInflation, IReadOnlyList<string> Warnings, long NumericValues = 0, int LastRowElement = 0,
    int? MeaningfulLastRow = null, int? MeaningfulLastColumn = null);

public interface IWorkbookSession : IDisposable
{
    WorkbookInfo Info { get; }
    CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns);
    IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default);
}

public interface IWorkbookEngine
{
    IWorkbookSession Open(string path, CancellationToken cancellationToken = default);
    IWorkbookSession Inspect(string path, CancellationToken cancellationToken = default)
        => throw new WorkbookException("INSPECTION_UNAVAILABLE", "Read-only package inspection is unavailable.");
}

public sealed class WorkbookException(string code, string message, IReadOnlyList<CompatibilityFinding>? details = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyList<CompatibilityFinding> Details { get; } = details ?? [];
}
