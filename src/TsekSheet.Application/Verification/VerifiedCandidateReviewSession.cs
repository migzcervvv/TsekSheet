using System.Text.Json;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

public enum VerifiedCandidateReviewStatus { Pending, Approved, Discarded, Invalidated }

public sealed record VerifiedCandidateReviewSnapshot(VerifiedCandidateReviewStatus Status, VerifiedCandidatePreview Preview)
{
    public bool CanApply => false;
}

/// <summary>
/// Owns live review bindings and one engine ticket. Approval changes metadata
/// only. Snapshots never authorize adoption, integration or saving.
/// </summary>
public sealed class VerifiedCandidateReviewSession
{
    private readonly object _gate = new();
    private readonly IWorkbookSession _copy;
    private readonly IWorkbookSession _parent;
    private readonly CandidateDestination _destination;
    private readonly VerifiedPlacementInput[] _inputs;
    private readonly IReadOnlyList<VerifiedMappingPreview> _mappings;
    private StructuralCandidateReview _candidate;
    private VerifiedCandidateReviewStatus _status;

    private VerifiedCandidateReviewSession(IWorkbookSession copy, IWorkbookSession parent,
        VerifiedPlacementInput[] inputs, VerifiedCandidatePreview preview)
    {
        _copy = copy;
        _parent = parent;
        _inputs = inputs;
        _destination = preview.Destination;
        _mappings = preview.Mappings;
        _candidate = Clone(preview.Candidate);
    }

    // No constructor accepts caller-created snapshots as verification proof.
    public static VerifiedCandidateReviewSession Prepare(IWorkbookSession copy, IWorkbookSession parent,
        CandidateDestination destination, IReadOnlyList<VerifiedPlacementInput> inputs, CancellationToken token = default)
    {
        if (inputs is not { Count: >= 1 and <= 20 })
            throw WorkflowProfiles.Error("INVALID_RECORD", "Supply 1-20 unique live record reviews.");
        var captured = inputs.ToArray();
        var preview = VerifiedCandidatePreparation.Prepare(copy, parent, destination, captured, token);
        return new(copy, parent, captured, preview);
    }

    public VerifiedCandidateReviewSnapshot Snapshot
    {
        get { lock (_gate) return Capture(); }
    }

    public VerifiedCandidateReviewSnapshot Approve(CandidateReviewApproval command, CancellationToken token = default)
    {
        lock (_gate)
        {
            RequireOpen();
            if (command == null || command != ExpectedCommand(command.DuplicatesReviewed))
                throw WorkflowProfiles.Error("INVALID_REVIEW", "Approve the exact retained candidate and its bindings.");

            // Stale authorities and cancellation invalidate this ticket even if
            // a command would otherwise fail a duplicate/formatting/replay gate.
            try { Recheck(token); }
            catch (Exception failure) { DiscardAfterFailure(failure); throw; }

            if (_status == VerifiedCandidateReviewStatus.Approved)
                throw WorkflowProfiles.Error("APPROVAL_REPLAY", "This exact candidate has already been approved.");
            if (_candidate.Proposal.Records.Any(record => record.Duplicates.Count != 0) && !command.DuplicatesReviewed)
                throw WorkflowProfiles.Error("REVIEW_REQUIRED", "Explicit duplicate review is required for this exact candidate.");
            if (_candidate.RejectionReasons.Count != 0)
                throw WorkflowProfiles.Error("NATIVE_FORMATTING_UNVERIFIED", string.Join(" ", _candidate.RejectionReasons));

            try
            {
                var approved = _copy.ApproveCandidateReview(_parent, command, token);
                // Only the approval lifecycle fact may change. Do not trust a
                // different response ticket, provenance, oracle or Apply flag.
                var expected = _candidate with { Lifecycle = _candidate.Lifecycle with { Approval = "approved" } };
                if (!_candidate.ReviewOnly || _candidate.Proposal.CanApply || _candidate.OracleReport?.Accepted != true
                    || JsonSerializer.Serialize(approved) != JsonSerializer.Serialize(expected))
                    throw WorkflowProfiles.Error("INVALID_REVIEW", "Approval returned a different candidate or lifecycle.");
                _candidate = Clone(approved);
                Recheck(token);
                _status = VerifiedCandidateReviewStatus.Approved;
                return Capture();
            }
            catch (Exception failure) { DiscardAfterFailure(failure); throw; }
        }
    }

    /// <summary>Discard only this session's exact ticket, including after approval.</summary>
    public VerifiedCandidateReviewSnapshot Discard()
    {
        lock (_gate)
        {
            RequireOpen();
            _status = VerifiedCandidateReviewStatus.Invalidated;
            // On cleanup failure the session stays unusable; the error is never
            // hidden and a newer pending ticket is never substituted.
            _copy.CancelCandidateReview(_candidate.CandidateId, _copy.Info.Revision);
            _status = VerifiedCandidateReviewStatus.Discarded;
            return Capture();
        }
    }

    private CandidateReviewApproval ExpectedCommand(bool duplicatesReviewed) => new(
        _destination.Copy.SessionId, _destination.Parent.SessionId, _destination.Copy.WorksheetId,
        _candidate.CandidateId, _candidate.ReviewSha256, _candidate.CandidateSha256, _candidate.ManifestSha256,
        _destination.Copy.SourceSha256, _destination.Copy.Revision, _destination.Parent.Revision,
        _destination.Copy.ProfileId, _destination.Copy.ProfileHash, duplicatesReviewed);

    private void Recheck(CancellationToken token) =>
        VerifiedCandidatePreparation.Recheck(_copy, _parent, _destination, _inputs, token);

    private void DiscardAfterFailure(Exception failure)
    {
        _status = VerifiedCandidateReviewStatus.Invalidated;
        try
        {
            _copy.CancelCandidateReview(_candidate.CandidateId, _copy.Info.Revision);
            _status = VerifiedCandidateReviewStatus.Discarded;
        }
        catch (Exception cleanup)
        {
            throw new AggregateException("Verified candidate approval and cleanup failed.", failure, cleanup);
        }
    }

    private void RequireOpen()
    {
        if (_status is VerifiedCandidateReviewStatus.Discarded or VerifiedCandidateReviewStatus.Invalidated)
            throw WorkflowProfiles.Error("STALE_REVIEW", "This review session is no longer usable. Prepare a fresh candidate.");
    }

    private VerifiedCandidateReviewSnapshot Capture() =>
        new(_status, new(_destination, _mappings, Clone(_candidate)));

    private static StructuralCandidateReview Clone(StructuralCandidateReview candidate) =>
        JsonSerializer.Deserialize<StructuralCandidateReview>(JsonSerializer.Serialize(candidate))!;
}
