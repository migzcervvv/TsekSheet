using System.Globalization;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks;

internal sealed partial class WorkbookSession
{
    // Exact, intentionally narrow review policy. It never enables production adoption.
    private static IReadOnlyList<string> NativeFormattingReviewPolicy(Dictionary<string, byte[]> parts, StructuralInsertionManifest manifest)
    {
        var s = StructuralPreservationOracle.S;
        var selected = StructuralPreservationOracle.Xml(parts[StructuralPreservationOracle.InspectEnvelope(parts, manifest)]);
        var template = selected.Descendants(s + "row").Single(r => (int)r.Attribute("r")! == manifest.TemplateRow);
        if ((string?)template.Attribute("ht") != "21" || (string?)template.Attribute("customHeight") != "1")
            return [$"Template row {manifest.TemplateRow} has height {(string?)template.Attribute("ht") ?? "inherited"} points. Review approval is restricted to explicitly fixed 21-point templates. Heights 22 and 21.95 are not treated as equivalent; Apply remains disabled."];
        foreach (var (path, bytes) in parts.Where(p => p.Key.StartsWith("xl/worksheets/", StringComparison.Ordinal)))
        {
            var xml = StructuralPreservationOracle.Xml(bytes); var defaults = xml.Root!.Element(s + "sheetFormatPr");
            if (defaults == null || (string?)defaults.Attribute("defaultRowHeight") != "15" || defaults.Attributes().Any(a => a.Name.LocalName != "defaultRowHeight" && !a.IsNamespaceDeclaration))
                return ["Worksheet height defaults are outside the independently tested review policy. Apply remains disabled."];
            foreach (var row in xml.Descendants(s + "row").Where(r => r.Attribute("ht") != null))
                if (!decimal.TryParse((string?)row.Attribute("ht"), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var height) || height != 21m || (string?)row.Attribute("customHeight") != "1")
                    return [$"Worksheet row {(string?)row.Attribute("r")} uses height {(string?)row.Attribute("ht")} points outside the fixed 21-point review policy. Apply remains disabled."];
        }
        return [];
    }
    private WorkbookSession ReviewDestination(IWorkbookSession destination)
    {
        if (destination is not WorkbookSession parent || ReferenceEquals(parent, this) || parent.workingSheet != null || parent.info.ParentSessionId != null)
            throw PackageGuard.Reject("INVALID_WORKING_COPY", "Review requires the original destination session and its selected copy.");
        return parent;
    }
    private void ValidateReviewDestination(WorkbookSession parent, int expectedRevision)
    {
        ObjectDisposedException.ThrowIf(disposed, this); ObjectDisposedException.ThrowIf(parent.disposed, parent);
        if (info.ParentSessionId != parent.info.SessionId || info.SourceSha256 != parent.info.SourceSha256 || workingSheet == null)
            throw PackageGuard.Reject("INVALID_WORKING_COPY", "Candidate provenance differs from the original session.");
        if (expectedRevision != parent.info.Revision || baseRevision != parent.info.Revision || copyBaseBytes == null || !copyBaseBytes.AsSpan().SequenceEqual(parent.currentBytes))
            throw PackageGuard.Reject("STALE_DESTINATION", "Destination revision or base bytes changed. Create a fresh working copy.");
    }
    public StructuralCandidateReview PrepareCandidateReview(IWorkbookSession destination, WorkflowPlanRequest request, int expectedDestinationRevision, CancellationToken token = default)
    {
        var parent = ReviewDestination(destination);
        lock (parent.gate) lock (gate)
        {
            token.ThrowIfCancellationRequested(); ValidateReviewDestination(parent, expectedDestinationRevision);
            if (reviewConstruction) throw PackageGuard.Reject("BUSY", "A review is already being constructed.");
            reviewConstruction = true;
            try { return PrepareStructuralReview(request, WorkflowPlanning.Prepare(this, request, token), token); }
            finally { reviewConstruction = false; }
        }
    }
    public StructuralCandidateReview ApproveCandidateReview(IWorkbookSession destination, CandidateReviewApproval request, CancellationToken token = default)
    {
        var parent = ReviewDestination(destination);
        lock (parent.gate) lock (gate)
        {
            token.ThrowIfCancellationRequested(); ValidateReviewDestination(parent, request.ExpectedDestinationRevision);
            var review = pendingStructuralReview?.Review ?? throw PackageGuard.Reject("STALE_REVIEW", "Generate a current candidate first.");
            if (!review.ReviewOnly || request.SessionId != info.SessionId || request.DestinationSessionId != parent.info.SessionId || request.WorksheetId != workingSheet
                || request.SourceSha256 != info.SourceSha256 || request.ManifestSha256 != review.ManifestSha256 || request.ProfileId != review.Proposal.ProfileId
                || request.ProfileHash != review.Proposal.ProfileHash || review.OracleReport?.Accepted != true)
                throw PackageGuard.Reject("INVALID_REVIEW", "Approval must match source, destination, profile, manifest and oracle acceptance.");
            return ApproveStructuralReview(new(request.CandidateId,request.ReviewSha256,request.CandidateSha256,request.ExpectedCopyRevision,request.DuplicatesReviewed), token);
        }
    }
    public void CancelCandidateReview(Guid candidateId, int expectedRevision)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (info.Revision != expectedRevision || pendingStructuralReview?.Review.CandidateId != candidateId || !pendingStructuralReview.Review.ReviewOnly)
                throw PackageGuard.Reject("STALE_REVIEW", "The review is no longer current.");
            pendingStructuralReview = null;
        }
    }
}
