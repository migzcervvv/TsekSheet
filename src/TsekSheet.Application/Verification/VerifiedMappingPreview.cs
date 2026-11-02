using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

public sealed record MappingDestination(Guid SessionId, Guid WorksheetId, int Revision, string SourceSha256,
    string ProfileId, string ProfileHash);
public sealed record VerifiedMappedField(string Header, int Column, TypedReviewField Value);
public sealed record MappingSection(string Id, string Label, int HeadingRow, int FirstDataRow, int SubtotalRow);

/// <summary>
/// An observation of verified values and resolved columns, not a cell edit or
/// insertion plan. Future operations must recheck live review/destination state.
/// </summary>
public sealed class VerifiedMappingPreview
{
    internal VerifiedMappingPreview(MappingDestination destination, string worksheetName, TypedReviewRecord record,
        IEnumerable<VerifiedMappedField> fields, IEnumerable<MappingSection> sections)
    {
        Destination = destination;
        WorksheetName = worksheetName;
        Review = record.Version;
        Fields = Array.AsReadOnly(fields.ToArray());
        Sections = Array.AsReadOnly(sections.ToArray());
    }

    public MappingDestination Destination { get; }
    public string WorksheetName { get; }
    public ReviewVersion Review { get; }
    public IReadOnlyList<VerifiedMappedField> Fields { get; }
    public IReadOnlyList<MappingSection> Sections { get; }
    public bool CanApply => false;
}

public static class VerifiedMapping
{
    // Takes live authorities, never a caller-created observation/verification flag.
    public static VerifiedMappingPreview Preview(IWorkbookSession session, MappingDestination expectedDestination,
        FieldReviewSession review, ReviewVersion expectedReview, CancellationToken token = default)
    {
        var (profile, analysis, record) = Capture(session, expectedDestination, review, expectedReview, token);
        token.ThrowIfCancellationRequested();
        RequireDestination(session, expectedDestination);
        review.Validate(expectedReview);
        token.ThrowIfCancellationRequested();
        return Map(expectedDestination, profile, analysis, record);
    }

    internal static (WorkflowProfile Profile, WorksheetAnalysis Analysis, TypedReviewRecord Record) Capture(
        IWorkbookSession session, MappingDestination expectedDestination, FieldReviewSession review,
        ReviewVersion expectedReview, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(review);
        token.ThrowIfCancellationRequested();
        var profile = RequireProfile(expectedDestination);
        RequireReviewProfile(profile, expectedReview);

        RequireDestination(session, expectedDestination);
        var analysis = WorksheetAnalysisReader.Read(session, expectedDestination.WorksheetId,
            expectedDestination.Revision, profile, token);
        // Projection is atomic with corrections/retraction. The final destination
        // check brackets this capture with the same monotonic workbook revision.
        var record = review.Project(expectedReview);
        token.ThrowIfCancellationRequested();
        return (profile, analysis, record);
    }

    internal static WorkflowProfile RequireProfile(MappingDestination expectedDestination)
    {
        if (expectedDestination == null || expectedDestination.SessionId == Guid.Empty
            || expectedDestination.WorksheetId == Guid.Empty || expectedDestination.Revision < 0)
            throw WorkflowProfiles.Error("INVALID_MAPPING_REQUEST", "Supply a selected worksheet and its current destination binding.");
        var profile = WorkflowProfiles.Get(expectedDestination.ProfileId);
        if (expectedDestination.ProfileHash != WorkflowProfiles.Hash(profile))
            throw WorkflowProfiles.Error("STALE_PROFILE", "Refresh the approved mapping profile.");
        return profile;
    }

    internal static void RequireReviewProfile(WorkflowProfile profile, ReviewVersion expectedReview)
    {
        if (expectedReview == null)
            throw new FieldReviewException("STALE_RECORD_REVIEW", "Supply the current record review version.");
        if (expectedReview.ProfileId != profile.Id || expectedReview.ProfileHash != WorkflowProfiles.Hash(profile))
            throw WorkflowProfiles.Error("STALE_PROFILE", "The record review and selected worksheet must use the same approved profile.");
    }

    internal static VerifiedMappingPreview Map(MappingDestination destination, WorkflowProfile profile,
        WorksheetAnalysis analysis, TypedReviewRecord record) =>
        new(destination, analysis.WorksheetName, record,
            profile.Fields.Select(field => new VerifiedMappedField(field.Header, analysis.Columns[field.Key],
                record.Fields.Single(value => value.Key == field.Key))),
            analysis.Sections.Select(section => new MappingSection(section.Id, section.Label,
                section.HeadingRow, section.FirstDataRow, section.SubtotalRow)));

    internal static void RequireDestination(IWorkbookSession session, MappingDestination expected)
        => RequireDestination(session.Info, expected);

    internal static void RequireDestination(WorkbookInfo info, MappingDestination expected)
    {
        if (info.SessionId != expected.SessionId || info.SourceSha256 != expected.SourceSha256)
            throw WorkflowProfiles.Error("INVALID_MAPPING_DESTINATION", "The mapping destination belongs to another workbook source or session.");
        if (info.Revision != expected.Revision)
            throw WorkflowProfiles.Error("STALE_REVISION", "Refresh the selected worksheet before mapping verified fields.");
        if (!info.Worksheets.Any(sheet => sheet.Id == expected.WorksheetId))
            throw WorkflowProfiles.Error("UNKNOWN_WORKSHEET", "Select a worksheet belonging to this destination session.");
    }
}
