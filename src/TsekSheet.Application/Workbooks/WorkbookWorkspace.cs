using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Workbooks;

public sealed record WorkbookOpenResult(string Status, WorkbookInfo? Workbook = null);
public sealed record WorkbookSaveResult(string Status, SaveReceipt? Receipt = null);
public sealed record WorkingCopySummary(Guid SessionId, Guid SourceWorksheetId, int BaseRevision, int Revision, bool IsDirty, int ChangeCount);

// One active workbook and at most two isolated working copies per page.
public sealed class WorkbookWorkspace(IWorkbookEngine engine, Func<CancellationToken, Task<string?>> selectWorkbook, Func<CancellationToken, Task<string?>>? selectSaveDestination = null,
    Func<CancellationToken, Task<string?>>? selectSectionProfile = null) : IDisposable
{
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private IWorkbookSession? active;
    private readonly Dictionary<Guid, IWorkbookSession> copies = [];
    private bool disposed;
    private SectionReview? sectionReview;
    private CancellationTokenSource? reviewOperation;
    private Guid? reviewSheet;
    public int ReviewDecisionCount { get { lock (gate) return sectionReview?.Snapshot.Decisions.Count ?? 0; } }

    public Task<SectionReviewSnapshot?> StartSectionReviewAsync(Guid sessionId, Guid worksheetId, int revision) => Run(async token =>
    {
        IWorkbookSession session; CancellationTokenSource cts;
        lock (gate)
        {
            session = RequireSession(sessionId);
            if (!ReferenceEquals(session, active) || session.Info.Compatibility == null) throw new WorkbookException("READ_ONLY_INSPECTION", "Section review requires an inspection session.");
            ClearReviewLocked(); reviewSheet = worksheetId;
            cts = CancellationTokenSource.CreateLinkedTokenSource(token); reviewOperation = cts;
        }
        try
        {
            if (selectSectionProfile == null) throw new WorkbookException("PROFILE_REQUIRED", "Section review requires a configured read-only profile.");
            var json = await selectSectionProfile(cts.Token);
            if (json == null) return null;
            var profile = ReadOnlySectionAnalysis.ParseProfile(json);
            var candidate = await Task.Run(() => new SectionReview(session, worksheetId, revision, profile, cts.Token), cts.Token);
            lock (gate) { cts.Token.ThrowIfCancellationRequested(); ObjectDisposedException.ThrowIf(disposed, this); sectionReview = candidate; }
            return candidate.Snapshot;
        }
        finally { lock (gate) { if (ReferenceEquals(reviewOperation, cts)) reviewOperation = null; } cts.Dispose(); }
    });

    public Task<SectionReviewSnapshot> DecideSectionAsync(SectionReviewRequest request) => Run(token => Task.Run(() =>
    {
        lock (gate) { var review = RequireReview(request.Binding); return review.Decide(request, token); }
    }, token));
    public Task<SectionReviewSnapshot> RetractSectionAsync(SectionReviewRetraction request) => Run(token => Task.Run(() =>
    {
        lock (gate) { var review = RequireReview(request.Binding); return review.Retract(request, token); }
    }, token));
    private SectionReview RequireReview(SectionReviewBinding binding)
    {
        var review = sectionReview ?? throw new WorkbookException("SECTION_REVIEW", "No active section review.");
        review.ValidateCurrent(RequireSession(binding.SessionId)); return review;
    }
    // Cancellation is independent of the serialized work queue, so a capture can be interrupted.
    public string CancelSectionReview(Guid sessionId, Guid worksheetId)
    {
        lock (gate) { RequireSession(sessionId); if (reviewSheet == worksheetId) ClearReviewLocked(); return "cancelled"; }
    }
    private void ClearReviewLocked() { reviewOperation?.Cancel(); sectionReview = null; reviewSheet = null; }

    private async Task<T> Run<T>(Func<CancellationToken, Task<T>> action)
    {
        CancellationToken token; lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); token = lifetime.Token; }
        if (!await operation.WaitAsync(0, token)) throw new WorkbookException("BUSY", "A workbook operation is already running.");
        try { return await action(token); }
        finally { operation.Release(); }
    }

    public Task<WorkbookOpenResult> OpenAsync(bool inspectOnly = false) => Run(async token =>
    {
        lock (gate) if (HasPendingChangesLocked()) throw new WorkbookException("UNSAVED_CHANGES", "Discard pending changes before opening another workbook.");
        var path = await selectWorkbook(token);
        if (path == null) return new WorkbookOpenResult("cancelled");
        var candidate = await Task.Run(() => inspectOnly ? engine.Inspect(path, token) : engine.Open(path, token), token);
        IWorkbookSession? previous;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); ClearReviewLocked(); previous = active; active = candidate; foreach (var copy in copies.Values) copy.Dispose(); copies.Clear(); }
        }
        catch { candidate.Dispose(); throw; }
        previous?.Dispose();
        return new WorkbookOpenResult("opened", candidate.Info);
    });

    public Task<CellViewport> ReadAsync(Guid sessionId, Guid worksheetId, int row, int column, int rows, int columns) => Run(token => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        IWorkbookSession session; lock (gate) { session = RequireSession(sessionId); if (reviewSheet != null && reviewSheet != worksheetId) ClearReviewLocked(); }
        return session.ReadViewport(worksheetId, row, column, rows, columns);
    }, token));

    public Task<WorkbookSaveResult> SaveAsNewAsync(Guid sessionId, int expectedRevision) => Run(async token =>
    {
        IWorkbookSession session;
        lock (gate)
        {
            session = RequireSession(sessionId);
            if (!ReferenceEquals(session, active)) throw new WorkbookException("WORKING_COPY_SAVE_UNSUPPORTED", "Save As exports the original session only. Working-copy edits are excluded.");
            if (session.Info.Revision != expectedRevision) throw new WorkbookException("STALE_REVISION", "Refresh the workbook before saving.");
            if (!session.Info.EditingAllowed) throw new WorkbookException("EDIT_UNSUPPORTED", "This workbook is available for inspection only.");
        }
        if (selectSaveDestination == null) throw new WorkbookException("UNSUPPORTED_OPERATION", "Save As is unavailable in this host.");
        var path = await selectSaveDestination(token);
        if (path == null) return new WorkbookSaveResult("cancelled");
        var receipt = await Task.Run(() => session.SaveAsNew(path, expectedRevision, token), token);
        return new WorkbookSaveResult("saved", receipt);
    });

    public Task<WorkingCopySummary> CreateWorkingCopyAsync(Guid sessionId, Guid worksheetId, int expectedRevision) => Run(token => Task.Run(() =>
    {
        IWorkbookSession source;
        lock (gate)
        {
            source = RequireSession(sessionId);
            if (!ReferenceEquals(source, active)) throw new WorkbookException("UNSUPPORTED_OPERATION", "Create a working copy from the original workbook.");
            if (copies.Count >= 2) throw new WorkbookException("LIMIT_EXCEEDED", "Close the workbook to release its two working copies.");
        }
        var copy = source.CreateWorkingCopy(worksheetId, expectedRevision, token);
        try
        {
            token.ThrowIfCancellationRequested();
            lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); copies.Add(copy.Info.SessionId, copy); }
            return Summarize(copy);
        }
        catch { copy.Dispose(); throw; }
    }, token));

    public Task<WorkingCopySummary> EditWorkingCopyAsync(Guid sessionId, CellEdit edit) => Run(token => Task.Run(() =>
    {
        IWorkbookSession session; lock (gate) session = RequireSession(sessionId);
        if (session.WorkingCopy == null) throw new WorkbookException("READ_ONLY_WORKBOOK", "The desktop editor changes working copies only.");
        session.Edit(edit, token); return Summarize(session);
    }, token));

    public Task<TsekSheet.Application.Workflows.WorkflowPlan> PlanRecordsAsync(TsekSheet.Application.Workflows.WorkflowPlanRequest request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession session; lock (gate) session = RequireSession(request.SessionId);
        if (session.Info.Compatibility != null) throw new WorkbookException("READ_ONLY_INSPECTION", "Workflow planning requires the validated workbook path; package inspection only supports bounded cell views.");
        return TsekSheet.Application.Workflows.WorkflowPlanning.Prepare(session, request, token);
    }, token));

    public Task<TsekSheet.Application.Workflows.StructuralCandidateReview> GenerateCandidateAsync(Guid destinationId, int destinationRevision, TsekSheet.Application.Workflows.WorkflowPlanRequest request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession copy; IWorkbookSession destination;
        lock (gate)
        {
            copy = RequireSession(request.SessionId); destination = RequireSession(destinationId);
            if (!copies.ContainsKey(request.SessionId) || !ReferenceEquals(destination, active)) throw new WorkbookException("WORKING_COPY_REQUIRED", "Create and select an isolated working copy before generating a candidate.");
        }
        return copy.PrepareCandidateReview(destination, request, destinationRevision, token);
    }, token));

    public Task<TsekSheet.Application.Workflows.StructuralCandidateReview> ApproveCandidateAsync(TsekSheet.Application.Workflows.CandidateReviewApproval request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession copy; IWorkbookSession destination;
        lock (gate)
        {
            copy = RequireSession(request.SessionId); destination = RequireSession(request.DestinationSessionId);
            if (!copies.ContainsKey(request.SessionId) || !ReferenceEquals(destination, active)) throw new WorkbookException("INVALID_WORKING_COPY", "Review approval must target the active original and its copy.");
        }
        return copy.ApproveCandidateReview(destination, request, token);
    }, token));

    public Task<string> CancelCandidateAsync(Guid sessionId, Guid candidateId, int expectedRevision) => Run(token => Task.Run(() =>
    {
        IWorkbookSession copy; lock (gate) copy = RequireSession(sessionId);
        if (!copies.ContainsKey(sessionId)) throw new WorkbookException("INVALID_WORKING_COPY", "Only copy reviews can be cancelled.");
        token.ThrowIfCancellationRequested(); copy.CancelCandidateReview(candidateId, expectedRevision); return "cancelled";
    }, token));

    public Task<WorksheetIntegrationPreview> PreviewIntegrationAsync(Guid sessionId, WorksheetIntegrationRequest request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession destination; IWorkbookSession copy;
        lock (gate) { destination = RequireSession(sessionId); copy = RequireSession(request.WorkingCopyId);
            if (!ReferenceEquals(destination, active) || !copies.ContainsKey(request.WorkingCopyId)) throw new WorkbookException("INVALID_WORKING_COPY", "Choose a working copy and its original session."); }
        return destination.PreviewIntegration(copy, request, token);
    }, token));

    public Task<WorksheetIntegrationResult> IntegrateAsync(Guid sessionId, WorksheetIntegrationRequest request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession destination; IWorkbookSession copy;
        lock (gate) { destination = RequireSession(sessionId); copy = RequireSession(request.WorkingCopyId);
            if (!ReferenceEquals(destination, active) || !copies.ContainsKey(request.WorkingCopyId)) throw new WorkbookException("INVALID_WORKING_COPY", "Choose a working copy and its original session."); }
        return destination.Integrate(copy, request, token);
    }, token));

    public Task<string> CloseAsync(Guid sessionId, bool discardChanges = false) => Run(_ =>
    {
        IWorkbookSession session; IWorkbookSession[] children;
        lock (gate)
        {
            session = RequireSession(sessionId);
            if (!ReferenceEquals(session, active)) throw new WorkbookException("SESSION_NOT_FOUND", "Close the original workbook session.");
            if (HasPendingChangesLocked() && !discardChanges) throw new WorkbookException("UNSAVED_CHANGES", "Pending changes will be discarded. Confirm before closing.");
            ClearReviewLocked(); active = null; children = copies.Values.ToArray(); copies.Clear();
        }
        foreach (var child in children) child.Dispose(); session.Dispose(); return Task.FromResult("closed");
    });

    private IWorkbookSession RequireSession(Guid id) => !disposed && active?.Info.SessionId == id ? active : !disposed && copies.TryGetValue(id, out var copy) ? copy : throw new WorkbookException("SESSION_NOT_FOUND", "Workbook session is closed or does not belong to this page.");
    private static WorkingCopySummary Summarize(IWorkbookSession copy) { var info = copy.WorkingCopy!; return new(info.SessionId, info.SourceWorksheetId, info.BaseRevision, info.Revision, info.IsDirty, info.Changes.Count); }
    private bool HasPendingChangesLocked() => active?.Info.IsDirty == true || copies.Values.Any(c => c.Info.IsDirty);
    public bool HasPendingChanges { get { lock (gate) return !disposed && HasPendingChangesLocked(); } }

    public void Dispose()
    {
        IWorkbookSession? session; IWorkbookSession[] children;
        lock (gate) { if (disposed) return; disposed = true; lifetime.Cancel(); ClearReviewLocked(); session = active; active = null; children = copies.Values.ToArray(); copies.Clear(); }
        foreach (var child in children) child.Dispose(); session?.Dispose(); lifetime.Dispose();
    }
}
