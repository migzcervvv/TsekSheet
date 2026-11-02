using TsekSheet.Application.Verification;

namespace TsekSheet.Application.Documents;

public enum LocalDocumentKind { Pdf, Png, Jpeg }
public enum LocalDocumentStatus { Registered, Invalidated }
public sealed record LocalDocumentVersion(Guid DocumentId, int Revision, string Sha256);
public sealed record LocalDocumentSnapshot(LocalDocumentVersion Version, LocalDocumentKind Kind,
    long ByteLength, LocalDocumentStatus Status);
public sealed record LocalDocumentLimits(int MaxSources = 20, long MaxFileBytes = 32 * 1024 * 1024);
public sealed class DocumentSourceException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

internal sealed record DocumentFileIdentity(uint Volume, ulong Index, long CreationTime);
internal sealed record DocumentObservation(DocumentFileIdentity Identity, string Sha256,
    long ByteLength, LocalDocumentKind Kind, long LastWriteTime);
internal interface ILocalDocumentReader
{
    DocumentObservation Read(string selectedPath, long maxBytes, CancellationToken token);
}

/// <summary>
/// Owns bounded local source associations in memory. Paths enter only from an
/// explicit host file selection, never a frontend command. Returned evidence is
/// an observation, not human verification or a durable capability. Existing
/// supplied-evidence field review APIs remain synthetic/proposal-only. The
/// separate SourceLinkedFieldReview owns live source checks.
/// </summary>
public sealed class LocalDocumentSources : IDisposable
{
    private sealed record Entry(string Path, DocumentObservation Observation, LocalDocumentSnapshot Snapshot);
    private readonly object _gate = new();
    private readonly Dictionary<Guid, Entry> _entries = [];
    private readonly LocalDocumentLimits _limits;
    private readonly ILocalDocumentReader _reader;
    private bool _disposed;

    public LocalDocumentSources(LocalDocumentLimits? limits = null) : this(new WindowsDocumentReader(), limits) { }

    internal LocalDocumentSources(ILocalDocumentReader reader, LocalDocumentLimits? limits = null)
    {
        _reader = reader;
        _limits = limits ?? new();
        if (_limits.MaxSources is < 1 or > 20 || _limits.MaxFileBytes is < 8 or > 32 * 1024 * 1024)
            throw new ArgumentOutOfRangeException(nameof(limits));
    }

    public LocalDocumentSnapshot Register(string selectedPath, CancellationToken token = default)
    {
        lock (_gate)
        {
            CheckOpen();
            token.ThrowIfCancellationRequested();
            if (_entries.Count >= _limits.MaxSources)
                throw Error("SOURCE_LIMIT", "Release a registered source before selecting another document.");
            var observation = _reader.Read(selectedPath, _limits.MaxFileBytes, token);
            token.ThrowIfCancellationRequested();
            CheckOpen();
            var snapshot = new LocalDocumentSnapshot(new(Guid.NewGuid(), 0, observation.Sha256),
                observation.Kind, observation.ByteLength, LocalDocumentStatus.Registered);
            _entries.Add(snapshot.Version.DocumentId, new(selectedPath, observation, snapshot));
            return snapshot;
        }
    }

    public LocalDocumentSnapshot Snapshot(Guid documentId)
    {
        lock (_gate) return Find(documentId).Snapshot;
    }

    internal void CheckAvailable()
    {
        lock (_gate) CheckOpen();
    }

    /// <summary>
    /// Rechecks identity and all bytes before returning path-free evidence.
    /// For PDFs, pageNumber is a requested association only; no page count or
    /// rendering is validated here. Single-image inputs permit page one only.
    /// </summary>
    public SourceEvidence IssueEvidence(LocalDocumentVersion expected, int pageNumber, CancellationToken token = default)
    {
        lock (_gate)
        {
            var entry = Require(expected);
            token.ThrowIfCancellationRequested();
            if (pageNumber is < 1 or > 10000 || (entry.Observation.Kind != LocalDocumentKind.Pdf && pageNumber != 1))
                throw Error("INVALID_SOURCE_PAGE", "Choose a supported one-based source page association.");
            DocumentObservation current;
            try { current = _reader.Read(entry.Path, _limits.MaxFileBytes, token); }
            catch (DocumentSourceException)
            {
                Invalidate(entry);
                throw Error("SOURCE_INVALIDATED", "The selected source is unavailable or changed. Select it again before review.");
            }
            token.ThrowIfCancellationRequested();
            Require(expected);
            if (current != entry.Observation)
            {
                Invalidate(entry);
                throw Error("SOURCE_INVALIDATED", "The selected source changed or was replaced. Select it again before review.");
            }
            return new(expected.DocumentId, current.Sha256, expected.Revision, pageNumber);
        }
    }

    public void Release(LocalDocumentVersion expected)
    {
        lock (_gate)
        {
            var entry = Find(expected?.DocumentId ?? Guid.Empty);
            if (entry.Snapshot.Version != expected)
                throw Error("STALE_SOURCE_VERSION", "Refresh the source observation before releasing it.");
            _entries.Remove(expected.DocumentId);
        }
    }

    public void Dispose()
    {
        lock (_gate) { _entries.Clear(); _disposed = true; }
    }

    private Entry Require(LocalDocumentVersion expected)
    {
        var entry = Find(expected?.DocumentId ?? Guid.Empty);
        if (entry.Snapshot.Version != expected)
            throw Error("STALE_SOURCE_VERSION", "Refresh the registered source version before continuing.");
        if (entry.Snapshot.Status != LocalDocumentStatus.Registered)
            throw Error("SOURCE_INVALIDATED", "Select the document again before continuing review.");
        return entry;
    }

    private Entry Find(Guid id)
    {
        CheckOpen();
        return _entries.TryGetValue(id, out var entry) ? entry
            : throw Error("SOURCE_NOT_REGISTERED", "Select a local document in this source session.");
    }

    private void Invalidate(Entry entry)
    {
        Require(entry.Snapshot.Version);
        var snapshot = entry.Snapshot with
        {
            Version = entry.Snapshot.Version with { Revision = checked(entry.Snapshot.Version.Revision + 1) },
            Status = LocalDocumentStatus.Invalidated
        };
        _entries[snapshot.Version.DocumentId] = entry with { Snapshot = snapshot };
    }

    private void CheckOpen()
    {
        if (_disposed) throw Error("SOURCE_SESSION_CLOSED", "Select sources in an open document session.");
    }
    internal static DocumentSourceException Error(string code, string message) => new(code, message);
}
