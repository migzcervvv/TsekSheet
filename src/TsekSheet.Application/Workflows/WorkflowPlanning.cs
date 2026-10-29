using System.Globalization;
using System.Xml;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Workflows;

public sealed record WorkflowRecord(string Id, IReadOnlyDictionary<string, string> Values, string? SelectedSectionId = null,
    string? SelectedVendor = null, bool ConfirmNewVendor = false);
public sealed record WorkflowPlanRequest(Guid SessionId, Guid WorksheetId, int ExpectedRevision, string ProfileId, string ProfileHash, IReadOnlyList<WorkflowRecord> Records);
public sealed record VendorSectionMatch(string SectionId, string Label, int LastRow, int Entries);
public sealed record DuplicateWarning(int? ExistingRow, string? SectionId, string? BatchRecordId, string Reason);
public sealed record MappedField(string Key, string Header, int Column, string Kind, string Value);
public sealed record RecordInsertionProposal(string RecordId, string Vendor, string MatchReason, IReadOnlyList<VendorSectionMatch> Matches,
    IReadOnlyList<string> SuggestedVendors, bool RequiresVendorChoice, bool RequiresSectionChoice, string? SectionId, string? SectionLabel,
    int? BaseInsertionRow, int? ProposedRow, int AffectedThroughRow, IReadOnlyList<MappedField> Fields, IReadOnlyList<DuplicateWarning> Duplicates);
public sealed record WorkflowPlan(Guid SessionId, Guid WorksheetId, int Revision, string SourceSha256, string ProfileId, string ProfileHash,
    bool ReadyForReview, bool CanApply, string InsertionBlockedReason, IReadOnlyList<string> RequiredConfirmations,
    IReadOnlyList<string> IntegrityConcerns, IReadOnlyList<RecordInsertionProposal> Records)
{
    public string StructuralStatus { get; init; } = "production-disabled";
    public WorkflowReviewState Review => WorkflowReviewState.Planning(ReadyForReview, StructuralStatus);
}

public static class WorkflowPlanning
{
    public const string InsertionBlock = "Apply is disabled. A bounded structural oracle and internal candidate experiment exist; production structural Merge, export and Excel compatibility remain unverified.";

    public static WorkflowPlan Prepare(IWorkbookSession session, WorkflowPlanRequest request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (request.SessionId != session.Info.SessionId || request.ExpectedRevision < 0) throw WorkflowProfiles.Error("INVALID_REQUEST", "Planning requires a valid session identity and revision.");
        var profile = WorkflowProfiles.Get(request.ProfileId);
        if (request.ProfileHash != WorkflowProfiles.Hash(profile)) throw WorkflowProfiles.Error("STALE_PROFILE", "Refresh the approved workflow profile before planning.");
        var analysis = WorksheetAnalysisReader.Read(session, request.WorksheetId, request.ExpectedRevision, profile, token);
        var plan = Plan(analysis, profile, request.Records, token);
        if (!plan.ReadyForReview) return plan with { StructuralStatus = "ambiguous-destination" };
        if (plan.Records.Any(r => r.Duplicates.Count > 0)) return plan with { StructuralStatus = "duplicate-review-required" };
        return session.InspectStructuralInsertion(plan, token);
    }

