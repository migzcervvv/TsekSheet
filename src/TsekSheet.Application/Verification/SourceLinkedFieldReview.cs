using TsekSheet.Application.Documents;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

public sealed record RegisteredSourcePage(LocalDocumentVersion Version, int PageNumber);
public sealed record SourceLinkedDraftField(string Key, string Value, RegisteredSourcePage Source);
public enum SourceLinkedReviewStatus { Active, Invalidated }
public sealed record SourceLinkedReviewSnapshot(RecordReviewSnapshot Review, SourceLinkedReviewStatus Status);

/// <summary>
/// Owns a private review bound to freshly checked registered local sources.
/// Snapshots/typed results are observations, never live mapping authorization.
/// Explicit Verify represents human intent; source availability never verifies.
/// </summary>
public sealed class SourceLinkedFieldReview : IDisposable
{
    private readonly object _gate = new();
    private readonly LocalDocumentSources _sources;
    private FieldReviewSession? _review;
    private bool _invalidated;
    private bool _disposed;
    private bool _busy;

    public SourceLinkedFieldReview(LocalDocumentSources sources, WorkflowProfile profile,
        IReadOnlyList<SourceLinkedDraftField> candidates, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(sources);
        _sources = sources;
        WorkflowProfiles.Validate(profile);
        token.ThrowIfCancellationRequested();
        if (candidates == null || candidates.Count > profile.Fields.Count)
            throw Error("INVALID_REVIEW_INPUT", "Supply at most one draft for each configured field.");
        // Capture caller-owned lists before any I/O callback can change them.
        var drafts = candidates.ToArray();
        if (drafts.Any(d => d == null) || drafts.Select(d => d.Key).Distinct(StringComparer.Ordinal).Count() != drafts.Length)
            throw Error("INVALID_REVIEW_INPUT", "Supply distinct configured draft fields.");
        _review = new(profile, drafts.Select(d => new DraftField(d.Key, d.Value, Evidence(d.Source, token))).ToArray());
        CheckSources(_review, token);
    }

    // Cached, immutable display state only: no I/O and no source authority.
    public SourceLinkedReviewSnapshot Snapshot
    {
        get
        {
            lock (_gate)
            {
                CheckOpen();
                return new(_review!.Snapshot, _invalidated ? SourceLinkedReviewStatus.Invalidated : SourceLinkedReviewStatus.Active);
            }
        }
    }

    public RecordReviewSnapshot Correct(FieldReviewBinding expected, string value, RegisteredSourcePage source,
        CancellationToken token = default) => Execute(expected?.Review!, staged =>
            staged.Correct(expected!, value, Evidence(source, token)), token);

    // Only an explicit command may call this; extraction and confidence cannot.
    public RecordReviewSnapshot Verify(FieldReviewBinding expected, CancellationToken token = default) =>
        Execute(expected?.Review!, staged => staged.Verify(expected!), token);

    public RecordReviewSnapshot Retract(FieldReviewBinding expected, CancellationToken token = default) =>
        Execute(expected?.Review!, staged => staged.Retract(expected!), token);

    public RecordReviewValidation Validate(ReviewVersion expected, CancellationToken token = default) =>
        Execute(expected, staged => staged.Validate(expected), token);

    public TypedReviewRecord Project(ReviewVersion expected, CancellationToken token = default) =>
        Execute(expected, staged => staged.Project(expected), token);

    internal RecordReviewSnapshot CorrectAll(ReviewVersion expected, WorkflowProfile profile,
        IReadOnlyDictionary<string, string> values, RegisteredSourcePage source, CancellationToken token) => Execute(expected, staged =>
    {
        if (values.Count != profile.Fields.Count) throw Error("INVALID_REVIEW_INPUT", "Supply every configured field exactly once.");
        WorkflowValueValidation.Validate(profile.Fields, profile.IdentifierFields, values);
        var evidence = Evidence(source, token);
        foreach (var field in staged.Snapshot.Fields)
            if (field.Value != values[field.Key])
                staged.Correct(staged.Snapshot.Fields.Single(f => f.Key == field.Key).Binding, values[field.Key], evidence);
        return staged.Snapshot;
    }, token);

    internal RecordReviewSnapshot ConfirmAll(ReviewVersion expected, CancellationToken token) => Execute(expected, staged =>
    {
        foreach (var field in staged.Snapshot.Fields)
            if (field.Status != FieldReviewStatus.Verified && field.Source != null)
                staged.Verify(staged.Snapshot.Fields.Single(f => f.Key == field.Key).Binding);
        staged.Project(staged.Snapshot.Version);
        return staged.Snapshot;
    }, token);

    // Draft projection validates values but grants no write authority or verification.
    internal TypedReviewRecord ProjectDraft(ReviewVersion expected, WorkflowProfile profile, CancellationToken token) => Execute(expected, staged =>
    {
        var snapshot = staged.Snapshot;
        var values = WorkflowValueValidation.Validate(profile.Fields, profile.IdentifierFields, snapshot.Fields.ToDictionary(f => f.Key, f => f.Value));
        return new TypedReviewRecord(expected, snapshot.Fields.Select(f => new TypedReviewField(f.Key,
            profile.Fields.Single(p => p.Key == f.Key).Kind, f.Value, values[f.Key].Text, values[f.Key].Number, values[f.Key].Date, f.Source, f.ValueRevision)));
    }, token);

