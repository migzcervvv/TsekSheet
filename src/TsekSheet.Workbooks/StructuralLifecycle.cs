using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks;

internal enum StructuralLifecycleStage { Planning, Approval, WorkingCopyAdoption, MergeCandidate, MergeOracle, MergeAdoption, ExportConstruction, ExportValidation }

internal sealed partial class WorkbookSession
{
    private sealed record ReviewedCandidate(byte[] Before, byte[] Bytes, string ManifestJson, StructuralCandidateReview Review,
        bool Approved = false, bool Adopted = false, bool Integrated = false);
    private sealed record StructuralExportProof(byte[] Before, string ManifestJson, string CandidateSha256, int Revision);
    private ReviewedCandidate? pendingStructuralReview;
    private bool reviewConstruction;
    private ReviewedCandidate? acceptedStructuralReview;
    private StructuralExportProof? structuralExportProof;
    private static string StructuralHash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static string StructuralHash(string text) => StructuralHash(Encoding.UTF8.GetBytes(text));
    private static T Frozen<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    private void StructuralLifecycle(StructuralLifecycleStage stage, CancellationToken token)
    {
        engine.StructuralLifecycleFaultForTesting?.Invoke(stage); token.ThrowIfCancellationRequested();
    }

    internal StructuralCandidateReview PrepareStructuralReview(WorkflowPlanRequest request, WorkflowPlan reviewed, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested();
            if (structuralExperiment || changes.Count != 0 || copyBaseBytes == null || !copyBaseBytes.AsSpan().SequenceEqual(currentBytes))
                throw PackageGuard.Reject("STRUCTURAL_REVIEW_SCOPE", "Review supports one operation on a clean selected working copy only.");
            StructuralLifecycle(StructuralLifecycleStage.Planning, token);
            ExperimentInsert(request, reviewed, true, true, token, prepareOnly: true);
            return ReviewView(pendingStructuralReview!);
        }
    }

    private void StorePreparedCandidate(WorkflowPlan plan, StructuralInsertionManifest manifest, byte[] bytes, StructuralPreservationReport report)
    {
        var manifestJson = JsonSerializer.Serialize(manifest);
        var s = StructuralPreservationOracle.S;
        var parts = StructuralPreservationOracle.Read(currentBytes, default);
        var path = StructuralPreservationOracle.InspectEnvelope(parts, manifest);
        var after = StructuralPreservationOracle.Xml(StructuralPreservationOracle.Read(bytes, default)[path]);
        var formulas = after.Descendants(s + "c").Where(c => c.Element(s + "f") != null).ToDictionary(c => (string)c.Attribute("r")!, c => c.Element(s + "f")!.Value);
        var impacts = StructuralPreservationOracle.Xml(parts[path]).Descendants(s + "c").Where(c => c.Element(s + "f") != null).Select(c =>
        {
            var address = (string)c.Attribute("r")!; var row = (int)c.Parent!.Attribute("r")!;
            var column = new string(address.TakeWhile(char.IsLetter).ToArray());
            var final = column + (row >= manifest.Boundary ? row + manifest.Count : row);
            return $"{address} -> {final}: {c.Element(s + "f")!.Value} -> {formulas[final]}";
        }).ToArray();
        var review = new StructuralCandidateReview(Guid.NewGuid(), "", StructuralHash(bytes), StructuralHash(manifestJson), info.DisplayName,
            manifest.Worksheet, Frozen(plan), new("resolved", "eligible-experimental", "generated", report.Accepted ? "accepted" : "rejected", "not-approved",
            "not-adopted", "not-integrated", "not-exported"), Array.AsReadOnly(impacts),
            ["Uniform existing record styles and row height", "Style indexes retained; all unmapped constants cleared", "Formula caches removed; no verified recalculation"],
            new(manifest.Boundary, manifest.Count, manifest.TemplateRow, manifest.FirstDataRow, manifest.SubtotalRow, "Explicit complete section SUM includes every inserted decimal value"),
            reviewConstruction ? NativeFormattingReviewPolicy(parts, manifest) : [], checked(info.Revision + 1), checked(baseRevision + 1))
        {
            DestinationSessionId = info.ParentSessionId, ExpectedDestinationRevision = baseRevision, ReviewOnly = reviewConstruction,
            OracleReport = new(report.Accepted, report.AllowedChanges.ToArray(), report.Differences.Select(d => new CandidateOracleFinding(d.Category,d.Location,d.Reason)).ToArray())
        };
        if (reviewConstruction && impacts.Length > 64) throw PackageGuard.Reject("STRUCTURAL_LIMIT", "Review supports at most 64 formula impacts.");
        review = review with { ReviewSha256 = StructuralHash(JsonSerializer.Serialize(review)) };
        pendingStructuralReview = new(currentBytes.ToArray(), bytes.ToArray(), manifestJson, review);
    }

    private static StructuralCandidateReview ReviewView(ReviewedCandidate ticket) => Frozen(ticket.Review with
    {
        Lifecycle = ticket.Review.Lifecycle with
        {
            Approval = ticket.Approved ? "approved" : "not-approved",
            WorkingCopyAdoption = ticket.Adopted ? "adopted" : "not-adopted",
            Integration = ticket.Integrated ? "integrated" : "not-integrated"
        }
    });

    private ReviewedCandidate RequirePending(StructuralApproval approval)
    {
        var ticket = pendingStructuralReview ?? throw PackageGuard.Reject("STALE_REVIEW", "No current candidate review exists.");
        if ((!engine.StructuralExperimentsEnabled && !ticket.Review.ReviewOnly) || ticket.Adopted || ticket.Review.CandidateId != approval.CandidateId
            || ticket.Review.ReviewSha256 != approval.ReviewSha256 || ticket.Review.CandidateSha256 != approval.CandidateSha256
            || approval.ExpectedCopyRevision != info.Revision || ticket.Review.Proposal.Revision != info.Revision
            || ticket.Review.Proposal.SessionId != info.SessionId || ticket.Review.Proposal.SourceSha256 != info.SourceSha256
            || !ticket.Before.AsSpan().SequenceEqual(currentBytes) || StructuralHash(ticket.Bytes) != ticket.Review.CandidateSha256
            || StructuralHash(ticket.ManifestJson) != ticket.Review.ManifestSha256)
            throw PackageGuard.Reject("STALE_REVIEW", "Candidate identity, review, bytes or accepted revision changed. Generate a new review.");
        if (ticket.Review.Proposal.ProfileHash != WorkflowProfiles.Hash(WorkflowProfiles.Get(ticket.Review.Proposal.ProfileId)))
            throw PackageGuard.Reject("STALE_PROFILE", "The reviewed profile changed.");
        if (ticket.Review.Proposal.Records.Any(r => r.Duplicates.Count != 0) && !approval.DuplicatesReviewed)
            throw PackageGuard.Reject("REVIEW_REQUIRED", "Explicit duplicate review is required for this exact candidate.");
        if (ticket.Review.RejectionReasons.Count != 0) throw PackageGuard.Reject("NATIVE_FORMATTING_UNVERIFIED", string.Join(" ", ticket.Review.RejectionReasons));
        return ticket;
    }

    internal StructuralCandidateReview ApproveStructuralReview(StructuralApproval approval, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested();
            var ticket = RequirePending(approval);
            if (ticket.Approved) throw PackageGuard.Reject("APPROVAL_REPLAY", "This exact candidate has already been approved.");
            StructuralLifecycle(StructuralLifecycleStage.Approval, token);
            pendingStructuralReview = ticket with { Approved = true };
            return ReviewView(pendingStructuralReview);
        }
    }

    internal StructuralCandidateReview AdoptStructuralReview(StructuralApproval approval, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested();
            var ticket = RequirePending(approval);
            if (ticket.Review.ReviewOnly) throw PackageGuard.Reject("PRODUCTION_APPLY_DISABLED", "Review-only candidates cannot be adopted, even with experimental capabilities.");
            if (!ticket.Approved) throw PackageGuard.Reject("REVIEW_REQUIRED", "Approve the exact generated candidate before adoption.");
            using var reservation = engine.ReserveSnapshotWork(estimate);
            using var candidate = LoadStructuralBytes(ticket.Bytes.ToArray(), token);
            var manifest = JsonSerializer.Deserialize<StructuralInsertionManifest>(ticket.ManifestJson)!;
            if (!StructuralPreservationOracle.Evaluate(ticket.Before, manifest, candidate.currentBytes, token).Accepted)
                throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", "Reviewed candidate failed revalidation.");
            var next = info with { Revision = ticket.Review.IntendedWorkingCopyRevision, IsDirty = true, Worksheets = candidate.info.Worksheets };
            var accepted = ticket with { Adopted = true }; var response = ReviewView(accepted);
            StructuralLifecycle(StructuralLifecycleStage.WorkingCopyAdoption, token);
            TransferStructuralState(candidate, next); structuralExperiment = true;
            acceptedStructuralReview = accepted; pendingStructuralReview = null;
            return response;
        }
    }

    internal WorkbookInfo IntegrateReviewedStructure(IWorkbookSession source, StructuralIntegrationRequest request, CancellationToken token = default)
    {
        var copy = RequireIntegrationCopy(source);
        lock (gate) lock (copy.gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); ObjectDisposedException.ThrowIf(copy.disposed, copy); token.ThrowIfCancellationRequested();
            EnsureEditable(); copy.EnsureEditable();
            var ticket = copy.acceptedStructuralReview;
            if (!engine.StructuralIntegrationExperimentsEnabled || !copy.engine.StructuralExperimentsEnabled || !copy.structuralExperiment
                || ticket is not { Approved: true, Adopted: true, Integrated: false } || ticket.Review.ReviewOnly || structuralExportProof != null)
                throw PackageGuard.Reject("STRUCTURAL_MERGE_DISABLED", "Only one approved reviewed structural operation can use the internal integration capability.");
            if (copy.info.SessionId != request.WorkingCopyId || copy.info.ParentSessionId != info.SessionId || copy.info.SourceSha256 != info.SourceSha256
                || copy.workingSheet != request.WorksheetId || ticket.Review.Proposal.WorksheetId != request.WorksheetId)
                throw PackageGuard.Reject("INVALID_WORKING_COPY", "Structural copy provenance or worksheet identity differs.");
            if (request.ExpectedCopyRevision != copy.info.Revision || copy.info.Revision != ticket.Review.IntendedWorkingCopyRevision || copy.changes.Count != 0 || request.ExpectedDestinationRevision != info.Revision
                || request.ExpectedBaseRevision != copy.baseRevision || copy.baseRevision != info.Revision || info.Revision == int.MaxValue
                || !ticket.Before.AsSpan().SequenceEqual(currentBytes) || copy.copyBaseBytes == null || !copy.copyBaseBytes.AsSpan().SequenceEqual(currentBytes))
                throw PackageGuard.Reject("STRUCTURAL_CONFLICT", "Source/base/destination revision or package diverged. No rebase is permitted.");
            if (request.CandidateId != ticket.Review.CandidateId || request.ReviewSha256 != ticket.Review.ReviewSha256
                || request.CandidateSha256 != ticket.Review.CandidateSha256 || StructuralHash(copy.currentBytes) != request.CandidateSha256
                || StructuralHash(ticket.ManifestJson) != ticket.Review.ManifestSha256 || ticket.Review.IntendedDestinationRevision != info.Revision + 1)
                throw PackageGuard.Reject("STALE_REVIEW", "Integration must use the exact approved and adopted candidate bytes.");
            var manifest = JsonSerializer.Deserialize<StructuralInsertionManifest>(ticket.ManifestJson)!;
            var profile = WorkflowProfiles.Get(ticket.Review.Proposal.ProfileId);
            if (WorkflowProfiles.Hash(profile) != ticket.Review.Proposal.ProfileHash) throw PackageGuard.Reject("STALE_PROFILE", "The reviewed profile changed.");
            var section = WorksheetAnalysisReader.Read(this, request.WorksheetId, info.Revision, profile, token).Sections.Single(s => s.Id == ticket.Review.Proposal.Records[0].SectionId);
            if (section.FirstDataRow != manifest.FirstDataRow || section.SubtotalRow != manifest.SubtotalRow || !section.Records.Any(r => r.Row == manifest.TemplateRow))
                throw PackageGuard.Reject("STRUCTURAL_CONFLICT", "Reviewed section/template boundaries differ.");
            using var reservation = engine.ReserveSnapshotWork(estimate);
            StructuralLifecycle(StructuralLifecycleStage.MergeCandidate, token);
            var bytes = copy.currentBytes.ToArray();
            if (engine.CandidateFaultForTesting != null) bytes = engine.CandidateFaultForTesting(bytes);
            using var candidate = LoadStructuralBytes(bytes, token);
            StructuralLifecycle(StructuralLifecycleStage.MergeOracle, token);
            if (StructuralHash(bytes) != request.CandidateSha256 || !StructuralPreservationOracle.Evaluate(currentBytes, manifest, bytes, token).Accepted)
                throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", "Integration candidate differs from the approved structural semantics.");
            var next = info with { Revision = checked(info.Revision + 1), IsDirty = true, Worksheets = candidate.info.Worksheets };
            var proof = new StructuralExportProof(currentBytes.ToArray(), ticket.ManifestJson, request.CandidateSha256, next.Revision);
            var integratedTicket = ticket with { Integrated = true };
            StructuralLifecycle(StructuralLifecycleStage.MergeAdoption, token);
            TransferStructuralState(candidate, next); structuralExportProof = proof; acceptedStructuralReview = integratedTicket; copy.acceptedStructuralReview = integratedTicket;
            return info;
        }
    }

    internal StructuralCandidateReview? CurrentStructuralReview
    {
        get
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                var ticket = acceptedStructuralReview ?? pendingStructuralReview;
                if (ticket == null) return null;
                var review = ReviewView(ticket);
                if (structuralExportProof != null) review = review with { Lifecycle = review.Lifecycle with
                    { Export = structuralExportProof.Revision != info.Revision ? "stale-revision" : info.LastExportedRevision == info.Revision ? "exported" : "eligible-experimental" } };
                return review;
            }
        }
    }

    private void TransferStructuralState(WorkbookSession candidate, WorkbookInfo next)
    {
        var oldWorkbook = workbook; var oldStream = inputStream; var oldRelease = release;
        workbook = candidate.workbook; inputStream = candidate.inputStream; currentBytes = candidate.currentBytes; facts = candidate.facts;
        cellsBySheet = candidate.cellsBySheet; estimate = candidate.estimate; release = candidate.release; info = next;
        candidate.disposed = true; candidate.workbook = null; candidate.inputStream = null;
        try { oldWorkbook?.Dispose(); } catch { }
        try { oldStream?.Dispose(); } catch { }
        finally { oldRelease(); }
    }

    private WorkbookSession LoadStructuralBytes(byte[] bytes, CancellationToken token)
    {
        try { return engine.Load(bytes, info.DisplayName, info.SourceSha256, info.ParentSessionId, info.Worksheets, token); }
        catch (Exception ex) when (ex is not WorkbookException and not OperationCanceledException and not OutOfMemoryException)
        { throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", "Structural candidate could not be reopened; accepted state retained."); }
    }

    private void ValidateStructuralExport(byte[] bytes, CancellationToken token)
    {
        if (structuralExportProof is not { } proof) return;
        if (!engine.StructuralIntegrationExperimentsEnabled || proof.Revision != info.Revision || proof.CandidateSha256 != StructuralHash(bytes))
            throw PackageGuard.Reject("STRUCTURAL_EXPORT_DISABLED", "Structural export requires the exact integrated revision and experimental capability.");
        StructuralLifecycle(StructuralLifecycleStage.ExportValidation, token);
        var manifest = JsonSerializer.Deserialize<StructuralInsertionManifest>(proof.ManifestJson)!;
        if (!StructuralPreservationOracle.Evaluate(proof.Before, manifest, bytes, token).Accepted)
            throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", "Export candidate failed the structural preservation oracle.");
    }
}
