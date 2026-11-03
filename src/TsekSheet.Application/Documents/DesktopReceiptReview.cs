using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Documents;

public sealed record ReceiptSource(LocalDocumentSnapshot Document, string DisplayName);
public sealed record ReceiptField(string Key, string Value, int ValueRevision, string Status);
public sealed record ReceiptState(ReceiptSource? Source, ReviewVersion? Version, int PageNumber,
    string Status, IReadOnlyList<ReceiptField> Fields);

// One page-owned manual review. Client values never supply source/verification authority.
public sealed class DesktopReceiptReview : IDisposable
{
    private readonly object gate = new();
    private readonly LocalDocumentSources sources = new();
    private ReceiptSource? source;
    private SourceLinkedFieldReview? review;
    private int page = 1;
    private bool disposed;

    public ReceiptState State
    {
        get
        {
            lock (gate)
            {
                CheckOpen();
                var snapshot = review?.Snapshot;
                return new(source, snapshot?.Review.Version, page, snapshot?.Status.ToString() ?? "NotStarted",
                    snapshot?.Review.Fields.Select(f => new ReceiptField(f.Key, f.Value, f.ValueRevision, f.Status.ToString())).ToArray() ?? []);
            }
        }
    }

    public ReceiptState Register(string path, CancellationToken token)
    {
        lock (gate)
        {
            CheckOpen();
            if (source != null) throw new FieldReviewException("RECEIPT_ALREADY_OPEN", "Close the current receipt before selecting another source.");
            var document = sources.Register(path, token);
            source = new(document, Path.GetFileName(path));
            return State;
        }
    }

    public ReceiptState Start(string profileId, int pageNumber, CancellationToken token)
    {
        lock (gate)
        {
            CheckOpen();
            if (review != null) throw new FieldReviewException("REVIEW_ALREADY_STARTED", "Close the receipt before changing its profile or page association.");
            if (source == null) throw new FieldReviewException("SOURCE_REQUIRED", "Select a local receipt first.");
            sources.IssueEvidence(source.Document.Version, pageNumber, token);
            review = new(sources, WorkflowProfiles.Get(profileId), [], token);
            page = pageNumber;
            return State;
        }
    }

    public ReceiptState Change(string action, ReviewVersion expected, string key, int valueRevision, string? value, CancellationToken token)
    {
        lock (gate)
        {
            var current = Require(expected);
            var field = current.Snapshot.Review.Fields.SingleOrDefault(f => f.Key == key);
            if (field == null || field.ValueRevision != valueRevision)
                throw new FieldReviewException("STALE_FIELD_REVIEW", "Refresh the field before continuing review.");
            if (action == "correct") current.Correct(field.Binding, value!, new(source!.Document.Version, page), token);
            else if (action == "verify") current.Verify(field.Binding, token);
            else if (action == "retract") current.Retract(field.Binding, token);
            else throw new FieldReviewException("INVALID_REQUEST", "Unknown review action.");
            return State;
        }
    }

    public VerifiedPlacementPreview Preview(ReviewVersion expected, Workbooks.IWorkbookSession session,
        MappingDestination destination, PlacementChoices choices, CancellationToken token)
    {
        lock (gate) return Require(expected).PreviewPlacement(session, destination, expected, choices, token);
    }

    private SourceLinkedFieldReview Require(ReviewVersion expected)
    {
        CheckOpen();
        if (review == null || review.Snapshot.Review.Version != expected)
            throw new FieldReviewException("STALE_RECORD_REVIEW", "Refresh the receipt before continuing review.");
        return review;
    }

    public async Task<SourceRenderedPage> RenderAsync(ILocalDocumentRenderer renderer,LocalDocumentVersion expected,int pageNumber,CancellationToken token)
    {
        LocalDocumentKind kind;
        lock(gate)
        {
            CheckOpen();
            if(source==null || source.Document.Version!=expected)throw new DocumentSourceException("STALE_SOURCE_VERSION","Refresh the registered receipt before rendering.");
            kind=source.Document.Kind;
        }
        var bytes=await Task.Run(()=>sources.Capture(expected,token),token);
        var result=await renderer.RenderAsync(bytes,kind,pageNumber,token);
        sources.IssueEvidence(expected,pageNumber,token);
        lock(gate)
        {
            CheckOpen();
            if(source?.Document.Version!=expected)throw new DocumentSourceException("SOURCE_NOT_REGISTERED","Receipt was closed or replaced during rendering.");
            var current=sources.Snapshot(expected.DocumentId);
            if(current.Status!=LocalDocumentStatus.Registered || current.Version!=expected)
                throw new DocumentSourceException("SOURCE_INVALIDATED","Receipt source was invalidated during rendering.");
            token.ThrowIfCancellationRequested();
            return new(expected,result);
        }
    }

    public void Close()
    {
        lock (gate)
        {
            CheckOpen(); review?.Dispose(); review = null;
            if (source != null) sources.Release(sources.Snapshot(source.Document.Version.DocumentId).Version);
            source = null; page = 1;
        }
    }
    public void Dispose() { lock (gate) { review?.Dispose(); sources.Dispose(); disposed = true; } }
    private void CheckOpen() { if (disposed) throw new FieldReviewException("SOURCE_SESSION_CLOSED", "Receipt session is closed."); }
}
