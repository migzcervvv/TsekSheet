using System.Collections.ObjectModel;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Workflows;

public sealed record WorkflowRow(int Row, IReadOnlyDictionary<string, string> Values);
public sealed record WorksheetSection(string Id, string Label, int HeadingRow, int FirstDataRow, int SubtotalRow, IReadOnlyList<WorkflowRow> Records);
public sealed record WorksheetAnalysis(Guid SessionId, Guid WorksheetId, string WorksheetName, int Revision, string SourceSha256,
    string ProfileId, string ProfileHash, int LastRow, IReadOnlyDictionary<string, int> Columns, IReadOnlyList<WorksheetSection> Sections);

// All reads use the bounded session projection. No workbook library or mutable cell objects enter planning.
public static class WorksheetAnalysisReader
{
    public static WorksheetAnalysis Read(IWorkbookSession session, Guid worksheetId, int expectedRevision, WorkflowProfile profile, CancellationToken token = default)
    {
        WorkflowProfiles.Validate(profile); token.ThrowIfCancellationRequested();
        var info = session.Info;
        if (info.Revision != expectedRevision) throw WorkflowProfiles.Error("STALE_REVISION", "Refresh the worksheet before planning.");
        var sheet = info.Worksheets.SingleOrDefault(s => s.Id == worksheetId) ?? throw WorkflowProfiles.Error("UNKNOWN_WORKSHEET", "Worksheet does not belong to this session.");
        if (sheet.LastRow < 1 || sheet.LastRow > profile.MaxRows || sheet.LastColumn > profile.MaxColumns)
            throw WorkflowProfiles.Error("ANALYSIS_LIMIT", "The complete selected worksheet exceeds this profile's bounded scan. No partial vendor search was performed.");
        var cells = new Dictionary<(int Row, int Column), CellInfo>();
        for (var row = 1; row <= sheet.LastRow; row += 20)
        for (var col = 1; col <= profile.MaxColumns; col += 10)
        {
            token.ThrowIfCancellationRequested();
            foreach (var cell in session.ReadViewport(sheet.Id, row, col, Math.Min(20, sheet.LastRow - row + 1), Math.Min(10, profile.MaxColumns - col + 1)).Cells)
            {
                if (cell.Truncated) throw WorkflowProfiles.Error("ANALYSIS_UNSUPPORTED", "Truncated cell content cannot be used to classify records.");
                cells.Add((cell.Row, cell.Column), cell);
            }
        }
        var result = WorksheetSectionAnalyzer.Analyze(info, sheet, profile, cells, token);
        token.ThrowIfCancellationRequested();
        if (session.Info.Revision != expectedRevision) throw WorkflowProfiles.Error("STALE_REVISION", "The worksheet changed during analysis. Refresh and plan again.");
        return result;
    }
}

