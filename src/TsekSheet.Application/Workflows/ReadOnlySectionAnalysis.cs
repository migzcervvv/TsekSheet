using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Workflows;

public sealed record ReadOnlyHeaderField(string Key, string Header, IReadOnlyList<string>? HeaderAliases = null);
public sealed record ReadOnlyRowMarker(string Text, IReadOnlyList<int> Columns, bool Prefix = false);
public sealed record ReadOnlyBoundary(string Kind, ReadOnlyRowMarker? Marker = null, int? ExplicitRow = null, string? FormulaField = null);
public sealed record ReadOnlySectionRule(string Id, string Label, string? ParentId, ReadOnlyRowMarker Heading,
    ReadOnlyBoundary End, int DataStartOffset = 1);
public sealed record ReadOnlySectionProfile(string Id, int Version, int StartRow, int EndRow, IReadOnlyList<int> Columns,
    IReadOnlyList<ReadOnlyHeaderField> Fields, string EntityField, IReadOnlyList<string> IdentifierFields,
    IReadOnlyList<string> ContextFields, IReadOnlyList<ReadOnlySectionRule> Sections,
    IReadOnlyList<ReadOnlyRowMarker> SummaryMarkers, IReadOnlyList<ReadOnlyRowMarker> ExclusionMarkers,
    bool AllowRepeatedHeaders = true);
public sealed record ReadOnlyAnalysisDiagnostic(string Code, string Message, int? Row = null, string? SectionId = null);
public sealed record ReadOnlySection(string Id, string Label, string? ParentId, int? HeadingRow, int? FirstDataRow,
    IReadOnlyList<int> SubtotalCandidates, int? SubtotalRow, bool BoundaryRuleMatched)
{
    public string Status => BoundaryRuleMatched ? "Provisional boundary rule matched" : "Unverified boundary";
    public bool CanInsert => false;
}
public sealed record ReadOnlyRecord(int Row, string? Entity, string? NormalizedEntity, string? SectionId, string Classification,
    IReadOnlyDictionary<string, string> Identifiers, bool Hidden, bool MarkerConflict = false);
public sealed record ReadOnlyVendor(string Entity, IReadOnlyList<int> Rows, IReadOnlyList<string> SectionIds, bool SectionCoverageComplete = false);
public sealed record ReadOnlyDuplicate(string Entity, string Identifier, IReadOnlyList<int> Rows, IReadOnlyList<string> Fields);
public sealed record ReadOnlySimilarity(string FirstEntity, string SecondEntity, string Reason);
public sealed record ReadOnlySummaryDependency(int Row, int Column, string FormulaStatus, IReadOnlyList<string> LocalReferences, bool Complete);
public sealed record ReadOnlySubtotalCandidate(int Row, int Column, string FormulaStatus, bool ConfiguredBoundary);
public sealed record ReadOnlyAnalysisCoverage(int StartRow, int EndRow, IReadOnlyList<int> Columns, int RowsCaptured,
    int CellsCaptured, int ObservedLastRow, int ObservedLastColumn, int HiddenRowsIncluded, long AccountedBytes,
    bool EntireWorksheetRowsCaptured, bool RoleColumnsComplete, bool RecordCoverageComplete);
public sealed record ReadOnlySectionReport(Guid SessionId, Guid WorksheetId, int Revision, string SourceSha256, string ProfileId,
    string ProfileSha256, ReadOnlyAnalysisCoverage Coverage, IReadOnlyList<int> HeaderRows, IReadOnlyDictionary<string, int> FieldColumns,
    IReadOnlyList<ReadOnlySection> Sections, IReadOnlyList<int> SummaryRows, IReadOnlyList<int> ExcludedRows,
    IReadOnlyList<int> EntityOnlyRows, IReadOnlyList<int> UnassignedRows, IReadOnlyList<ReadOnlyRecord> Records,
    IReadOnlyList<ReadOnlyVendor> Vendors, IReadOnlyList<ReadOnlyDuplicate> PotentialDuplicates, IReadOnlyList<ReadOnlySimilarity> SimilarNames,
    IReadOnlyList<ReadOnlySummaryDependency> SummaryDependencies, IReadOnlyList<ReadOnlySubtotalCandidate> SubtotalCandidates, IReadOnlyList<ReadOnlyAnalysisDiagnostic> Diagnostics,
    bool HierarchyVerified, bool IdentitiesVerified, bool CanInsert, double CaptureMilliseconds, double AnalysisMilliseconds);