    public static WorkflowPlan Plan(WorksheetAnalysis analysis, WorkflowProfile profile, IReadOnlyList<WorkflowRecord> records, CancellationToken token = default)
    {
        WorkflowProfiles.Validate(profile); token.ThrowIfCancellationRequested();
        if (analysis.ProfileId != profile.Id || analysis.ProfileHash != WorkflowProfiles.Hash(profile)) throw WorkflowProfiles.Error("STALE_PROFILE", "The reviewed profile differs from the analyzed profile.");
        if (records is not { Count: >= 1 and <= 20 } || records.Any(r => r == null || r.Id is not { Length: > 0 and <= 48 } || !r.Id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_'))
            || records.Select(r => r.Id).Distinct().Count() != records.Count) throw WorkflowProfiles.Error("INVALID_RECORD", "Supply 1–20 uniquely identified records.");
        var normalized = records.Select(r => ValidateValues(r, profile)).ToArray();
        var existing = analysis.Sections.SelectMany(s => s.Records.Select(r => (Section: s, Record: r))).ToArray();
        var proposals = new List<RecordInsertionProposal>();
        for (var index = 0; index < records.Count; index++)
        {
            token.ThrowIfCancellationRequested();
            var request = records[index]; var values = normalized[index]; var vendor = values[profile.EntityField];
            var entity = WorkflowProfiles.Entity(vendor, profile);
            var exact = existing.Where(e => WorkflowProfiles.Entity(e.Record.Values[profile.EntityField], profile) == entity).ToArray();
            var suggestions = exact.Length != 0 ? [] : existing.Select(e => e.Record.Values[profile.EntityField]).Distinct()
                .Where(v => Similar(WorkflowProfiles.Normalize(v, profile), WorkflowProfiles.Normalize(vendor, profile))).Order(StringComparer.Ordinal).Take(8).ToArray();
            var reason = exact.Length == 0 ? "new entity" : WorkflowProfiles.Normalize(vendor, profile) == entity ? "exact normalized match" : "configured alias";
            if (request.SelectedVendor != null)
            {
                if (exact.Length != 0 || request.ConfirmNewVendor || !suggestions.Contains(request.SelectedVendor, StringComparer.Ordinal))
                    throw WorkflowProfiles.Error("INVALID_VENDOR_CHOICE", "Choose only a displayed similar-name candidate, or explicitly retain the submitted entity.");
                vendor = request.SelectedVendor; entity = WorkflowProfiles.Entity(vendor, profile); values[profile.EntityField] = vendor;
                exact = existing.Where(e => WorkflowProfiles.Entity(e.Record.Values[profile.EntityField], profile) == entity).ToArray(); reason = "explicitly selected similar-name candidate";
            }
            if (request.ConfirmNewVendor && exact.Length != 0) throw WorkflowProfiles.Error("INVALID_VENDOR_CHOICE", "An exact or alias match cannot be silently treated as a different entity.");
            var vendorChoice = exact.Length == 0 && suggestions.Length > 0 && !request.ConfirmNewVendor;
            var matches = exact.GroupBy(e => e.Section.Id).Select(g => new VendorSectionMatch(g.Key, g.First().Section.Label, g.Max(e => e.Record.Row), g.Count())).ToArray();
            WorksheetSection? destination = null;
            if (!vendorChoice)
            {
                var allowed = matches.Length == 0 ? [profile.FallbackSectionId] : matches.Select(m => m.SectionId).ToArray();
                if (request.SelectedSectionId != null && !allowed.Contains(request.SelectedSectionId)) throw WorkflowProfiles.Error("INVALID_SECTION_CHOICE", "The selected section is not an allowed destination for this entity.");
                var chosen = request.SelectedSectionId ?? (allowed.Length == 1 ? allowed[0] : null);
                destination = analysis.Sections.SingleOrDefault(s => s.Id == chosen);
            }
            else if (request.SelectedSectionId != null) throw WorkflowProfiles.Error("CHOICE_REQUIRED", "Resolve the vendor identity before choosing its destination section.");
            var row = destination == null ? (int?)null : matches.SingleOrDefault(m => m.SectionId == destination.Id)?.LastRow + 1 ?? destination.SubtotalRow;
            if (destination != null && (row < destination.FirstDataRow || row > destination.SubtotalRow)) throw WorkflowProfiles.Error("UNSAFE_BOUNDARY", "The proposed row is outside the detected data area.");
            var duplicates = new List<DuplicateWarning>();
            foreach (var item in existing.Where(e => WorkflowProfiles.Entity(e.Record.Values[profile.EntityField], profile) == entity))
            {
                var ids = MatchingIdentifiers(values, item.Record.Values, profile);
                if (ids.Length > 0) duplicates.Add(new(item.Record.Row, item.Section.Id, null, Explain(ids, values, item.Record.Values, profile)));
            }
            // Check the whole batch, including records later in the request. Do not silently deduplicate it.
            for (var other = 0; other < records.Count; other++)
            {
                if (other == index) continue;
                var otherVendor = records[other].SelectedVendor ?? normalized[other][profile.EntityField];
                if (WorkflowProfiles.Entity(otherVendor, profile) != entity) continue;
                var ids = MatchingIdentifiers(values, normalized[other], profile);
                if (ids.Length > 0) duplicates.Add(new(null, null, records[other].Id, Explain(ids, values, normalized[other], profile)));
            }
            if (duplicates.Count > 50) throw WorkflowProfiles.Error("ANALYSIS_LIMIT", "Too many potential duplicates to review safely in one proposal.");
            proposals.Add(new(request.Id, vendor, reason, Array.AsReadOnly(matches), Array.AsReadOnly(suggestions), vendorChoice,
                !vendorChoice && matches.Length > 1 && destination == null, destination?.Id, destination?.Label, row, null, analysis.LastRow,
                Array.AsReadOnly(profile.Fields.Select(f => new MappedField(f.Key, f.Header, analysis.Columns[f.Key], f.Kind, values.GetValueOrDefault(f.Key, ""))).ToArray()), duplicates.AsReadOnly()));
        }
        var ready = proposals.All(p => p.BaseInsertionRow != null);
        if (ready)
        {
            // Base coordinates remain frozen; show final positions in stable ascending-boundary/input order.
            var ordered = proposals.Select((p, i) => (Proposal: p, Index: i)).OrderBy(x => x.Proposal.BaseInsertionRow).ThenBy(x => x.Index).ToArray();
            for (var offset = 0; offset < ordered.Length; offset++) proposals[ordered[offset].Index] = ordered[offset].Proposal with { ProposedRow = ordered[offset].Proposal.BaseInsertionRow + offset };
        }
        return new(analysis.SessionId, analysis.WorksheetId, analysis.Revision, analysis.SourceSha256, profile.Id, analysis.ProfileHash, ready, false, InsertionBlock,
            Array.AsReadOnly(new[] { "Review destination, entity identity, mapped values and affected rows", "Explicitly choose ambiguous entity/section matches", "Review every duplicate warning without automatic discard", "Explicit approval before any future structural mutation" }),
            Array.AsReadOnly(new[] { "The experimental oracle supports only plain rows and restricted same-sheet formulas", "Names, tables, merges and advanced structures block structural experiments", "New rows require a uniform explicit style template with unrelated constants cleared", "This proposal neither modifies nor saves the workbook; production Apply remains disabled" }), proposals.AsReadOnly());
    }

    private static Dictionary<string, string> ValidateValues(WorkflowRecord record, WorkflowProfile profile)
    {
        if (record.Values == null || record.Values.Count > profile.Fields.Count || record.Values.Keys.Any(k => !profile.Fields.Any(f => f.Key == k))) throw WorkflowProfiles.Error("INVALID_RECORD", "The record contains unsupported field mappings.");
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var field in profile.Fields)
        {
            var value = record.Values.GetValueOrDefault(field.Key, "");
            if (value == null || value.Length > field.MaxLength) throw WorkflowProfiles.Error("INVALID_RECORD", $"Field '{field.Header}' exceeds its permitted length.");
            try { XmlConvert.VerifyXmlChars(value); } catch (XmlException) { throw WorkflowProfiles.Error("INVALID_RECORD", "A record contains invalid text characters."); }
            value = value.Trim();
            if (field.Required && value.Length == 0) throw WorkflowProfiles.Error("INVALID_RECORD", $"Field '{field.Header}' is required.");
            if (value.Length > 0 && field.Kind == "decimal")
            {
                if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number)
                    || number < 0 || number > 1000000000000m || (decimal.GetBits(number)[3] >> 16 & 0xFF) > 4)
                    throw WorkflowProfiles.Error("INVALID_RECORD", "Decimal fields require a nonnegative decimal-point number, at most four decimal places and at most one trillion.");
                value = number.ToString(CultureInfo.InvariantCulture);
            }
            if (value.Length > 0 && field.Kind == "date" && !DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
                throw WorkflowProfiles.Error("INVALID_RECORD", "Dates must use YYYY-MM-DD.");
            values[field.Key] = value;
        }
        if (!profile.IdentifierFields.Any(k => values[k].Length > 0)) throw WorkflowProfiles.Error("INVALID_RECORD", "At least one configured record identifier is required.");
        return values;
    }
    private static bool Similar(string a, string b) => Math.Min(a.Length, b.Length) >= 4 && (a.StartsWith(b, StringComparison.Ordinal) || b.StartsWith(a, StringComparison.Ordinal)
        || a.Split(' ').Intersect(b.Split(' '), StringComparer.Ordinal).Any(word => word.Length >= 4));
    private static string[] MatchingIdentifiers(IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b, WorkflowProfile p) => p.IdentifierFields
        .Where(k => !string.IsNullOrWhiteSpace(a.GetValueOrDefault(k)) && WorkflowProfiles.Normalize(a[k], p) == WorkflowProfiles.Normalize(b.GetValueOrDefault(k, ""), p)).ToArray();
    private static string Explain(string[] ids, IReadOnlyDictionary<string, string> a, IReadOnlyDictionary<string, string> b, WorkflowProfile p)
    {
        var explanation = "Same normalized entity and matching " + string.Join(", ", ids.Select(k => p.Fields.Single(f => f.Key == k).Header))
            + ". Context: " + string.Join("; ", p.ContextFields.Select(k => $"{k}: submitted '{a.GetValueOrDefault(k, "")}', existing '{b.GetValueOrDefault(k, "")}'"));
        return explanation.Length <= 1024 ? explanation : explanation[..990] + " [context shortened]";
    }

    // An engine contract guard, not a candidate editor. Even a forged CanApply=true cannot mutate a session.
    public static WorkbookInfo RejectStructuralInsertion(IWorkbookSession session, WorkflowPlan plan, bool approved, bool duplicatesReviewed, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); var info = session.Info;
        if (plan.SessionId != info.SessionId || !info.Worksheets.Any(s => s.Id == plan.WorksheetId) || plan.SourceSha256 != info.SourceSha256)
            throw WorkflowProfiles.Error("INVALID_PROPOSAL", "Proposal identity does not match this worksheet session.");
        if (plan.Revision != info.Revision) throw WorkflowProfiles.Error("STALE_REVISION", "The worksheet changed after review. Regenerate the proposal.");
        if (plan.ProfileHash != WorkflowProfiles.Hash(WorkflowProfiles.Get(plan.ProfileId))) throw WorkflowProfiles.Error("STALE_PROFILE", "The approved profile changed after review.");
        if (!approved || !plan.ReadyForReview || (plan.Records.Any(r => r.Duplicates.Count > 0) && !duplicatesReviewed)) throw WorkflowProfiles.Error("REVIEW_REQUIRED", "Resolve all choices and review values and duplicate warnings before structural approval.");
        throw WorkflowProfiles.Error("ROW_INSERTION_DISABLED", InsertionBlock);
    }
}
