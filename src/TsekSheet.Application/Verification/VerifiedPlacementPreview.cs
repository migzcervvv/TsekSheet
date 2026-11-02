using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

// A choice never supplies replacement field values. Selecting a similar entity
// requires correction and renewed verification in the live review session.
public sealed record PlacementChoices(string? SectionId = null, bool ConfirmNewEntity = false);
public sealed record VerifiedPlacementInput(FieldReviewSession Review, ReviewVersion ExpectedReview,
    PlacementChoices? Choices = null);

/// <summary>A bounded, ordered observation for one destination, never write authority.</summary>
public sealed class VerifiedPlacementBatchPreview
{
    internal VerifiedPlacementBatchPreview(IEnumerable<VerifiedMappingPreview> mappings, WorkflowPlan proposal)
    {
        Mappings = Array.AsReadOnly(mappings.ToArray());
        Proposal = proposal;
    }

    // Mapping review IDs correspond to proposal record IDs in the same order.
    public IReadOnlyList<VerifiedMappingPreview> Mappings { get; }
    public WorkflowPlan Proposal { get; }
    public bool CanApply => false;
}

/// <summary>
/// A single-record placement observation with typed/source provenance. Resolved
/// choices and duplicate warnings do not authorize insertion or future writes.
/// </summary>
public sealed class VerifiedPlacementPreview
{
    internal VerifiedPlacementPreview(VerifiedMappingPreview mapping, WorkflowPlan proposal)
    {
        Mapping = mapping;
        Proposal = proposal;
    }

    public VerifiedMappingPreview Mapping { get; }
    public WorkflowPlan Proposal { get; }
    public bool CanApply => false;
}

public static class VerifiedPlacement
{
    public static VerifiedPlacementBatchPreview PreviewMany(IWorkbookSession session,
        MappingDestination expectedDestination, IReadOnlyList<VerifiedPlacementInput> inputs,
        CancellationToken token = default) => CaptureMany(session, expectedDestination, inputs, false, token);

    /// <summary>
    /// Adds only the existing read-only structural capability diagnostic. A pass
    /// neither generates a candidate nor authorizes approval, adoption or writes.
    /// </summary>
    public static VerifiedPlacementBatchPreview PreflightMany(IWorkbookSession session,
        MappingDestination expectedDestination, IReadOnlyList<VerifiedPlacementInput> inputs,
        CancellationToken token = default) => CaptureMany(session, expectedDestination, inputs, true, token);

    private static VerifiedPlacementBatchPreview CaptureMany(IWorkbookSession session,
        MappingDestination expectedDestination, IReadOnlyList<VerifiedPlacementInput> inputs,
        bool inspectStructure, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(session);
        token.ThrowIfCancellationRequested();
        if (inputs is not { Count: >= 1 and <= 20 })
            throw WorkflowProfiles.Error("INVALID_RECORD", "Supply 1-20 unique live record reviews.");
        var requests = inputs.ToArray();
        if (requests.Length is < 1 or > 20 || requests.Any(input => input == null || input.Review == null))
            throw WorkflowProfiles.Error("INVALID_RECORD", "Supply 1-20 unique live record reviews.");
        var profile = VerifiedMapping.RequireProfile(expectedDestination);
        var records = new List<TypedReviewRecord>();
        var identities = new HashSet<Guid>();
        foreach (var request in requests)
        {
            token.ThrowIfCancellationRequested();
            VerifiedMapping.RequireReviewProfile(profile, request.ExpectedReview);
            var record = request.Review.Project(request.ExpectedReview);
            if (!identities.Add(record.Version.ReviewId))
                throw WorkflowProfiles.Error("INVALID_RECORD", "A live record review may occur only once per preview.");
            records.Add(record);
        }

        VerifiedMapping.RequireDestination(session, expectedDestination);
        var analysis = WorksheetAnalysisReader.Read(session, expectedDestination.WorksheetId,
            expectedDestination.Revision, profile, token);
        var proposal = WorkflowPlanning.Plan(analysis, profile,
            records.Select((record, index) => Input(record, requests[index].Choices)).ToArray(), token);
        if (inspectStructure) proposal = WorkflowPlanning.InspectStructure(session, proposal, token);
        // Recheck after every diagnostic branch, including the engine call. The
        // captured versions remain observations, never durable write authority.
        VerifiedMapping.RequireDestination(session, expectedDestination);
        foreach (var request in requests)
        {
            token.ThrowIfCancellationRequested();
            request.Review.Validate(request.ExpectedReview);
        }
        token.ThrowIfCancellationRequested();
        return new(records.Select(record => VerifiedMapping.Map(expectedDestination, profile, analysis, record)), proposal);
    }

    public static VerifiedPlacementPreview Preview(IWorkbookSession session, MappingDestination expectedDestination,
        FieldReviewSession review, ReviewVersion expectedReview, PlacementChoices? choices = null,
        CancellationToken token = default)
    {
        var (profile, analysis, record) = VerifiedMapping.Capture(session, expectedDestination, review, expectedReview, token);
        // Use the pure planner, never Prepare/InspectStructuralInsertion or any
        // candidate API. Verified values cannot be substituted by planner choices.
        var proposal = WorkflowPlanning.Plan(analysis, profile, [Input(record, choices)], token);
        token.ThrowIfCancellationRequested();
        VerifiedMapping.RequireDestination(session, expectedDestination);
        review.Validate(expectedReview);
        token.ThrowIfCancellationRequested();
        return new(VerifiedMapping.Map(expectedDestination, profile, analysis, record), proposal);
    }

    private static WorkflowRecord Input(TypedReviewRecord record, PlacementChoices? choices) =>
        new(record.Version.ReviewId.ToString("N"),
            record.Fields.ToDictionary(field => field.Key, field => field.NormalizedText, StringComparer.Ordinal),
            choices?.SectionId, ConfirmNewVendor: choices?.ConfirmNewEntity ?? false);
}
