using System.Security.Cryptography;
using ClosedXML.Excel;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

public sealed class WorkbookEngine : IWorkbookEngine
{
    private readonly WorkbookLimits limits;
    private readonly object gate = new();
    private readonly SemaphoreSlim admissions = new(1, 1);
    private int active;
    private long estimatedBytes;
    internal Func<byte[], byte[]>? CandidateFaultForTesting { get; set; }
    internal CommitFaults? CommitFaultsForTesting { get; set; }
    internal bool StructuralExperimentsEnabled { get; set; }
    internal Action<StructuralStage>? StructuralFaultForTesting { get; set; }
    internal bool StructuralIntegrationExperimentsEnabled { get; set; }
    internal Action<StructuralLifecycleStage>? StructuralLifecycleFaultForTesting { get; set; }

    public WorkbookEngine(WorkbookLimits? limits = null)
    {
        this.limits = limits ?? new();
        if (this.limits.MaxFileBytes is < 1 or > int.MaxValue || this.limits.MaxExpandedBytes < 1 || this.limits.MaxEntries < 1 || this.limits.MaxCells < 1
            || this.limits.MaxWorksheets < 1 || this.limits.MaxStyles < 1 || this.limits.MaxSessions < 1 || this.limits.MaxEstimatedSessionBytes < 1)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    public IWorkbookSession Inspect(string path, CancellationToken cancellationToken = default)
        => new WorkbookInspection().Open(path, cancellationToken);

    public IWorkbookSession Open(string path, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = Path.GetFullPath(path); var root = Path.GetPathRoot(fullPath)!;
            if (fullPath.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType == DriveType.Network)
                throw PackageGuard.Reject("UNSUPPORTED_LOCATION", "Select a workbook on a local drive. Network locations are unsupported.");
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw PackageGuard.Reject("UNSUPPORTED_FORMAT", "Only plain .xlsx workbooks can be opened.");
            var current = root;
            foreach (var segment in fullPath[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw PackageGuard.Reject("UNSUPPORTED_LOCATION", "Workbook paths containing links or redirected folders are unsupported. Select a regular local file.");
            }
            admissions.Wait(cancellationToken);
            try
            {
            lock (gate) if (active >= limits.MaxSessions) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Close another workbook or snapshot before opening more in-memory content.");
            byte[] bytes;
            // Exclusive writer exclusion during the read; never opens the source with write access.
            using (var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (file.Length > limits.MaxFileBytes) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Workbook exceeds the configured compressed-file limit.");
                bytes = new byte[checked((int)file.Length)]; file.ReadExactly(bytes);
            }
            var session = LoadCore(bytes, Path.GetFileName(fullPath), Convert.ToHexString(SHA256.HashData(bytes)), null, null, cancellationToken);
            session.BindOriginalPath(fullPath);
            return session;
            }
            finally { admissions.Release(); }
        }
        catch (OperationCanceledException) { throw; }
        catch (WorkbookException) { throw; }
        catch (InvalidDataException) { throw PackageGuard.Reject("INVALID_PACKAGE", "Workbook is not a valid .xlsx package."); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw PackageGuard.Reject("FILE_UNAVAILABLE", "Workbook could not be read. Check that it exists, is accessible and is not locked by another writer."); }
        catch (Exception) { throw PackageGuard.Reject("INVALID_PACKAGE", "Workbook is invalid or contains an unsupported structure. Supply a plain, unprotected .xlsx copy."); }
    }

    internal WorkbookSession Load(byte[] bytes, string displayName, string hash, Guid? parent, IReadOnlyList<WorksheetInfo>? identities, CancellationToken token)
    {
        admissions.Wait(token);
        try { return LoadCore(bytes, displayName, hash, parent, identities, token); }
        finally { admissions.Release(); }
    }

    internal void CheckSnapshotCapacity()
    {
        lock (gate) if (active >= limits.MaxSessions) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Close another workbook or snapshot before creating a copy.");
    }

    internal IDisposable ReserveSnapshotWork(long estimate)
    {
        lock (gate)
        {
            if (estimate > limits.MaxEstimatedSessionBytes - estimatedBytes) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Insufficient in-memory capacity to prepare an independent snapshot.");
            estimatedBytes += estimate;
        }
        return new Reservation(() => { lock (gate) estimatedBytes -= estimate; });
    }

    private sealed class Reservation(Action release) : IDisposable
    {
        private Action? release = release;
        public void Dispose() => Interlocked.Exchange(ref release, null)?.Invoke();
    }

    private WorkbookSession LoadCore(byte[] bytes, string displayName, string hash, Guid? parent, IReadOnlyList<WorksheetInfo>? identities, CancellationToken token)
    {
        var facts = PackageGuard.Inspect(bytes, limits, token);
        var estimate = checked(bytes.LongLength * 3 + facts.ExpandedBytes * 4 + facts.Cells * 2048L + 4 * 1024 * 1024);
        lock (gate)
        {
            if (active >= limits.MaxSessions || estimate > limits.MaxEstimatedSessionBytes - estimatedBytes) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Close another workbook or snapshot before opening more in-memory content.");
            active++; estimatedBytes += estimate;
        }
        XLWorkbook? workbook = null;
        var stream = new MemoryStream(bytes, false);
        try
        {
            token.ThrowIfCancellationRequested();
            workbook = new XLWorkbook(stream);
            token.ThrowIfCancellationRequested();
            return new WorkbookSession(this, workbook, stream, bytes, facts, displayName, hash, parent, identities, estimate, () => Release(estimate));
        }
        catch { workbook?.Dispose(); stream.Dispose(); Release(estimate); throw; }
    }

    private void Release(long estimate) { lock (gate) { active--; estimatedBytes -= estimate; } }
}
