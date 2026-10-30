using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Workflows;

// Supplemental assertions; never cell edits or structural candidate approvals.
public sealed record SectionReviewBinding(Guid ReviewId, Guid SessionId, Guid WorksheetId, int WorksheetOrder,
    int CaptureRevision, string SourceSha256, int ProfileVersion, string ProfileSha256, int ReviewRevision);
public sealed record SectionReviewDecision(Guid Id, string Kind, int Row, string? SectionId, string? Value,
    int? EvidenceRow, int? EvidenceColumn, string Reason);
public sealed record SectionReviewRequest(SectionReviewBinding Binding, SectionReviewDecision Decision);
public sealed record SectionReviewRetraction(SectionReviewBinding Binding, Guid DecisionId);
public sealed record SectionReviewMetrics(int ObservedRows, int CapturedRows, bool AllRowsCaptured, bool RoleColumnsCovered,
    int HeaderRows, int RecordShapedRows, int OwnedRecords, int UncertainIdentities, int UnassignedMeaningfulRows,
    int IntermediateSubtotals, int UnclassifiedSubtotals, int SummaryRows, int BoundaryConflicts, int UnresolvedDependencies);
public sealed record SectionReviewRow(int Row, IReadOnlyDictionary<string, string> Fields, string VendorStatus,
    string? ProvisionalSection, bool Hidden);
public sealed record SectionReviewSnapshot(SectionReviewBinding Binding, string ProfileId, string WorksheetName,
    ReadOnlySectionReport Report, SectionReviewMetrics Before, SectionReviewMetrics After,
    IReadOnlyList<SectionReviewDecision> Decisions, IReadOnlyList<SectionReviewRow> IdentityQueue,
    IReadOnlyList<int> OriginalRecordRows, IReadOnlyList<string> DuplicateKeys, bool ConfigurationReviewed, bool RecordCoverageReviewed,
    bool VendorIdentitiesReviewed, bool HierarchyVerified, bool PreservationVerified, bool CanInsert,
    double CaptureMilliseconds, double ReanalysisMilliseconds);

public sealed class SectionReviewFacts
{
    public Dictionary<int, IReadOnlyDictionary<string, int>> Headers { get; } = [];
    public Dictionary<string, int> Headings { get; } = [];
    public Dictionary<string, int> Ends { get; } = [];
    public Dictionary<int, string> Identities { get; } = [];
    public Dictionary<int, string> Classifications { get; } = [];
}

