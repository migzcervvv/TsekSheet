using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Documents;

public sealed record QueueReceipt(string Id, string DisplayName, string Status);
public sealed record ReceiptQueueState(string? ActiveId, IReadOnlyList<QueueReceipt> Items, ReceiptState Receipt);

// Page-owned references only: no copies of original uploads and no persistence.
public sealed class ReceiptQueue(Func<string, WorkflowProfile>? profiles = null) : IDisposable
{
    private sealed record Entry(string Id, DesktopReceiptReview Review) { public string Status { get; set; } = "Pending"; }
    private readonly List<Entry> items = [];
    private readonly object gate = new();
    private readonly DesktopReceiptReview single = new(profiles);
    private int active;
    public DesktopReceiptReview Active { get { lock (gate) return items.Count == 0 ? single : items[active].Review; } }
    public ReceiptQueueState State { get { lock (gate) return new(items.Count == 0 ? null : items[active].Id,
        items.Select(e => new QueueReceipt(e.Id, e.Review.State.Source!.DisplayName, e.Status)).ToArray(), Active.State); } }

    public ReceiptQueueState Add(IReadOnlyList<string> paths, CancellationToken token)
    {
        lock (gate)
        {
            if (single.State.Source != null || paths.Count is < 1 or > 100 || items.Count + paths.Count > 100)
                throw new FieldReviewException("QUEUE_LIMIT", "Close the single receipt first; a queue allows up to 100 receipts.");
            var staged = new List<Entry>();
            try
            {
                var hashes = items.Select(e => e.Review.State.Source!.Document.Version.Sha256).ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    var receipt = new DesktopReceiptReview(profiles);
                    staged.Add(new(Guid.NewGuid().ToString("N"), receipt));
                    var state = receipt.Register(path, token);
                    if (!hashes.Add(state.Source!.Document.Version.Sha256))
                        throw new FieldReviewException("DUPLICATE_RECEIPT", "The selected files contain a receipt already in this queue (same SHA-256).");
                }
            }
            catch { foreach (var entry in staged) entry.Review.Dispose(); throw; }
            items.AddRange(staged); return State;
        }
    }

    public ReceiptQueueState Navigate(string expectedId, string targetId, bool skip)
    {
        lock (gate)
        {
            Require(expectedId);
            Active.CheckMutationAllowed();
            var target = items.FindIndex(e => e.Id == targetId);
            if (target < 0) throw new FieldReviewException("UNKNOWN_RECEIPT", "Choose a receipt in this queue.");
            if (skip && items[active].Status == "Pending") items[active].Status = "Skipped";
            active = target; return State;
        }
    }

    public void RequireEditable()
    {
        lock (gate)
        {
            if (items.Count > 0 && items[active].Status == "Inserted")
                throw new FieldReviewException("ALREADY_INSERTED", "This receipt was already inserted. Review its highlighted Excel row.");
        }
    }

    public ReceiptQueueState Complete(string expectedId) => Complete(expectedId, excelOwner: null);

    internal ReceiptQueueState Complete(string expectedId, DesktopReceiptReview? excelOwner)
    {
        lock (gate)
        {
            Require(expectedId);
            if (excelOwner is null) Active.CheckMutationAllowed();
            else if (!ReferenceEquals(Active, excelOwner)) throw new FieldReviewException("STALE_QUEUE", "The active receipt changed.");
            items[active].Status = "Inserted";
            for (var offset = 1; offset <= items.Count; offset++)
            {
                var next = (active + offset) % items.Count;
                if (items[next].Status == "Pending") { active = next; break; }
            }
            return State;
        }
    }
    private void Require(string id)
    {
        if (items.Count == 0 || items[active].Id != id) throw new FieldReviewException("STALE_QUEUE", "Refresh the queue before changing receipts.");
    }

    // Called after the Excel semaphore is acquired. The synchronous start takes
    // the receipt lease before navigation can change the active receipt.
    internal Task<T> StartExcel<T>(DesktopReceiptReview owner, string? expectedId, Func<Task<T>> start)
    {
        lock (gate)
        {
            if (expectedId is not null) Require(expectedId);
            if (!ReferenceEquals(Active, owner))
                throw new FieldReviewException("STALE_QUEUE", "The active receipt changed. Request a fresh preview.");
            RequireEditable();
            return start();
        }
    }

    public void Dispose() { lock (gate) { single.Dispose(); foreach (var e in items) e.Review.Dispose(); } }
}

public sealed partial class DesktopReceiptReview
{
    public ReceiptState ChangeProfile(ReviewVersion expected, string profileId, IReadOnlyDictionary<string, string> values, CancellationToken token)
    {
        lock (gate)
        {
            var current = Require(expected);
            CheckMutationAllowed();
            var previous = Profile(expected.ProfileId);
            if (values.Count != previous.Fields.Count || previous.Fields.Any(f => !values.ContainsKey(f.Key) || values[f.Key] is null || values[f.Key].Length > f.MaxLength))
                throw new FieldReviewException("INVALID_REVIEW_INPUT", "Supply each current draft field within its configured length.");
            var profile = Profile(profileId);
            var sourcePage = new RegisteredSourcePage(source!.Document.Version, page);
            sources.IssueEvidence(source.Document.Version, page, token);
            var candidates = values.Where(f => f.Value.Length > 0 && profile.Fields.Any(p => p.Key == f.Key))
                .Select(f => new SourceLinkedDraftField(f.Key, f.Value, sourcePage)).ToArray();
            var replacement = new SourceLinkedFieldReview(sources, profile, candidates, token);
            current.Dispose(); review = replacement; return State;
        }
    }

    public ReceiptState Prepare(ReviewVersion expected, IReadOnlyDictionary<string, string> values, CancellationToken token)
    {
        lock (gate)
        {
            var current = Require(expected);
            CheckMutationAllowed();
            current.CorrectAll(expected, Profile(expected.ProfileId), values, new(source!.Document.Version, page), token);
            return State;
        }
    }

    public ReceiptState ConfirmAll(ReviewVersion expected, CancellationToken token)
    {
        lock (gate)
        {
            var current = Require(expected);
            CheckMutationAllowed();
            current.ConfirmAll(expected, token);
            return State;
        }
    }

    public async Task<T> WithDraftExcelAsync<T>(ReviewVersion expected, Func<TypedReviewRecord, Task<T>> operation, CancellationToken token)
    {
        TypedReviewRecord record;
        lock (gate)
        {
            var current = Require(expected);
            CheckMutationAllowed();
            record = current.ProjectDraft(expected, Profile(expected.ProfileId), token);
            // Even an entirely blank optional field must not bypass the receipt's identity check.
            RecheckExcelSource(token);
            excelOperationActive = true;
        }
        try { return await operation(record).ConfigureAwait(false); }
        finally { lock (gate) { excelOperationActive = false; if (disposeAfterExcel) Dispose(); } }
    }
}
