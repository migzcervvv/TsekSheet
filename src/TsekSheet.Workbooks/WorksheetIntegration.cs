using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed partial class WorkbookSession
{
    public WorksheetIntegrationPreview PreviewIntegration(IWorkbookSession source, WorksheetIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        var copy = RequireIntegrationCopy(source);
        lock (gate) lock (copy.gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return InspectIntegration(copy, request);
        }
    }

    private WorkbookSession RequireIntegrationCopy(IWorkbookSession source)
    {
        // Classification cannot change during a session's lifetime. Prevent reverse lock ordering.
        if (workingSheet != null || info.ParentSessionId != null || source is not WorkbookSession copy || copy.workingSheet == null || ReferenceEquals(this, copy))
            throw PackageGuard.Reject("INVALID_WORKING_COPY", "Integrate a working copy into its original main session.");
        return copy;
    }

    private WorksheetIntegrationPreview InspectIntegration(WorkbookSession copy, WorksheetIntegrationRequest request)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ObjectDisposedException.ThrowIf(copy.disposed, copy);
        var conflicts = new List<CompatibilityFinding>();
        void Reject(string code, string message) => conflicts.Add(new(code, message));
        var destination = info.Worksheets.SingleOrDefault(s => s.Id == request.DestinationWorksheetId);
        if (request.Operation != WorksheetIntegrationOperation.Merge)
            Reject("UNSUPPORTED_OPERATION", "Replace and Create New are disabled: reference and resource preservation has not been verified.");
        if (copy.info.SessionId != request.WorkingCopyId || copy.info.ParentSessionId != info.SessionId || copy.info.SourceSha256 != info.SourceSha256)
            Reject("INVALID_WORKING_COPY", "The copy does not belong to this original session.");
        if (destination == null || copy.workingSheet != request.DestinationWorksheetId)
            Reject("UNKNOWN_WORKSHEET", "Merge must target the copy's original worksheet identity.");
        if (request.ExpectedCopyRevision != copy.info.Revision)
            Reject("STALE_COPY_REVISION", "The working copy changed after review. Preview it again.");
        if (request.ExpectedDestinationRevision != info.Revision)
            Reject("STALE_REVISION", "The original session changed after review. Preview it again.");
        if (request.ExpectedBaseRevision != copy.baseRevision || copy.baseRevision != info.Revision
            || copy.copyBaseBytes == null || !copy.copyBaseBytes.AsSpan().SequenceEqual(currentBytes))
            Reject("DESTINATION_CONFLICT", "The original session differs from the copy's base. Create a fresh copy; no changes were merged.");
        EnsureEditable(); copy.EnsureEditable();
        var count = destination == null || copy.workingSheet != destination.Id ? 0 : EffectiveChanges(copy, destination.Id).Length;
        return new(request, destination?.Name ?? "", count, conflicts.Count == 0, conflicts.AsReadOnly());
    }

    private CellChange[] EffectiveChanges(WorkbookSession copy, Guid sheet) => copy.changes
        .GroupBy(c => (c.Row, c.Column)).Select(g => g.Last())
        .Where(c => !SameContent(copy.cellsBySheet[sheet][Key(c.Row, c.Column)], cellsBySheet[sheet][Key(c.Row, c.Column)]))
        .ToArray();

    // Formula caches are not edits and may be removed by an explicit formula edit.
    private static bool SameContent(CellInfo a, CellInfo b) => a.Format == b.Format
        && (a.Formula != null || b.Formula != null ? a.Formula == b.Formula : a.Kind == b.Kind && a.Value == b.Value);

    public WorksheetIntegrationResult Integrate(IWorkbookSession source, WorksheetIntegrationRequest request, CancellationToken cancellationToken = default)
    {
        var copy = RequireIntegrationCopy(source);
        lock (gate) lock (copy.gate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = InspectIntegration(copy, request);
            if (!preview.CanApply) throw PackageGuard.Reject(preview.Conflicts[0].Code, string.Join(" ", preview.Conflicts.Select(c => c.Message)));
            var edits = EffectiveChanges(copy, request.DestinationWorksheetId);
            if (edits.Length == 0) return new(info, 0);
            if (info.Revision == int.MaxValue) throw PackageGuard.Reject("LIMIT_EXCEEDED", "The session revision limit has been reached.");
            using var preparation = engine.ReserveSnapshotWork(estimate);
            WorkbookSession? candidate = null;
            try
            {
                var bytes = currentBytes;
                foreach (var change in edits)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var original = cellsBySheet[change.WorksheetId][Key(change.Row, change.Column)];
                    var edit = new CellEdit(change.WorksheetId, change.Row, change.Column, change.Kind, change.Value, info.Revision);
                    var value = CellCandidate.Validate(edit, original, info.Worksheets);
                    var next = CellCandidate.Apply(bytes, preview.DestinationName, edit, value, cancellationToken);
                    if (engine.CandidateFaultForTesting != null) next.Bytes = engine.CandidateFaultForTesting(next.Bytes);
                    CellCandidate.ValidateUnchangedParts(bytes, next.Bytes, next.PartName, next.Address);
                    bytes = next.Bytes;
                }
                candidate = engine.Load(bytes, info.DisplayName, info.SourceSha256, info.ParentSessionId, info.Worksheets, cancellationToken);
                var changed = edits.Select(c => Key(c.Row, c.Column)).ToHashSet();
                foreach (var sheet in info.Worksheets)
                {
                    if (sheet != candidate.info.Worksheets.Single(s => s.Id == sheet.Id)
                        || cellsBySheet[sheet.Id].Count != candidate.cellsBySheet[sheet.Id].Count)
                        throw PackageGuard.Reject("CANDIDATE_REJECTED", "Integration changed worksheet metadata or cell counts.");
                    foreach (var (key, cell) in cellsBySheet[sheet.Id])
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var expected = sheet.Id == request.DestinationWorksheetId && changed.Contains(key) ? copy.cellsBySheet[sheet.Id][key] : cell;
                        if (!candidate.cellsBySheet[sheet.Id].TryGetValue(key, out var actual) || actual != expected)
                            throw PackageGuard.Reject("CANDIDATE_REJECTED", "Integration changed unexpected cell content or formatting.");
                    }
                }
                if (!facts.StructuralFacts.SequenceEqual(candidate.facts.StructuralFacts))
                    throw PackageGuard.Reject("CANDIDATE_REJECTED", "Integration changed workbook structure.");
                var nextInfo = info with { Revision = checked(info.Revision + 1), IsDirty = true };
                var result = new WorksheetIntegrationResult(nextInfo, edits.Length);
                cancellationToken.ThrowIfCancellationRequested();
                var oldWorkbook = workbook; var oldStream = inputStream; var oldRelease = release;
                workbook = candidate.workbook; inputStream = candidate.inputStream; currentBytes = candidate.currentBytes; facts = candidate.facts;
                cellsBySheet = candidate.cellsBySheet; estimate = candidate.estimate; release = candidate.release; info = nextInfo;
                candidate.disposed = true; candidate.workbook = null; candidate.inputStream = null;
                candidate = null;
                try { oldWorkbook?.Dispose(); } catch { /* Validated adoption remains authoritative. */ }
                try { oldStream?.Dispose(); } catch { /* Validated adoption remains authoritative. */ }
                finally { oldRelease(); }
                return result;
            }
            catch (WorkbookException) { candidate?.Dispose(); throw; }
            catch (OperationCanceledException) { candidate?.Dispose(); throw; }
            catch { candidate?.Dispose(); throw PackageGuard.Reject("CANDIDATE_REJECTED", "Integration failed validation. The original session and working copy were retained."); }
        }
    }
}
