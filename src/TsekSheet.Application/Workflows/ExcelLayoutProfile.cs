using System.Text.Json;
using System.Text.Json.Serialization;
using TsekSheet.Application.Excel;

namespace TsekSheet.Application.Workflows;

public sealed record HeaderAnchor(string Column, [property: JsonPropertyName("equals")] string Text);
public sealed record SheetMatch(int HeaderRow, IReadOnlyList<HeaderAnchor> Anchors);
public sealed record RowMarker(string Column, [property: JsonPropertyName("equals")] string Text,
    int Occurrence = 1, string? Note = null);
public sealed record SectionStart(int? HeaderRowOffset = null, RowMarker? AfterRow = null, int Offset = 0);
public sealed record SectionEnd(RowMarker RowBefore);
public sealed record LayoutSection(string Id, string Label, SectionStart Start, SectionEnd End,
    RowMarker SubtotalRow, string InsertMode, bool FillBlankRowsFirst);
public sealed record ProfileAccount(string Code, string Label, string Column, bool Vatable);
public sealed record LayoutField(string Key, string Label, string Kind, bool Required,
    string? Column = null, string? WritesTo = null);
public sealed record ExcelLayoutProfile(int SchemaVersion, string Id, int Revision, string DisplayName,
    SheetMatch SheetMatch, IReadOnlyList<LayoutField> Fields, IReadOnlyList<ProfileAccount> Accounts,
    IReadOnlyList<string> FormulaColumns, IReadOnlyList<LayoutSection> Sections,
    string BlankRowRule, IReadOnlyList<RowMarker> ProtectedRows,
    JsonElement? VatRule = null, JsonElement? Lookups = null)
{
    public const string SupportedBlankRule = "A data row is blank when every mapped input column (B-H and all account columns) is empty. Formula columns are ignored.";

    internal WorkflowProfile ToWorkflow()
    {
        if (Fields is null || Fields.Any(f => f is null)) throw WorkflowProfiles.Error("INVALID_PROFILE", "Profile fields are missing.");
        var p = new WorkflowProfile(SchemaVersion, Id, Revision, DisplayName, "excel-header-anchors",
            2000, 32, "", [], [], Fields.Select(f => new WorkflowField(f.Key, f.Label, f.Kind, f.Required, 256)).ToArray(),
            [], [], true, true, "", "", "", "", "disabled", "", true, false, Layout: this);
        Validate(p);
        return p;
    }

    internal static void Validate(WorkflowProfile profile)
    {
        var p = profile.Layout!;
        void Check(bool condition) { if (!condition) throw WorkflowProfiles.Error("INVALID_PROFILE", "Excel layout profile contains missing, ambiguous or unsupported configuration."); }
        bool Id(string? s) => s is { Length: > 0 and <= 48 } && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        bool Label(string? s) => s is { Length: > 0 and <= 128 } && !s.Any(char.IsControl);
        Check(p.SchemaVersion == 1 && p.Revision > 0 && Id(p.Id) && Label(p.DisplayName));
        Check(profile.Id == p.Id && profile.Revision == p.Revision && profile.SchemaVersion == p.SchemaVersion
            && profile.Fields.SequenceEqual(p.Fields.Select(f => new WorkflowField(f.Key, f.Label, f.Kind, f.Required, 256))));
        Check(p.SheetMatch is { HeaderRow: >= 1 and < 1048576, Anchors.Count: >= 1 and <= 32 });
        Check(p.Fields is { Count: >= 1 and <= 16 } && p.Accounts is { Count: >= 1 and <= 64 }
            && p.Sections is { Count: >= 1 and <= 16 } && p.FormulaColumns is { Count: <= 32 } && p.ProtectedRows is { Count: <= 32 });
        Check(p.Fields!.All(f => f is not null && Id(f.Key) && Label(f.Label) && f.Kind is "text" or "money" or "date" or "accountChoice"));
        Check(p.Fields.Select(f => f.Key).Distinct().Count() == p.Fields.Count);
        Check(p.Fields.Count(f => f.Kind == "accountChoice" && f.Required && f.Column is null && f.WritesTo is null) == 1);
        Check(p.Fields.Count(f => f.Kind == "money" && f.WritesTo == "selectedAccountColumn" && f.Column is null) == 1);
        Check(p.Fields.All(f => f.Kind == "accountChoice" || f.WritesTo == "selectedAccountColumn" || f.Column is not null));
        Check(p.Accounts!.All(a => a is not null && Label(a.Code) && Label(a.Label)));
        // Account codes may repeat for VAT/non-VAT destinations; the column is the stable choice ID.
        var inputs = p.Fields.Where(f => f.Column is not null).Select(f => f.Column!).Concat(p.Accounts.Select(a => a.Column)).ToArray();
        Check(inputs.Concat(p.FormulaColumns!).Distinct(StringComparer.OrdinalIgnoreCase).Count() == inputs.Length + p.FormulaColumns.Count);
        Check(p.BlankRowRule == SupportedBlankRule);
        Check(p.Sections!.All(s => s is not null));
        Check(p.Sections.Select(s => s.Id).Distinct().Count() == p.Sections.Count);
        Check(p.Sections.All(s => s is not null && Id(s.Id) && Label(s.Label) && s.InsertMode is "entireRow" or "columnsOnly"
            && s.Start is not null && s.End is not null && s.SubtotalRow is not null
            && (s.Start.HeaderRowOffset is >= 1 && s.Start.AfterRow is null && s.Start.Offset == 0
                || s.Start.HeaderRowOffset is null && s.Start.AfterRow is not null && s.Start.Offset >= 1)
            && s.End.RowBefore is not null && s.End.RowBefore.Column == s.SubtotalRow.Column
            && s.End.RowBefore.Text == s.SubtotalRow.Text && s.End.RowBefore.Occurrence == s.SubtotalRow.Occurrence));
        var markers = p.Sections.Select(s => s.SubtotalRow).Concat(p.Sections.Where(s => s.Start.AfterRow is not null).Select(s => s.Start.AfterRow!)).Concat(p.ProtectedRows!);
        Check(markers.All(m => m is not null && Label(m.Text) && m.Occurrence is >= 1 and <= 2000));
        Check(p.SheetMatch!.Anchors.All(a => a is not null && Label(a.Text)));
        try
        {
            foreach (var column in inputs.Concat(p.FormulaColumns).Concat(markers.Select(m => m.Column)).Concat(p.SheetMatch.Anchors.Select(a => a.Column)))
                _ = A1Formula.ColumnNumber(column);
        }
        catch (Exception e) when (e is FormatException or ArgumentException) { Check(false); }
    }

    public ExcelSectionDefinition Section(string id, string sheet)
    {
        var s = Sections.SingleOrDefault(s => s.Id == id) ?? throw WorkflowProfiles.Error("UNKNOWN_SECTION", "Choose a section from this profile.");
        return new(s.Id, s.Label, sheet, SheetMatch.HeaderRow,
            Fields.Where(f => f.Column is not null).ToDictionary(f => f.Key, f => f.Column!), FormulaColumns,
            s.SubtotalRow.Column, s.SubtotalRow.Text, s.InsertMode, SheetMatch: SheetMatch, Start: s.Start,
            SubtotalOccurrence: s.SubtotalRow.Occurrence, FillBlankRowsFirst: s.FillBlankRowsFirst,
            InputColumns: Fields.Where(f => f.Column is not null).Select(f => f.Column!).Concat(Accounts.Select(a => a.Column)).ToArray(), ProtectedRows: ProtectedRows);
    }
}