public static class WorksheetSectionAnalyzer
{
    public static WorksheetAnalysis Analyze(WorkbookInfo info, WorksheetInfo sheet, WorkflowProfile profile,
        IReadOnlyDictionary<(int Row, int Column), CellInfo> cells, CancellationToken token = default)
    {
        WorkflowProfiles.Validate(profile);
        if (sheet.LastRow < 1 || sheet.LastRow > profile.MaxRows || sheet.LastColumn > profile.MaxColumns || !info.Worksheets.Contains(sheet))
            throw WorkflowProfiles.Error("ANALYSIS_LIMIT", "A complete worksheet within the configured bounds is required.");
        if (cells.Count != sheet.LastRow * profile.MaxColumns || cells.Values.Any(c => c.Truncated))
            throw WorkflowProfiles.Error("INCOMPLETE_ANALYSIS", "Every bounded worksheet cell must be captured without truncation.");
        CellInfo Cell(int row, int col) => cells.TryGetValue((row, col), out var c) ? c : throw WorkflowProfiles.Error("INCOMPLETE_ANALYSIS", "Analysis requires a complete bounded worksheet capture.");
        string Literal(int row, int col) { var c = Cell(row, col); return c.Formula == null ? WorkflowProfiles.Normalize(c.Value, profile) : ""; }
        var headers = new List<(int Row, Dictionary<string, int> Columns)>();
        for (var row = 1; row <= sheet.LastRow; row++)
        {
            token.ThrowIfCancellationRequested();
            var columns = new Dictionary<string, int>();
            foreach (var field in profile.Fields)
            {
                var matches = Enumerable.Range(1, profile.MaxColumns).Where(c => Literal(row, c) == WorkflowProfiles.Normalize(field.Header, profile)).ToArray();
                if (matches.Length == 1) columns.Add(field.Key, matches[0]);
            }
            if (columns.Count == profile.Fields.Count) headers.Add((row, columns));
        }
        if (headers.Count != 1) throw WorkflowProfiles.Error("AMBIGUOUS_HEADERS", "Exactly one complete, unique header row is required by this profile.");
        var header = headers[0]; var markerColumn = header.Columns[profile.EntityField];
        var starts = profile.Sections.Select(rule =>
        {
            var matches = Enumerable.Range(header.Row + 1, sheet.LastRow - header.Row).Where(r => Literal(r, markerColumn) == WorkflowProfiles.Normalize(rule.Heading, profile)).ToArray();
            if (matches.Length != 1) throw WorkflowProfiles.Error("AMBIGUOUS_SECTION", $"Section '{rule.Label}' needs exactly one heading after the field headers.");
            return (Rule: rule, Row: matches[0]);
        }).OrderBy(s => s.Row).ToArray();
        var sections = new List<WorksheetSection>(); var classified = new HashSet<int>();
        for (var index = 0; index < starts.Length; index++)
        {
            var (rule, heading) = starts[index]; var end = index + 1 < starts.Length ? starts[index + 1].Row - 1 : sheet.LastRow;
            var subtotals = Enumerable.Range(heading + 1, end - heading).Where(r => Literal(r, markerColumn) == WorkflowProfiles.Normalize(rule.Subtotal, profile)).ToArray();
            if (subtotals.Length != 1 || subtotals[0] <= heading + 1)
                throw WorkflowProfiles.Error("UNSAFE_BOUNDARY", $"Section '{rule.Label}' requires one unambiguous subtotal and a nonempty bounded data area before the next section.");
            var subtotal = subtotals[0]; var records = new List<WorkflowRow>();
            foreach (var marker in new[] { heading, subtotal })
                if (profile.IdentifierFields.Any(k => { var cell = Cell(marker, header.Columns[k]); return cell.Formula != null || !string.IsNullOrWhiteSpace(cell.Value); }))
                    throw WorkflowProfiles.Error("UNSAFE_BOUNDARY", "A section marker also contains record identifiers. Resolve the ambiguous boundary before planning.");
            for (var row = heading + 1; row < subtotal; row++)
            {
                token.ThrowIfCancellationRequested(); classified.Add(row);
                if (Record(row, true) is { } record) records.Add(record);
            }
            classified.Add(heading); classified.Add(subtotal);
            sections.Add(new(rule.Id, rule.Label, heading, heading + 1, subtotal, records.AsReadOnly()));
        }
        // Never silently search only some sections when record-shaped rows exist elsewhere.
        for (var row = 1; row <= sheet.LastRow; row++)
        {
            token.ThrowIfCancellationRequested();
            if (row != header.Row && !classified.Contains(row) && Record(row, false) != null)
                throw WorkflowProfiles.Error("UNCLASSIFIED_RECORD", "Record-shaped content exists outside the configured section boundaries. Complete the profile before searching vendors.");
        }
        return new(info.SessionId, sheet.Id, sheet.Name, info.Revision, info.SourceSha256, profile.Id, WorkflowProfiles.Hash(profile), sheet.LastRow,
            new ReadOnlyDictionary<string, int>(header.Columns), sections.AsReadOnly());

        WorkflowRow? Record(int row, bool inside)
        {
            var roles = profile.IdentifierFields.Append(profile.EntityField).Select(k => Cell(row, header.Columns[k])).ToArray();
            if (roles.Any(c => c.Formula != null)) throw WorkflowProfiles.Error("AMBIGUOUS_RECORD", "Formula-derived vendor or identifier fields cannot be classified using cached values.");
            var entity = Cell(row, markerColumn);
            if (roles.Any(c => c.Truncated) || entity.Value.Length > profile.Fields.Single(f => f.Key == profile.EntityField).MaxLength
                || profile.IdentifierFields.Any(k => Cell(row, header.Columns[k]).Value.Length > profile.Fields.Single(f => f.Key == k).MaxLength))
                throw WorkflowProfiles.Error("ANALYSIS_UNSUPPORTED", "A vendor or identifier exceeds the profile's review limits.");
            var hasId = profile.IdentifierFields.Any(k => !string.IsNullOrWhiteSpace(Cell(row, header.Columns[k]).Value));
            if (!hasId)
            {
                if (inside && !string.IsNullOrWhiteSpace(entity.Value) && profile.ContextFields.Any(k => { var c = Cell(row, header.Columns[k]); return c.Formula == null && !string.IsNullOrWhiteSpace(c.Value); }))
                    throw WorkflowProfiles.Error("AMBIGUOUS_RECORD", "A possible record has contextual data but no identifier. It cannot be silently treated as a note.");
                return null; // Entity-only headings/notes and formula-only summaries are not records.
            }
            if (string.IsNullOrWhiteSpace(entity.Value) || entity.Kind != "Text") throw WorkflowProfiles.Error("AMBIGUOUS_RECORD", "An identified record requires a literal text entity name.");
            return new(row, new ReadOnlyDictionary<string, string>(header.Columns.ToDictionary(p => p.Key, p => Cell(row, p.Value).Formula == null ? Cell(row, p.Value).Value : "[formula; cached result unverified]")));
        }
    }
}
