using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Workbooks;

public sealed record WorkbookOpenResult(string Status, WorkbookInfo? Workbook = null);

// One active read-only workbook per page.
public sealed class WorkbookWorkspace(IWorkbookEngine engine, Func<CancellationToken, Task<string?>> selectWorkbook, Func<CancellationToken, Task<string?>>? selectSectionProfile = null) : IDisposable
{
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private IWorkbookSession? active;
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
        CompatibilityFinding? editableRejection = null;
        var candidate = await Task.Run(() =>
        {
            if (inspectOnly) return engine.Inspect(path, token);
            try { return engine.Open(path, token); }
            catch (WorkbookException ex) when (ex.Code is "LIMIT_EXCEEDED" or "UNSUPPORTED_FEATURE" or "EXTERNAL_RESOURCE" or "PROTECTED_WORKBOOK" or "INVALID_PACKAGE")
            {
                // Independent inspection admission, never an editable retry or a
                // converted mutable session. The first error is not a full inventory.
                token.ThrowIfCancellationRequested();
                editableRejection = new(ex.Code, ex.Message);
                try { return engine.Inspect(path, token); }
                catch (WorkbookException inspection)
                {
                    if (inspection.Code == "INSPECTION_UNAVAILABLE") throw new WorkbookException(ex.Code, ex.Message, ex.Details);
                    throw new WorkbookException(inspection.Code,
                        "Workbook could not be opened for inspection. Any existing project was retained.",
                        [new("EDITABLE_ADMISSION_" + ex.Code, ex.Message), new(inspection.Code, inspection.Message)]);
                }
            }
        }, token);
        IWorkbookSession? previous;
        try
        {
            token.ThrowIfCancellationRequested();
            lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); ClearReviewLocked(); previous = active; active = candidate; }
        }
        catch { candidate.Dispose(); throw; }
        previous?.Dispose();
        var info = candidate.Info;
        if (editableRejection != null)
            info = info with { Findings = Array.AsReadOnly(new[] { new CompatibilityFinding("EDITABLE_ADMISSION_" + editableRejection.Code, editableRejection.Message) }.Concat(info.Findings).ToArray()) };
        return new WorkbookOpenResult("opened", info);
    });

    public Task<CellViewport> ReadAsync(Guid sessionId, Guid worksheetId, int row, int column, int rows, int columns) => Run(token => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        IWorkbookSession session; lock (gate) { session = RequireSession(sessionId); if (reviewSheet != null && reviewSheet != worksheetId) ClearReviewLocked(); }
        return session.ReadViewport(worksheetId, row, column, rows, columns);
    }, token));

    public Task<TsekSheet.Application.Verification.VerifiedPlacementPreview> PreviewReceiptAsync(
        TsekSheet.Application.Documents.DesktopReceiptReview receipt, TsekSheet.Application.Verification.ReviewVersion expected,
        TsekSheet.Application.Verification.MappingDestination destination, TsekSheet.Application.Verification.PlacementChoices choices) => Run(token => Task.Run(() =>
    {
        IWorkbookSession session; lock (gate) session = RequireSession(destination.SessionId);
        if (session.Info.Compatibility != null) throw new WorkbookException("READ_ONLY_INSPECTION", "Receipt mapping requires a validated workbook; package inspection cannot plan placement.");
        return receipt.Preview(expected, session, destination, choices, token);
    }, token));

    public Task<TsekSheet.Application.Workflows.WorkflowPlan> PlanRecordsAsync(TsekSheet.Application.Workflows.WorkflowPlanRequest request) => Run(token => Task.Run(() =>
    {
        IWorkbookSession session; lock (gate) session = RequireSession(request.SessionId);
        if (session.Info.Compatibility != null) throw new WorkbookException("READ_ONLY_INSPECTION", "Workflow planning requires the validated workbook path; package inspection only supports bounded cell views.");
        return TsekSheet.Application.Workflows.WorkflowPlanning.Prepare(session, request, token);
    }, token));

    public Task<string> CloseAsync(Guid sessionId) => Run(_ =>
    {
        IWorkbookSession session;
        lock (gate)
        {
            session = RequireSession(sessionId);
            if (!ReferenceEquals(session, active)) throw new WorkbookException("SESSION_NOT_FOUND", "Close the original workbook session.");
            ClearReviewLocked(); active = null;
        }
        session.Dispose(); return Task.FromResult("closed");
    });

    private IWorkbookSession RequireSession(Guid id) => !disposed && active?.Info.SessionId == id ? active : throw new WorkbookException("SESSION_NOT_FOUND", "Workbook session is closed or does not belong to this page.");
    private bool HasPendingChangesLocked() => false;
    public bool HasPendingChanges { get { lock (gate) return !disposed && HasPendingChangesLocked(); } }

    public void Dispose()
    {
        IWorkbookSession? session;
        lock (gate) { if (disposed) return; disposed = true; lifetime.Cancel(); ClearReviewLocked(); session = active; active = null; }
        session?.Dispose(); lifetime.Dispose();
    }
}
