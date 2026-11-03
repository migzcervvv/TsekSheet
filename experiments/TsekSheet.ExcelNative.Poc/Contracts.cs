namespace TsekSheet.ExcelNative.Poc;

// Experimental contracts only. No production project references this assembly.
public sealed record ExperimentRequest(int Version, string Operation, string Root, string? SourceSha256 = null);
public sealed record ExcelOwnership(int ProcessId, long StartUtcTicks, long WindowHandle, int SessionId, int[] ExistingProcessIds);
public sealed record WorkerResult(string Status, string Code, bool OwnershipVerified, bool CleanupConfirmed,
    bool SourceUnchanged, bool ExactWorkingCopy, bool CandidateReopened, ExcelOwnership? Ownership,
    string Stage, long ElapsedMilliseconds, long ExcelPeakWorkingSet, string? ExcelVersion = null, string? ExcelBuild = null,
    IReadOnlyDictionary<string, long>? Timings = null, bool InstanceSettingsRestored = false);
public sealed record SupervisorResult(WorkerResult? Worker, bool OutcomeUnknown, string Code, int? ExitCode, long ElapsedMilliseconds, long PeakWorkerBytes);

public interface IExperimentalWorkbookProvider
{
    WorkerResult Execute(ExperimentRequest request);
}

public sealed class ExperimentException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