// Independent read-only diagnostics. No WorkflowPlan, candidate, approval or mutation capability is returned.
public static class ReadOnlySectionAnalysis
{
    private static readonly JsonSerializerOptions ProfileJson = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow, MaxDepth = 12 };
    private static WorkbookException Error(string message) => new("READ_ONLY_ANALYSIS", message);
    private static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim().ToUpperInvariant(), @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string Fingerprint(ReadOnlySectionProfile profile) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile))));
    public static ReadOnlySectionProfile ParseProfile(string json)
    {
        if (json == null || json.Length > 65536) throw Error("Read-only profile text exceeds its bound.");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            void Duplicates(JsonElement e)
            {
                if (e.ValueKind == JsonValueKind.Object)
                {
                    var keys = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var p in e.EnumerateObject()) { if (!keys.Add(p.Name)) throw Error("Duplicate read-only profile member."); Duplicates(p.Value); }
                }
                else if (e.ValueKind == JsonValueKind.Array) foreach (var item in e.EnumerateArray()) Duplicates(item);
            }
            Duplicates(doc.RootElement);
            var result = JsonSerializer.Deserialize<ReadOnlySectionProfile>(json, ProfileJson) ?? throw Error("Read-only profile is empty.");
            Validate(result); return result;
        }
        catch (JsonException) { throw Error("Malformed or unknown read-only profile members."); }
    }

    public static void Validate(ReadOnlySectionProfile p)
    {
        bool Label(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128 && !s.Any(char.IsControl);
        bool Key(string? s) => Label(s) && s!.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        void Check(bool c) { if (!c) throw Error("Invalid or over-budget read-only section profile."); }
        Check(p != null && Key(p.Id) && p.Version > 0 && p.StartRow >= 1 && p.EndRow >= p.StartRow && p.EndRow <= 1048576
            && (long)p.EndRow - p.StartRow + 1 <= 2000 && p.Columns is { Count: >= 1 and <= 32 });
        Check(p!.Columns.All(c => c is >= 1 and <= 16384) && p.Columns.Distinct().Count() == p.Columns.Count
            && ((long)p.EndRow - p.StartRow + 1) * p.Columns.Count <= 40000);
        Check(p.Fields is { Count: >= 3 and <= 16 } && p.Fields.All(f => f != null && Key(f.Key) && Label(f.Header))
            && p.Fields.Select(f => f.Key).Distinct().Count() == p.Fields.Count
            && p.Fields.Select(f => Normalize(f.Header)).Distinct().Count() == p.Fields.Count);
        Check(p.Fields.All(f => f.HeaderAliases == null || f.HeaderAliases.Count <= 8 && f.HeaderAliases.All(Label)));
        var headerOwners = p.Fields.SelectMany(f => (f.HeaderAliases ?? []).Append(f.Header).Select(s => (Text: Normalize(s), f.Key))).GroupBy(p => p.Text);
        Check(headerOwners.All(g => g.Select(p => p.Key).Distinct().Count() == 1));
        var fields = p.Fields.Select(f => f.Key).ToHashSet();
        Check(fields.Contains(p.EntityField) && p.IdentifierFields is { Count: >= 1 and <= 4 }
            && p.IdentifierFields.All(k => fields.Contains(k) && k != p.EntityField) && p.IdentifierFields.Distinct().Count() == p.IdentifierFields.Count
            && p.ContextFields is { Count: <= 8 } && p.ContextFields.All(fields.Contains) && p.ContextFields.Distinct().Count() == p.ContextFields.Count);
        Check(p.Sections is { Count: >= 1 and <= 32 } && p.Sections.All(s => s != null && Key(s.Id) && Label(s.Label)
            && (s.ParentId == null || Key(s.ParentId)) && s.DataStartOffset is >= 1 and <= 20 && s.End != null)
            && p.Sections.Select(s => s.Id).Distinct().Count() == p.Sections.Count);
        bool Marker(ReadOnlyRowMarker? m) => m != null && Label(m.Text) && m.Columns is { Count: >= 1 and <= 32 }
            && m.Columns.Distinct().Count() == m.Columns.Count && m.Columns.All(p.Columns.Contains);
        Check(p.SummaryMarkers is { Count: <= 32 } && p.SummaryMarkers.All(Marker) && p.ExclusionMarkers is { Count: <= 32 } && p.ExclusionMarkers.All(Marker));
        foreach (var section in p.Sections)
        {
            Check(Marker(section.Heading));
            Check(section.ParentId == null || p.Sections.Any(s => s.Id == section.ParentId && s.Id != section.Id));
            Check(section.End.Kind switch
            {
                "marker" => Marker(section.End.Marker),
                "explicit-row" => section.End.ExplicitRow >= p.StartRow && section.End.ExplicitRow <= p.EndRow,
                "contiguous-sum" => section.End.FormulaField != null && fields.Contains(section.End.FormulaField),
                _ => false
            });
            var parents = new HashSet<string> { section.Id }; var parent = section.ParentId;
            while (parent != null) { Check(parents.Add(parent)); parent = p.Sections.Single(s => s.Id == parent).ParentId; }
        }
    }

    public static ReadOnlySectionReport Read(IWorkbookSession session, Guid worksheetId, int expectedRevision, ReadOnlySectionProfile profile, CancellationToken token = default)
    {
        Validate(profile); token.ThrowIfCancellationRequested();
        var info = session.Info;
        if (info.Compatibility == null || session is not IInspectionAnalysisSession inspection) throw Error("This separate analysis requires an inspection session.");
        if (info.Revision != expectedRevision || !info.Worksheets.Any(s => s.Id == worksheetId)) throw Error("Stale or unknown inspection worksheet/revision.");
        var profileHash = Fingerprint(profile); var watch = Stopwatch.StartNew();
        var region = inspection.CaptureInspectionRegion(new(info.SessionId, worksheetId, expectedRevision, info.SourceSha256,
            profile.StartRow, profile.EndRow, profile.Columns), token);
        if (region.SessionId != info.SessionId || region.WorksheetId != worksheetId || region.Revision != expectedRevision || region.SourceSha256 != info.SourceSha256)
            throw Error("Capture returned a stale or mismatched inspection identity.");
        var captureMs = watch.Elapsed.TotalMilliseconds;
        var report = Analyze(region, profile, token);
        if (session.Info.SessionId != info.SessionId || session.Info.Revision != expectedRevision || session.Info.SourceSha256 != info.SourceSha256 || Fingerprint(profile) != profileHash)
            throw Error("Inspection/profile identity changed during analysis.");
        return report with { CaptureMilliseconds = captureMs };
    }

    public static ReadOnlySectionReport Analyze(InspectionRegion region, ReadOnlySectionProfile profile, CancellationToken token = default,
        SectionReviewFacts? review = null)
    {
        Validate(profile); token.ThrowIfCancellationRequested(); var watch = Stopwatch.StartNew();
        var profileHash = Fingerprint(profile);
        if (region.StartRow != profile.StartRow || region.EndRow != profile.EndRow || !region.Columns.Order().SequenceEqual(profile.Columns.Order())
            || region.Cells.Count != ((long)region.EndRow - region.StartRow + 1) * region.Columns.Count || region.AccountedBytes > 16 * 1024 * 1024)
            throw Error("Incomplete or over-budget analysis capture.");
        var cells = new Dictionary<(int, int), CellInfo>();
        foreach (var cell in region.Cells)
        {
            token.ThrowIfCancellationRequested();
            if (cell.Row < region.StartRow || cell.Row > region.EndRow || !region.Columns.Contains(cell.Column) || !cells.TryAdd((cell.Row, cell.Column), cell))
                throw Error("Duplicate/out-of-range analysis cells.");
        }
        var diagnostics = new List<ReadOnlyAnalysisDiagnostic>();
        void Warn(string code, string message, int? row = null, string? section = null)
        { if (diagnostics.Count >= 4096) throw Error("Analysis diagnostic budget exceeded."); diagnostics.Add(new(code, message, row, section)); }
        CellInfo Cell(int r, int c) => cells[(r, c)];
        bool Literal(CellInfo c) => c.Formula == null && !c.Truncated && !string.IsNullOrWhiteSpace(c.Value);
        var normalized = cells.ToDictionary(p => p.Key, p => Literal(p.Value) ? Normalize(p.Value.Value) : "");
        var headerText = profile.Fields.ToDictionary(f => f.Key, f => (f.HeaderAliases ?? []).Append(f.Header).Select(Normalize).ToHashSet());
        var rows = Enumerable.Range(region.StartRow, region.EndRow - region.StartRow + 1).ToArray();
        bool Matches(int row, ReadOnlyRowMarker marker) => marker.Columns.Any(c => Literal(Cell(row, c)) &&
            (marker.Prefix ? normalized[(row, c)].StartsWith(Normalize(marker.Text), StringComparison.Ordinal) : normalized[(row, c)] == Normalize(marker.Text)));
        int[] Match(ReadOnlyRowMarker m) => rows.Where(r => Matches(r, m)).ToArray();
        var headers = new List<int>(); var headerLike = new HashSet<int>(); var columns = new Dictionary<string, int>();
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested(); var map = new Dictionary<string, int>();
            foreach (var field in profile.Fields)
            {
                var matches = region.Columns.Where(c => headerText[field.Key].Contains(normalized[(row, c)])).ToArray();
                if (matches.Length == 1) map.Add(field.Key, matches[0]);
                else if (matches.Length > 1) Warn("AMBIGUOUS_HEADER", "A field header appears in multiple captured columns.", row);
            }
            if (review?.Headers.TryGetValue(row, out var reviewedMap) == true) map = new(reviewedMap);
            if (map.Count != profile.Fields.Count)
            {
                if (map.Count >= Math.Max(2, (profile.Fields.Count + 1) / 2))
                { headerLike.Add(row); Warn("INCOMPLETE_HEADER", "A header-like row lacks a complete mapping; it is not interpreted as a vendor record.", row); }
                continue;
            }
            headers.Add(row);
            if (map.Values.Distinct().Count() != map.Count) Warn("AMBIGUOUS_HEADER", "Multiple field roles resolve to the same column.", row);
            if (columns.Count == 0) columns = map;
            else if (map.Any(p => columns[p.Key] != p.Value)) Warn("SHIFTED_HEADER", "Repeated header mappings differ; no field mapping was silently changed.", row);
        }
        if (headers.Count == 0) Warn("MISSING_HEADERS", "No complete unique field header mapping was found.");
        if (headers.Count > 1 && !profile.AllowRepeatedHeaders) Warn("REPEATED_HEADERS", "This profile does not allow repeated headers.");
        var summaries = profile.SummaryMarkers.SelectMany(Match).Distinct().Order().ToArray();
        foreach (var marker in profile.SummaryMarkers) if (Match(marker).Length == 0) Warn("MISSING_SUMMARY_MARKER", "A configured summary marker was not found.");
        var excluded = profile.ExclusionMarkers.SelectMany(Match).Distinct().Order().ToArray();
        var sections = new List<ReadOnlySection>();
        foreach (var rule in profile.Sections)
        {
            token.ThrowIfCancellationRequested(); var headings = Match(rule.Heading);
            int? heading = review?.Headings.TryGetValue(rule.Id, out var reviewedHeading) == true ? reviewedHeading : headings.Length == 1 ? headings[0] : null;
            if (heading == null) Warn("AMBIGUOUS_HEADING", "Exactly one configured heading is required.", null, rule.Id);
            int[] ends = [];
            if (heading != null)
            {
                var first = heading.Value + rule.DataStartOffset;
                if (rule.End.Kind == "marker") ends = Match(rule.End.Marker!).Where(r => r >= first).ToArray();
                else if (rule.End.Kind == "explicit-row") ends = [rule.End.ExplicitRow!.Value];
                else if (columns.TryGetValue(rule.End.FormulaField!, out var col))
                {
                    // Exact range intent is configured. Do not choose nearest/first SUM, and retain every candidate.
                    ends = rows.Where(r => r >= first && ContiguousSum(Cell(r, col), col, first, r - 1)).ToArray();
                }
                if (review?.Ends.TryGetValue(rule.Id, out var reviewedEnd) == true) ends = [reviewedEnd];
                if (ends.Length != 1 || ends[0] < first || ends[0] > profile.EndRow)
                    Warn("AMBIGUOUS_BOUNDARY", "A single configured end boundary was not established; all candidates are retained.", heading, rule.Id);
            }
            var valid = heading != null && ends.Length == 1 && ends[0] >= heading.Value + rule.DataStartOffset && ends[0] <= profile.EndRow;
            sections.Add(new(rule.Id, rule.Label, rule.ParentId, heading, heading == null ? null : heading + rule.DataStartOffset, ends, valid ? ends[0] : null, valid));
        }
        bool IsAncestor(string parent, string child)
        { var next = profile.Sections.Single(s => s.Id == child).ParentId; while (next != null) { if (next == parent) return true; next = profile.Sections.Single(s => s.Id == next).ParentId; } return false; }
        foreach (var section in sections.Where(s => s.ParentId != null && s.BoundaryRuleMatched))
        {
            var parent = sections.Single(s => s.Id == section.ParentId);
            if (!parent.BoundaryRuleMatched || section.HeadingRow <= parent.HeadingRow || section.SubtotalRow >= parent.SubtotalRow)
                Warn("INVALID_NESTING", "A configured child is not strictly contained in its verified parent.", section.HeadingRow, section.Id);
        }
        for (var i = 0; i < sections.Count; i++) for (var j = i + 1; j < sections.Count; j++)
        {
            var a = sections[i]; var b = sections[j]; if (!a.BoundaryRuleMatched || !b.BoundaryRuleMatched) continue;
            bool overlap = a.HeadingRow <= b.SubtotalRow && b.HeadingRow <= a.SubtotalRow;
            bool nested = IsAncestor(a.Id, b.Id) && a.HeadingRow < b.HeadingRow && a.SubtotalRow > b.SubtotalRow
                || IsAncestor(b.Id, a.Id) && b.HeadingRow < a.HeadingRow && b.SubtotalRow > a.SubtotalRow;
            if (overlap && !nested) Warn("OVERLAPPING_REGIONS", "Regions overlap without a valid configured parent/child relationship.", a.HeadingRow, a.Id);
        }
        var reviewedNonRecords = review?.Classifications.Keys.ToHashSet() ?? [];
        summaries = summaries.Concat(review?.Classifications.Where(p => p.Value == "summary").Select(p => p.Key) ?? []).Distinct().Order().ToArray();
        excluded = excluded.Concat(review?.Classifications.Where(p => p.Value == "exclusion").Select(p => p.Key) ?? []).Distinct().Order().ToArray();
        var markerRows = sections.SelectMany(s => s.HeadingRow is { } h ? s.SubtotalRow is { } e ? new[] { h, e } : new[] { h } : []).Concat(summaries).Concat(excluded).ToHashSet();
        var records = new List<ReadOnlyRecord>(); var notes = new List<int>(); var unassigned = new List<int>(); var hidden = region.HiddenRows.ToHashSet();
        if (columns.Count == profile.Fields.Count)
        foreach (var row in rows)
        {
            token.ThrowIfCancellationRequested(); if (headers.Contains(row) || headerLike.Contains(row)) continue;
            var entity = Cell(row, columns[profile.EntityField]); var ids = profile.IdentifierFields.ToDictionary(k => k, k => Cell(row, columns[k]));
            var role = ids.Values.Append(entity).ToArray();
            bool hasId = ids.Values.Any(c => c.Formula != null || !string.IsNullOrWhiteSpace(c.Value));
            bool hasContext = profile.ContextFields.Any(k => Literal(Cell(row, columns[k])));
            bool hasEntity = !string.IsNullOrWhiteSpace(entity.Value);
            var markerConflict = markerRows.Contains(row) && (hasId || hasEntity && hasContext) && !reviewedNonRecords.Contains(row);
            if (markerRows.Contains(row))
            {
                if (markerConflict) Warn("MARKER_RESEMBLES_RECORD", "A heading/subtotal/summary/excluded row resembles a record and cannot be silently excluded.", row);
                else if (!reviewedNonRecords.Contains(row)) continue;
            }
            if (!hasId && !(hasEntity && hasContext) && !role.Any(c => c.Formula != null)) { if (hasEntity && entity.Formula == null) notes.Add(row); continue; }
            string classification = "Literal identity";
            if (role.Any(c => c.Formula != null)) classification = "Formula-derived identity";
            else if (role.Any(c => c.Truncated) || entity.Value.Length > 128 || ids.Values.Any(c => c.Value.Length > 64)) classification = "Truncated or overlength identity";
            else if (hasId && (!hasEntity || entity.Kind != "Text")) classification = "Missing literal vendor identity";
            else if (!hasId) classification = "Context without identifier";
            else if (markerConflict) classification = "Marker resembles record";
            var confirmedIdentity = review?.Identities.GetValueOrDefault(row);
            if (confirmedIdentity != null) classification = "Reviewer-confirmed identity";
            if (reviewedNonRecords.Contains(row)) classification = "Reviewed non-record: " + review!.Classifications[row];
            if (classification != "Literal identity" && classification != "Reviewer-confirmed identity" && !reviewedNonRecords.Contains(row)) Warn("UNRESOLVED_IDENTITY", classification + "; human verification required, no fill-down performed.", row);
            var owners = sections.Where(s => s.BoundaryRuleMatched && row >= s.FirstDataRow && row < s.SubtotalRow).ToArray();
            var deepest = owners.Where(s => !owners.Any(other => IsAncestor(s.Id, other.Id))).ToArray();
            var owner = deepest.Length == 1 ? deepest[0].Id : null;
            if (owner == null && !reviewedNonRecords.Contains(row)) { unassigned.Add(row); Warn("UNASSIGNED_RECORD", "Record-shaped content has no single verified section owner.", row); }
            var literal = classification == "Literal identity" || reviewedNonRecords.Contains(row) && Literal(entity) && entity.Kind == "Text" && entity.Value.Length <= 128 && ids.Values.All(c => c.Formula == null && !c.Truncated && c.Value.Length <= 64);
            records.Add(new(row, confirmedIdentity ?? (literal ? entity.Value : null), confirmedIdentity != null ? Normalize(confirmedIdentity) : literal ? Normalize(entity.Value) : null, owner, classification,
                ids.ToDictionary(p => p.Key, p => p.Value.Formula == null && !p.Value.Truncated ? p.Value.Value : ""), hidden.Contains(row), markerConflict));
        }
        if (records.Count > 2000) throw Error("Record diagnostic budget exceeded.");
        var vendorGroups = records.Where(r => r.NormalizedEntity != null).GroupBy(r => r.NormalizedEntity!).ToArray();
        if (vendorGroups.Length > 256) throw Error("Vendor similarity analysis exceeds its 256-identity bound.");
        var vendors = vendorGroups.Select(g => new ReadOnlyVendor(g.Key, g.Select(r => r.Row).ToArray(), g.Where(r => r.SectionId != null).Select(r => r.SectionId!).Distinct().Order().ToArray())).ToArray();
        var duplicates = records.Where(r => r.NormalizedEntity != null).SelectMany(r => r.Identifiers.Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => new { Entity = r.NormalizedEntity!, Identifier = Normalize(p.Value), r.Row, Field = p.Key }))
            .GroupBy(r => (r.Entity, r.Identifier)).Where(g => g.Select(r => r.Row).Distinct().Count() > 1)
            .Select(g => new ReadOnlyDuplicate(g.Key.Entity, g.Key.Identifier, g.Select(r => r.Row).Distinct().Order().ToArray(), g.Select(r => r.Field).Distinct().Order().ToArray())).ToArray();
        var similar = new List<ReadOnlySimilarity>();
        for (var i = 0; i < vendors.Length; i++) for (var j = i + 1; j < vendors.Length; j++)
        {
            token.ThrowIfCancellationRequested();
            string Compact(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());
            var a = vendors[i].Entity; var b = vendors[j].Entity;
            if (Compact(a) == Compact(b) || OneEditApart(a, b)) similar.Add(new(a, b, "Similar literal names; distinct identities retained pending human review"));
            if (similar.Count > 512) throw Error("Similarity diagnostic budget exceeded.");
        }
        var dependencies = new List<ReadOnlySummaryDependency>();
        // Section containment alone does not explain a row. Retain residual content in
        // any captured role column, without inferring a payable or a vendor identity.
        var classifiedRows = headers.Concat(headerLike).Concat(markerRows).Concat(reviewedNonRecords)
            .Concat(notes).Concat(records.Select(r => r.Row)).ToHashSet();
        foreach (var row in rows.Where(r => !classifiedRows.Contains(r)))
        {
            token.ThrowIfCancellationRequested();
            if (region.Columns.Any(col => Cell(row, col).Formula != null || Cell(row, col).Truncated || !string.IsNullOrWhiteSpace(Cell(row, col).Value)))
            {
                unassigned.Add(row);
                Warn("UNCLASSIFIED_ROW", "Meaningful captured role content has no record or non-record classification; human review is required.", row);
            }
        }
        if (columns.Count == profile.Fields.Count)
        foreach (var row in rows.Where(r => !headers.Contains(r) && !markerRows.Contains(r) && !reviewedNonRecords.Contains(r)
            && !sections.Any(s => s.BoundaryRuleMatched && r >= s.FirstDataRow && r < s.SubtotalRow)))
        {
            token.ThrowIfCancellationRequested();
            if (profile.ContextFields.Any(k => Cell(row, columns[k]).Formula != null))
            {
                if (!unassigned.Contains(row)) unassigned.Add(row);
                Warn("UNCLASSIFIED_SUMMARY", "Formula-bearing context outside verified regions has no configured summary classification.", row);
            }
        }
        var subtotalCandidates = columns.Count != profile.Fields.Count ? [] : region.Cells.Where(c => c.Formula?.StartsWith("SUM(", StringComparison.Ordinal) == true
                && profile.ContextFields.Concat(profile.Sections.Where(s => s.End.FormulaField != null).Select(s => s.End.FormulaField!)).Any(k => columns[k] == c.Column))
            .Select(c => new ReadOnlySubtotalCandidate(c.Row, c.Column, c.Interpretation?.FormulaStatus ?? "Stored formula; unverified", sections.Any(s => s.SubtotalRow == c.Row))).ToArray();
        if (subtotalCandidates.Length > 2048) throw Error("Subtotal candidate diagnostic budget exceeded.");
        foreach (var row in summaries.Concat(sections.Where(s => s.SubtotalRow != null).Select(s => s.SubtotalRow!.Value)).Distinct())
        foreach (var col in region.Columns)
        {
            token.ThrowIfCancellationRequested(); var c = Cell(row, col); if (c.Formula == null) continue;
            var refs = Regex.Matches(c.Formula, @"(?<![A-Za-z0-9_])\$?[A-Z]{1,3}\$?[1-9][0-9]{0,6}(?![A-Za-z0-9_])", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)).Select(m => m.Value).Distinct().Take(129).ToArray();
            var status = c.Interpretation?.FormulaStatus ?? "Stored formula; unverified";
            dependencies.Add(new(row, col, status, refs.Take(128).ToArray(), false));
            if (dependencies.Count > 1024) throw Error("Summary dependency diagnostic budget exceeded.");
        }
        var observedLastRow = Math.Max(region.Scan.LastRow, region.Scan.LastRowElement);
        bool wholeRows = region.StartRow == 1 && region.EndRow >= observedLastRow;
        if (!wholeRows) Warn("INCOMPLETE_ROW_COVERAGE", "Capture does not cover all observed worksheet rows; no whole-worksheet search claim is permitted.");
        bool roleComplete = columns.Count == profile.Fields.Count && !region.Cells.Any(c => c.Truncated);
        if (!roleComplete) Warn("INCOMPLETE_ROLE_COVERAGE", "Missing field mapping or truncated captured content prevents complete role analysis.");
        bool coverage = wholeRows && roleComplete && unassigned.Count == 0 && !diagnostics.Any(d => d.Code is "MARKER_RESEMBLES_RECORD" or "SHIFTED_HEADER" or "AMBIGUOUS_HEADER" or "REPEATED_HEADERS" or "INCOMPLETE_HEADER");
        bool hierarchy = coverage && sections.All(s => s.BoundaryRuleMatched) && !diagnostics.Any(d => d.Code is "INVALID_NESTING" or "OVERLAPPING_REGIONS" or "AMBIGUOUS_BOUNDARY" or "AMBIGUOUS_HEADING" or "MISSING_SUMMARY_MARKER");
        bool identities = coverage && records.All(r => r.Classification is "Literal identity" or "Reviewer-confirmed identity" || reviewedNonRecords.Contains(r.Row)) && !diagnostics.Any(d => d.Code == "MARKER_RESEMBLES_RECORD");
        vendors = vendors.Select(v => v with { SectionCoverageComplete = hierarchy && identities && records.Where(r => r.NormalizedEntity == v.Entity).All(r => r.SectionId != null) }).ToArray();
        token.ThrowIfCancellationRequested(); if (Fingerprint(profile) != profileHash) throw Error("Profile changed during pure analysis.");
        return new(region.SessionId, region.WorksheetId, region.Revision, region.SourceSha256, profile.Id, profileHash,
            new(region.StartRow, region.EndRow, region.Columns, rows.Length, region.Cells.Count, observedLastRow, region.Scan.LastColumn, region.HiddenRows.Count,
                region.AccountedBytes, wholeRows, roleComplete, coverage), headers, columns, sections, summaries, excluded, notes, unassigned, records,
            vendors, duplicates, similar, dependencies, subtotalCandidates, diagnostics, hierarchy, identities, false, 0, watch.Elapsed.TotalMilliseconds);
    }

    private static bool ContiguousSum(CellInfo cell, int column, int firstRow, int lastRow)
    {
        if (cell.Formula == null || cell.Truncated || cell.Interpretation?.FormulaStatus.StartsWith("Unsupported", StringComparison.Ordinal) == true) return false;
        var m = Regex.Match(cell.Formula, @"^SUM\((\$?[A-Z]{1,3})\$?([1-9][0-9]{0,6}):(\$?[A-Z]{1,3})\$?([1-9][0-9]{0,6})\)$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        int Col(string s) { int n = 0; foreach (var c in s.TrimStart('$')) n = n * 26 + c - 'A' + 1; return n; }
        return m.Success && Col(m.Groups[1].Value) == column && Col(m.Groups[3].Value) == column
            && int.Parse(m.Groups[2].Value) == firstRow && int.Parse(m.Groups[4].Value) == lastRow;
    }
    private static bool OneEditApart(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1) return false;
        int i = 0, j = 0, edits = 0;
        while (i < a.Length && j < b.Length)
        {
            if (a[i] == b[j]) { i++; j++; continue; }
            if (++edits > 1) return false;
            if (a.Length >= b.Length) i++; if (b.Length >= a.Length) j++;
        }
        return edits + (i < a.Length || j < b.Length ? 1 : 0) == 1;
    }
}