    /// <summary>
    /// Observes verified fields against one explicit worksheet. Source checks
    /// bracket analysis/projection; the last destination check follows source
    /// I/O. The returned preview cannot authorize workbook changes.
    /// </summary>
    public VerifiedMappingPreview PreviewMapping(IWorkbookSession session, MappingDestination destination,
        ReviewVersion expected, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        token.ThrowIfCancellationRequested();
        var profile = VerifiedMapping.RequireProfile(destination);
        VerifiedMapping.RequireReviewProfile(profile, expected);
        VerifiedMapping.RequireDestination(session, destination);
        return Execute(expected, staged =>
        {
            // Initial source I/O may itself outlive the destination binding.
            VerifiedMapping.RequireDestination(session, destination);
            var analysis = WorksheetAnalysisReader.Read(session, destination.WorksheetId,
                destination.Revision, profile, token);
            var record = staged.Project(expected);
            return VerifiedMapping.Map(destination, profile, analysis, record);
        }, token, () => VerifiedMapping.RequireDestination(session, destination));
    }

    public VerifiedPlacementPreview PreviewPlacement(IWorkbookSession session, MappingDestination destination,
        ReviewVersion expected, PlacementChoices? choices = null, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        var profile = VerifiedMapping.RequireProfile(destination);
        VerifiedMapping.RequireReviewProfile(profile, expected);
        VerifiedMapping.RequireDestination(session, destination);
        return Execute(expected, staged =>
        {
            VerifiedMapping.RequireDestination(session, destination);
            var analysis = WorksheetAnalysisReader.Read(session, destination.WorksheetId, destination.Revision, profile, token);
            var record = staged.Project(expected);
            var input = new WorkflowRecord(record.Version.ReviewId.ToString("N"),
                record.Fields.ToDictionary(f => f.Key, f => f.NormalizedText, StringComparer.Ordinal),
                choices?.SectionId, ConfirmNewVendor: choices?.ConfirmNewEntity ?? false);
            var proposal = WorkflowPlanning.Plan(analysis, profile, [input], token);
            return new VerifiedPlacementPreview(VerifiedMapping.Map(destination, profile, analysis, record), proposal);
        }, token, () => VerifiedMapping.RequireDestination(session, destination));
    }

    public void Dispose()
    {
        lock (_gate) { _review = null; _disposed = true; }
        // The registry may be shared by other reviews; its owner releases it.
    }

    private T Execute<T>(ReviewVersion expected, Func<FieldReviewSession, T> operation, CancellationToken token,
        Action? finalCheck = null)
    {
        lock (_gate)
        {
            CheckActive();
            if (_busy) throw Error("SOURCE_REVIEW_BUSY", "Wait for the current source review command to finish.");
            if (expected == null || expected != _review!.Snapshot.Version)
                throw Error("STALE_RECORD_REVIEW", "Refresh this record and profile before continuing review.");
            token.ThrowIfCancellationRequested();
            _busy = true;
            try
            {
                CheckSources(_review!, token);
                var staged = _review!.Fork();
                var result = operation(staged);
                CheckSources(staged, token);
                if (finalCheck != null)
                {
                    finalCheck();
                    // Registration release/disposal during the destination read
                    // must not publish a preview. This performs no source I/O.
                    CheckRegistrations(staged);
                }
                CheckActive();
                token.ThrowIfCancellationRequested();
                _review = staged;
                return result;
            }
            catch (DocumentSourceException)
            {
                // A rejected new/foreign source cannot invalidate an unrelated
                // current review. Failure of an already retained source does.
                if (!_disposed)
                {
                    try { CheckRegistrations(_review!); }
                    catch (DocumentSourceException) { _invalidated = true; }
                }
                throw;
            }
            finally { _busy = false; }
        }
    }

    private SourceEvidence Evidence(RegisteredSourcePage source, CancellationToken token)
    {
        if (source?.Version == null)
            throw Error("INVALID_REVIEW_SOURCE", "Choose an exact registered source and page association.");
        return _sources.IssueEvidence(source.Version, source.PageNumber, token);
    }

    private void CheckSources(FieldReviewSession review, CancellationToken token)
    {
        var evidence = review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Source!).Distinct().ToArray();
        try
        {
            _sources.CheckAvailable();
            foreach (var source in evidence)
            {
                var version = new LocalDocumentVersion(source.DocumentId, source.DocumentRevision, source.DocumentSha256);
                if (_sources.IssueEvidence(version, source.PageNumber, token) != source)
                    throw Error("INVALID_REVIEW_SOURCE", "Refresh the registered source before continuing review.");
                CheckActive();
            }
            // A later read must not hide release/disposal of an earlier source.
            // These checks supplement byte observations; no persistent lock is held.
            CheckRegistrations(review);
        }
        catch (DocumentSourceException)
        {
            _invalidated = true;
            throw;
        }
        CheckActive();
        token.ThrowIfCancellationRequested();
    }

    private void CheckRegistrations(FieldReviewSession review)
    {
        _sources.CheckAvailable();
        foreach (var source in review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Source!).Distinct())
        {
            var current = _sources.Snapshot(source.DocumentId);
            if (current.Status != LocalDocumentStatus.Registered || current.Version !=
                new LocalDocumentVersion(source.DocumentId, source.DocumentRevision, source.DocumentSha256))
                throw LocalDocumentSources.Error("SOURCE_INVALIDATED", "Select the document again before continuing review.");
        }
    }

    private void CheckOpen()
    {
        if (_disposed) throw Error("SOURCE_REVIEW_CLOSED", "Start a review in an open document session.");
    }

    private void CheckActive()
    {
        CheckOpen();
        if (_invalidated) throw Error("SOURCE_REVIEW_INVALIDATED", "Start a new review with freshly selected sources.");
    }

    private static FieldReviewException Error(string code, string message) => new(code, message);
}