// One object per active inspection; capture and all private decisions are memory-only.
public sealed class SectionReview
{
    private readonly InspectionRegion capture;
    private readonly ReadOnlySectionProfile profile;
    private readonly ReadOnlySectionReport baseline;
    private readonly string worksheetName;
    private SectionReviewBinding binding;
    private List<SectionReviewDecision> decisions = [];
    private readonly HashSet<Guid> usedIds = [];
    private readonly double captureMs;
    public SectionReviewSnapshot Snapshot { get; private set; }
    private static WorkbookException Invalid(string message) => new("SECTION_REVIEW", message);
    private static string Normalize(string s) => Regex.Replace(s.Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string DuplicateKey(ReadOnlyDuplicate warning) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(warning))));

    public SectionReview(IWorkbookSession session, Guid worksheetId, int revision, ReadOnlySectionProfile profile, CancellationToken token = default)
    {
        ReadOnlySectionAnalysis.Validate(profile);
        var info = session.Info;
        if (info.Compatibility == null || session is not IInspectionAnalysisSession inspection || info.Revision != revision)
            throw Invalid("An unchanged inspection session is required.");
        var sheet = info.Worksheets.SingleOrDefault(s => s.Id == worksheetId) ?? throw Invalid("Unknown review worksheet.");
        this.profile = profile; worksheetName = sheet.Name;
        binding = new(Guid.NewGuid(), info.SessionId, worksheetId, sheet.Order, revision, info.SourceSha256, profile.Version, ReadOnlySectionAnalysis.Fingerprint(profile), 0);
        var watch = Stopwatch.StartNew();
        capture = inspection.CaptureInspectionRegion(new(info.SessionId, worksheetId, revision, info.SourceSha256, profile.StartRow, profile.EndRow, profile.Columns), token);
        captureMs = watch.Elapsed.TotalMilliseconds;
        if (capture.SessionId != binding.SessionId || capture.WorksheetId != worksheetId || capture.Revision != revision || capture.SourceSha256 != binding.SourceSha256)
            throw Invalid("Capture identity changed.");
        baseline = ReadOnlySectionAnalysis.Analyze(capture, profile, token);
        ValidateCurrent(session);
        Snapshot = Recompute([], binding, token);
    }

    public void ValidateCurrent(IWorkbookSession session)
    {
        var info = session.Info;
        if (info.SessionId != binding.SessionId || info.SourceSha256 != binding.SourceSha256 || info.Revision != binding.CaptureRevision
            || info.Worksheets.SingleOrDefault(s => s.Id == binding.WorksheetId)?.Order != binding.WorksheetOrder
            || ReadOnlySectionAnalysis.Fingerprint(profile) != binding.ProfileSha256)
            throw Invalid("Source, worksheet order, capture or profile changed; start a new review.");
    }

    public SectionReviewSnapshot Decide(SectionReviewRequest request, CancellationToken token = default)
    {
        CheckBinding(request.Binding); token.ThrowIfCancellationRequested(); var d = request.Decision;
        if (d == null || d.Id == Guid.Empty || usedIds.Contains(d.Id) || usedIds.Count >= 2048 || decisions.Count >= 512)
            throw Invalid("Replayed decision or review decision budget exceeded.");
        if (string.IsNullOrWhiteSpace(d.Reason) || d.Reason.Length > 512 || d.Reason.Any(char.IsControl)
            || d.Value?.Length > 128 || d.SectionId?.Length > 128 || d.Row < capture.StartRow || d.Row > capture.EndRow)
            throw Invalid("A captured row and a bounded documented reason are required.");
        // Decisions replace a single assertion slot only through explicit retraction.
        if (decisions.Any(x => Slot(x) == Slot(d))) throw Invalid("Retract the existing assertion before replacing it.");
        ValidateDecision(d);
        var next = decisions.Append(d).ToList(); var nextBinding = binding with { ReviewRevision = binding.ReviewRevision + 1 };
        var result = Recompute(next, nextBinding, token);
        token.ThrowIfCancellationRequested(); usedIds.Add(d.Id); decisions = next; binding = nextBinding; return Snapshot = result;
    }

    public SectionReviewSnapshot Retract(SectionReviewRetraction request, CancellationToken token = default)
    {
        CheckBinding(request.Binding);
        if (!decisions.Any(d => d.Id == request.DecisionId)) throw Invalid("Unknown or already retracted decision.");
        var next = decisions.Where(d => d.Id != request.DecisionId).ToList(); var nextBinding = binding with { ReviewRevision = binding.ReviewRevision + 1 };
        var result = Recompute(next, nextBinding, token);
        token.ThrowIfCancellationRequested(); decisions = next; binding = nextBinding; return Snapshot = result;
    }

    private void CheckBinding(SectionReviewBinding b)
    { if (b != binding) throw Invalid("Stale or mismatched review binding; refresh or restart review."); }
    private static string Slot(SectionReviewDecision d) => d.Kind switch
    {
        "identity" or "missing" or "non-record" or "summary" or "exclusion" or "intermediate" => "classification:" + d.Row,
        "heading" or "boundary" or "parent" => d.Kind + ":" + d.SectionId,
        _ => d.Kind + ":" + d.Row + ":" + d.Value
    };
    private CellInfo Cell(int row, int col) => capture.Cells.SingleOrDefault(c => c.Row == row && c.Column == col) ?? throw Invalid("Evidence is outside the captured roles.");
    private static bool Literal(CellInfo c) => c.Kind == "Text" && c.Formula == null && !c.Truncated && !string.IsNullOrWhiteSpace(c.Value) && c.Value.Length <= 128;
    private bool Meaningful(int row) => capture.Cells.Any(c => c.Row == row && (c.Formula != null || !string.IsNullOrWhiteSpace(c.Value)));
    private ReadOnlySectionRule Rule(string? id) => profile.Sections.SingleOrDefault(s => s.Id == id) ?? throw Invalid("Unknown configured section.");
    private void ValidateDecision(SectionReviewDecision d)
    {
        if (!Meaningful(d.Row)) throw Invalid("Empty rows cannot supply review evidence.");
        if (d.Kind is not ("identity" or "header" or "heading") && (d.EvidenceRow != null || d.EvidenceColumn != null)) throw Invalid("Unexpected evidence coordinates.");
        if (d.Kind is not ("heading" or "boundary" or "parent") && d.SectionId != null) throw Invalid("Unexpected section identity.");
        switch (d.Kind)
        {
            case "header":
                if (d.EvidenceRow != d.Row || d.EvidenceColumn == null || d.Value == null || !baseline.FieldColumns.TryGetValue(d.Value, out var column) || column != d.EvidenceColumn || !Literal(Cell(d.Row, column)))
                    throw Invalid("Confirm a literal header variant at its established role column.");
                // Require a header-like source row; a payable row cannot be relabeled as a header.
                var matches = profile.Fields.Count(f => baseline.FieldColumns.TryGetValue(f.Key, out var c) && Literal(Cell(d.Row, c)) && (f.HeaderAliases ?? []).Append(f.Header).Any(h => Normalize(h) == Normalize(Cell(d.Row, c).Value)));
                if (matches < Math.Max(2, (profile.Fields.Count + 1) / 2)) throw Invalid("Header confirmation requires a captured header-like row.");
                break;
            case "heading":
                var headingRule = Rule(d.SectionId);
                if (d.Value != null || d.EvidenceRow != d.Row || d.EvidenceColumn == null || !headingRule.Heading.Columns.Contains(d.EvidenceColumn.Value)) throw Invalid("Invalid heading evidence role.");
                var heading = Cell(d.Row, d.EvidenceColumn.Value);
                if (!Literal(heading) || !(headingRule.Heading.Prefix ? Normalize(heading.Value).StartsWith(Normalize(headingRule.Heading.Text), StringComparison.Ordinal) : Normalize(heading.Value) == Normalize(headingRule.Heading.Text))) throw Invalid("Heading assertion must match the configured source marker.");
                break;
            case "boundary":
                Rule(d.SectionId);
                if (d.Value != null || !Snapshot.Report.Sections.Single(s => s.Id == d.SectionId).SubtotalCandidates.Contains(d.Row)) throw Invalid("Select an actual proposed boundary for this section.");
                if (decisions.Any(x => x.Row == d.Row && x.Kind == "intermediate")) throw Invalid("An intermediate subtotal cannot also be a final boundary.");
                break;
            case "parent":
                Rule(d.SectionId);
                if (Snapshot.Report.Sections.Single(s => s.Id == d.SectionId).HeadingRow != d.Row) throw Invalid("Parent decisions require the selected heading row.");
                if (d.Value != null) Rule(d.Value);
                break;
            case "identity":
                if (!baseline.Records.Any(r => r.Row == d.Row && r.Classification == "Missing literal vendor identity" && !r.MarkerConflict)
                    || d.Value != null || d.EvidenceRow == null || d.EvidenceColumn == null
                    || !baseline.FieldColumns.TryGetValue(profile.EntityField, out var entityCol) || d.EvidenceColumn != entityCol
                    || !baseline.Records.Any(r => r.Row == d.EvidenceRow && r.Classification == "Literal identity") || !Literal(Cell(d.EvidenceRow.Value, entityCol)))
                    throw Invalid("Confirm missing identity using an explicit captured literal vendor record; no fill-down or typed identities.");
                break;
            case "missing":
                if (d.Value != null || !baseline.Records.Any(r => r.Row == d.Row && r.Classification != "Literal identity")) throw Invalid("Select an unresolved identity row.");
                break;
            case "intermediate":
                if (d.Value != null || !baseline.SubtotalCandidates.Any(s => s.Row == d.Row)
                    || Snapshot.Report.Sections.Any(s => s.SubtotalRow == d.Row) || decisions.Any(x => x.Kind == "boundary" && x.Row == d.Row)) throw Invalid("Only a captured non-final subtotal candidate can be classified intermediate.");
                break;
            case "non-record":
                if (d.Value != null || !baseline.Records.Any(r => r.Row == d.Row)) throw Invalid("Select a captured record-shaped row.");
                break;
            case "summary": case "exclusion": case "investigate":
                if (d.Value != null) throw Invalid("Unexpected classification value.");
                break;
            case "duplicate":
                if (d.Value == null || !Snapshot.Report.PotentialDuplicates.Any(p => DuplicateKey(p) == d.Value && p.Rows.Contains(d.Row))) throw Invalid("Select a current duplicate warning and one of its source rows.");
                break;
            default: throw Invalid("Unsupported review decision.");
        }
    }

    private SectionReviewSnapshot Recompute(List<SectionReviewDecision> next, SectionReviewBinding nextBinding, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); var watch = Stopwatch.StartNew(); var facts = new SectionReviewFacts(); var effective = profile;
        foreach (var d in next)
        {
            switch (d.Kind)
            {
                case "header":
                    var map = facts.Headers.GetValueOrDefault(d.Row)?.ToDictionary() ?? new Dictionary<string, int>();
                    foreach (var f in profile.Fields)
                        if (baseline.FieldColumns.TryGetValue(f.Key, out var c) && Literal(Cell(d.Row, c)) && (f.HeaderAliases ?? []).Append(f.Header).Any(h => Normalize(h) == Normalize(Cell(d.Row, c).Value))) map[f.Key] = c;
                    map[d.Value!] = d.EvidenceColumn!.Value; facts.Headers[d.Row] = map; break;
                case "heading": facts.Headings.Add(d.SectionId!, d.Row); break;
                case "boundary": facts.Ends.Add(d.SectionId!, d.Row); break;
                case "parent": effective = effective with { Sections = effective.Sections.Select(s => s.Id == d.SectionId ? s with { ParentId = d.Value } : s).ToArray() }; break;
                case "identity": facts.Identities.Add(d.Row, Cell(d.EvidenceRow!.Value, d.EvidenceColumn!.Value).Value); break;
                case "summary": case "exclusion": case "non-record": case "intermediate": facts.Classifications.Add(d.Row, d.Kind); break;
            }
        }
        // Validate the entire candidate hierarchy each time, including cycles, containment and overlap.
        ReadOnlySectionAnalysis.Validate(effective);
        var report = ReadOnlySectionAnalysis.Analyze(capture, effective, token, facts);
        report = report with
        {
            PotentialDuplicates = baseline.PotentialDuplicates.Concat(report.PotentialDuplicates).DistinctBy(DuplicateKey).ToArray(),
            SummaryDependencies = baseline.SummaryDependencies.Concat(report.SummaryDependencies).DistinctBy(d => (d.Row, d.Column)).ToArray()
        };
        foreach (var d in next.Where(d => d.Kind == "parent" && d.Value != null))
        {
            var child = report.Sections.Single(s => s.Id == d.SectionId); var parent = report.Sections.Single(s => s.Id == d.Value);
            if (!child.BoundaryRuleMatched || !parent.BoundaryRuleMatched || child.HeadingRow <= parent.HeadingRow || child.SubtotalRow >= parent.SubtotalRow)
                throw Invalid("Reviewed child must be strictly contained in its bounded parent.");
        }
        if (next.Any(d => d.Kind == "parent") && report.Diagnostics.Any(d => d.Code is "INVALID_NESTING" or "OVERLAPPING_REGIONS"
            && !(Snapshot?.Report ?? baseline).Diagnostics.Any(old => old.Code == d.Code && old.Row == d.Row && old.SectionId == d.SectionId)))
            throw Invalid("Reviewed relationship introduced a new hierarchy conflict.");
        foreach (var d in next.Where(d => d.Kind == "boundary"))
            if (next.Any(x => x.Row == d.Row && x.Kind == "intermediate")) throw Invalid("Conflicting subtotal assertions.");
        var queue = baseline.Records.Where(r => r.Classification != "Literal identity").Select(r => new SectionReviewRow(r.Row,
            baseline.FieldColumns.ToDictionary(p => p.Key, p => { var c = Cell(r.Row, p.Value); var v = c.Value[..Math.Min(c.Value.Length, 900)]; return c.Formula != null ? "Formula cache unverified: " + v : c.Truncated || c.Value.Length > 900 ? "Truncated: " + v : v; }),
            report.Records.SingleOrDefault(x => x.Row == r.Row)?.Classification ?? r.Classification,
            report.Records.SingleOrDefault(x => x.Row == r.Row)?.SectionId, r.Hidden)).ToArray();
        var after = Metrics(report, next);
        bool configuration = profile.Sections.All(s => next.Any(d => d.Kind == "heading" && d.SectionId == s.Id) && next.Any(d => d.Kind == "boundary" && d.SectionId == s.Id))
            && report.HeaderRows.Count > 0 && report.HeaderRows.All(r => next.Any(d => d.Kind == "header" && d.Row == r))
            && profile.Sections.Where(s => s.ParentId != null).All(s => next.Any(d => d.Kind == "parent" && d.SectionId == s.Id))
            && !report.Diagnostics.Any(d => d.Code.Contains("HEADER", StringComparison.Ordinal));
        bool recordReviewed = configuration && after.AllRowsCaptured && after.RoleColumnsCovered && after.UnassignedMeaningfulRows == 0 && !next.Any(d => d.Kind == "investigate");
        bool identitiesReviewed = baseline.Records.Where(r => r.Classification != "Literal identity").All(r => next.Any(d => d.Row == r.Row && d.Kind is "identity" or "missing" or "non-record" or "summary" or "exclusion"));
        // Analysis profile hash describes declarations after parent assertions; binding retains the originally selected profile.
        token.ThrowIfCancellationRequested();
        return new(nextBinding, profile.Id, worksheetName, report, Metrics(baseline, []), after, next.ToArray(), queue,
            baseline.Records.Select(r => r.Row).ToArray(), report.PotentialDuplicates.Select(DuplicateKey).ToArray(), configuration, recordReviewed, identitiesReviewed,
            report.HierarchyVerified && !next.Any(d => d.Kind == "investigate"), false, false, captureMs, watch.Elapsed.TotalMilliseconds);
    }

    private static SectionReviewMetrics Metrics(ReadOnlySectionReport r, IReadOnlyList<SectionReviewDecision> d) => new(
        r.Coverage.ObservedLastRow, r.Coverage.RowsCaptured, r.Coverage.EntireWorksheetRowsCaptured, r.Coverage.RoleColumnsComplete,
        r.HeaderRows.Count, r.Records.Count, r.Records.Count(x => x.SectionId != null && !x.Classification.StartsWith("Reviewed non-record", StringComparison.Ordinal)),
        r.Records.Count(x => x.Classification is not ("Literal identity" or "Reviewer-confirmed identity") && !x.Classification.StartsWith("Reviewed non-record", StringComparison.Ordinal)),
        r.UnassignedRows.Count, d.Count(x => x.Kind == "intermediate"), r.SubtotalCandidates.Select(x => x.Row).Distinct().Count(row => !r.Sections.Any(s => s.SubtotalRow == row) && !d.Any(x => x.Row == row && x.Kind is "intermediate" or "summary" or "exclusion" or "non-record")),
        r.SummaryRows.Count, r.Diagnostics.Count(x => x.Code is "AMBIGUOUS_BOUNDARY" or "AMBIGUOUS_HEADING" or "INVALID_NESTING" or "OVERLAPPING_REGIONS" or "MARKER_RESEMBLES_RECORD"), r.SummaryDependencies.Count(x => !x.Complete));
}
