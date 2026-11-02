using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

public sealed record CandidateDestination(MappingDestination Copy, MappingDestination Parent, int BaseRevision);

/// <summary>A generated review-only candidate with verified input provenance, never write authority.</summary>
public sealed class VerifiedCandidatePreview
{
    internal VerifiedCandidatePreview(CandidateDestination destination,
        IReadOnlyList<VerifiedMappingPreview> mappings, StructuralCandidateReview candidate)
    {
        Destination = destination;
        Mappings = Array.AsReadOnly(mappings.ToArray());
        Candidate = candidate;
    }

    public CandidateDestination Destination { get; }
    public IReadOnlyList<VerifiedMappingPreview> Mappings { get; }
    public StructuralCandidateReview Candidate { get; }
    public bool CanApply => false;
}

public static class VerifiedCandidatePreparation
{
    public static VerifiedCandidatePreview Prepare(IWorkbookSession copy, IWorkbookSession parent,
        CandidateDestination destination, IReadOnlyList<VerifiedPlacementInput> inputs,
        CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(copy);
        ArgumentNullException.ThrowIfNull(parent);
        token.ThrowIfCancellationRequested();
        if (inputs is not { Count: >= 1 and <= 20 })
            throw WorkflowProfiles.Error("INVALID_RECORD", "Supply 1-20 unique live record reviews.");
        var requests = inputs.ToArray();
        RequireBindings(copy, parent, destination);
        // Capture fresh live authorities. No caller-created preview or preflight
        // is accepted, and engine generation performs its own structural checks.
        var placement = VerifiedPlacement.PreviewMany(copy, destination.Copy, requests, token);
        var request = new WorkflowPlanRequest(destination.Copy.SessionId, destination.Copy.WorksheetId,
            destination.Copy.Revision, destination.Copy.ProfileId, destination.Copy.ProfileHash,
            placement.Mappings.Select((mapping, index) => new WorkflowRecord(mapping.Review.ReviewId.ToString("N"),
                mapping.Fields.ToDictionary(field => field.Value.Key, field => field.Value.NormalizedText, StringComparer.Ordinal),
                requests[index].Choices?.SectionId, ConfirmNewVendor: requests[index].Choices?.ConfirmNewEntity ?? false)).ToArray());
        Recheck(copy, parent, destination, requests, token);
        var candidate = copy.PrepareCandidateReview(parent, request, destination.Parent.Revision, token);
        try
        {
            Recheck(copy, parent, destination, requests, token);
            return new(destination, placement.Mappings, candidate);
        }
        catch (Exception failure)
        {
            // Cancellation must not prevent cleanup. Never discard a different
            // ticket. Report even a stale cleanup failure: the public contract
            // cannot distinguish replacement from another concurrent revision.
            try { copy.CancelCandidateReview(candidate.CandidateId, copy.Info.Revision); }
            catch (Exception cleanup) { throw new AggregateException("Candidate revalidation and cleanup failed.", failure, cleanup); }
            throw;
        }
    }

    internal static void Recheck(IWorkbookSession copy, IWorkbookSession parent, CandidateDestination destination,
        IEnumerable<VerifiedPlacementInput> inputs, CancellationToken token)
    {
        RequireBindings(copy, parent, destination);
        foreach (var input in inputs) input.Review.Validate(input.ExpectedReview);
        token.ThrowIfCancellationRequested();
    }

    private static void RequireBindings(IWorkbookSession copy, IWorkbookSession parent, CandidateDestination destination)
    {
        if (destination == null || destination.BaseRevision < 0)
            throw WorkflowProfiles.Error("INVALID_WORKING_COPY", "Supply the selected copy and its explicit parent binding.");
        VerifiedMapping.RequireProfile(destination.Copy);
        VerifiedMapping.RequireProfile(destination.Parent);
        var copyInfo = copy.Info;
        var parentInfo = parent.Info;
        var working = copy.WorkingCopy;
        VerifiedMapping.RequireDestination(copyInfo, destination.Copy);
        VerifiedMapping.RequireDestination(parentInfo, destination.Parent);
        if (ReferenceEquals(copy, parent) || parentInfo.ParentSessionId != null || parent.WorkingCopy != null
            || copyInfo.ParentSessionId != destination.Parent.SessionId || working == null
            || working.SessionId != destination.Copy.SessionId || working.SourceWorksheetId != destination.Copy.WorksheetId
            || destination.Copy.WorksheetId != destination.Parent.WorksheetId
            || destination.Copy.SourceSha256 != destination.Parent.SourceSha256
            || destination.Copy.ProfileId != destination.Parent.ProfileId || destination.Copy.ProfileHash != destination.Parent.ProfileHash)
            throw WorkflowProfiles.Error("INVALID_WORKING_COPY", "Select a working copy and its original worksheet destination using the same source and profile.");
        if (working.BaseRevision != destination.BaseRevision || destination.BaseRevision != destination.Parent.Revision)
            throw WorkflowProfiles.Error("STALE_DESTINATION", "The copy base must match the current explicit destination revision.");
        // Info/WorkingCopy are separate reads. Check their captured revisions as
        // well; this is a monotonic-version observation, not a cross-session lock.
        if (copyInfo.Revision != destination.Copy.Revision || working.Revision != destination.Copy.Revision
            || parentInfo.Revision != destination.Parent.Revision)
            throw WorkflowProfiles.Error("STALE_REVISION", "Refresh the selected copy and destination bindings.");
    }
}
