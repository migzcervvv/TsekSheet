namespace TsekSheet.Workbooks;

// Admission bounds, not a hard CLR heap quota. One engine shares a budget across sessions/snapshots.
public sealed record WorkbookLimits
{
    public long MaxFileBytes { get; init; } = 16 * 1024 * 1024;
    public long MaxExpandedBytes { get; init; } = 64 * 1024 * 1024;
    public int MaxEntries { get; init; } = 1024;
    public int MaxCells { get; init; } = 50000;
    public int MaxWorksheets { get; init; } = 32;
    public int MaxStyles { get; init; } = 4096;
    public int MaxSessions { get; init; } = 4;
    public long MaxEstimatedSessionBytes { get; init; } = 256 * 1024 * 1024;
}
