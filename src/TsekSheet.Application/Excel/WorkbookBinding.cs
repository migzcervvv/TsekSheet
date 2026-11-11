using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Excel;

public sealed record BindingSubtotal(string Column, string Text, string? Anchor = null);
public sealed record WorkbookTarget(string Id, string Label, string Sheet, string Type, string? Reference,
    IReadOnlyDictionary<string, string> ColumnMap, IReadOnlyList<string> ExpectedFormulaColumns,
    SheetMatch? HeaderSignature, BindingSubtotal? Subtotal, IReadOnlyList<string> RequiredFields, string? StartAnchor = null);

public sealed record WorkbookBinding(int SchemaVersion, string Id, int Revision, DocumentTypeReference DocumentType,
    string FilenameHint, IReadOnlyList<string> RequiredSheets, string WorkbookIdentityName, IReadOnlyList<WorkbookTarget> Targets);

// Identity metadata for a future persisted draft. A restored draft needs a fresh Excel preview.
public sealed record BindingDraftIdentity(DocumentTypeReference DocumentType, string BindingId, int BindingRevision, string TargetId);

public static class WorkbookBindings
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = 12
    };

    public static WorkbookBinding Payables { get; } = CreatePayables();

    public static WorkbookBinding Parse(string json, DocumentType documentType)
    {
        Require(json is { Length: > 0 and <= 65536 }, "A binding must contain at most 64 KB of JSON.");
        try
        {
            using var parsed = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            CheckMembers(parsed.RootElement);
            Require(parsed.RootElement.ValueKind == JsonValueKind.Object && parsed.RootElement.TryGetProperty("schemaVersion", out var version)
                && version.ValueKind == JsonValueKind.Number && version.TryGetInt32(out _), "Specify a binding schemaVersion.");
            var binding = Migrate(parsed.RootElement);
            Validate(binding, documentType);
            return Freeze(binding);
        }
        catch (JsonException error)
        {
            throw new WorkbookException("INVALID_BINDING", $"Unsupported or malformed binding property at {error.Path ?? "$"}.");
        }
    }

    private static WorkbookBinding Migrate(JsonElement data) => data.GetProperty("schemaVersion").GetInt32() switch
    {
        1 => data.Deserialize<WorkbookBinding>(Json)!,
        _ => throw new WorkbookException("INVALID_BINDING", "Unsupported binding schemaVersion. This release accepts version 1.")
    };

    private static void CheckMembers(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) CheckMembers(item);
        if (value.ValueKind != JsonValueKind.Object) return;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            Require(names.Add(property.Name), $"Duplicate binding member '{property.Name}'.");
            CheckMembers(property.Value);
        }
    }

    public static void Validate(WorkbookBinding binding, DocumentType documentType)
    {
        Require(binding.SchemaVersion == 1, "Unsupported binding schemaVersion. This release accepts version 1.");
        Require(binding.DocumentType == documentType.Identity, "The binding must reference the installed document type and revision.");
        Require(Id(binding.Id) && binding.Revision > 0 && Label(binding.FilenameHint) && binding.FilenameHint.IndexOfAny(['/', '\\', ':']) < 0,
            "Supply a binding ID, positive revision and a filename hint without a path.");
        Require(binding.RequiredSheets is { Count: > 0 and <= 32 } && binding.RequiredSheets.All(s => Label(s) && s.Length <= 31)
            && Distinct(binding.RequiredSheets), "Supply distinct required Excel worksheet names.");
        Require(Reference(binding.WorkbookIdentityName), "Supply a valid workbook identity defined name.");
        Require(binding.Targets is { Count: > 0 and <= 64 } && binding.Targets.All(t => t is not null && Id(t.Id))
            && Distinct(binding.Targets.Select(t => t.Id)), "Supply unique target IDs.");
        var references = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { binding.WorkbookIdentityName };
        foreach (var target in binding.Targets)
        {
            void Check(bool condition, string reason) => Require(condition, $"Target '{target.Id}'. {reason}");
            Check(Label(target.Label) && binding.RequiredSheets.Contains(target.Sheet), "Supply a label and a required worksheet.");
            Check(target.Type is "table" or "namedRange" or "unsupported", "Choose table, namedRange or unsupported.");
            Require(target.ColumnMap is not null && target.RequiredFields is not null && target.ExpectedFormulaColumns is not null,
                $"Target '{target.Id}'. Supply a column map, requiredFields and expectedFormulaColumns.");
            foreach (var key in target.ColumnMap.Keys.Concat(target.RequiredFields))
                Check(documentType.Fields.Any(f => f.Key == key), $"Unknown document field '{key}'.");
            Check(Distinct(target.RequiredFields) && target.RequiredFields.All(target.ColumnMap.ContainsKey), "Each required field needs one column mapping.");
            if (target.Type == "unsupported")
            {
                Check(target.Reference is null && target.StartAnchor is null && target.Subtotal is null && target.HeaderSignature is null
                    && target.ColumnMap.Count == 0 && target.ExpectedFormulaColumns.Count == 0, "An unsupported destination cannot specify writable ranges or columns.");
                continue;
            }
            Check(Reference(target.Reference) && references.Add(target.Reference!), "Supply a unique Excel table or defined name.");
            Check(target.Subtotal is not null && Column(target.Subtotal.Column) && Label(target.Subtotal.Text), "Supply a literal subtotal label and column.");
            Check(target.Type == "namedRange"
                ? Reference(target.StartAnchor) && references.Add(target.StartAnchor!) && Reference(target.Subtotal!.Anchor) && references.Add(target.Subtotal.Anchor!)
                : target.StartAnchor is null && target.Subtotal!.Anchor is null, "A named range needs distinct start and subtotal anchor names; a Table uses its own boundaries.");
            var columns = target.ColumnMap.Values.Concat(target.ExpectedFormulaColumns).ToArray();
            Check(target.ColumnMap.Count > 0 && columns.All(Column) && Distinct(columns), "Mapped and expected formula columns must be valid and distinct.");
            Check(target.HeaderSignature is { HeaderRow: >= 1 and < 1048576, Anchors.Count: > 0 and <= 64 }
                && target.HeaderSignature.Anchors.All(a => a is not null && Column(a.Column) && Label(a.Text))
                && Distinct(target.HeaderSignature.Anchors.Select(a => a.Column))
                && columns.All(c => target.HeaderSignature.Anchors.Any(a => a.Column == c)), "Supply a literal header signature covering every mapped and formula column.");
        }
    }

    internal static string Configuration(WorkbookBinding binding) => JsonSerializer.Serialize(binding, Json);

    private static bool Id(string? text) => text is { Length: > 0 and <= 48 } && text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool Label(string? text) => text is { Length: > 0 and <= 128 } && !string.IsNullOrWhiteSpace(text) && !text.Any(char.IsControl);
    private static bool Reference(string? text) => text is { Length: > 0 and <= 128 }
        && Regex.IsMatch(text, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant)
        && !Regex.IsMatch(text, @"^(?:[A-Z]{1,3}[0-9]+|R[0-9]*C[0-9]*|R|C)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static bool Column(string? text)
    {
        if (text is not { Length: > 0 and <= 3 } || text.Any(c => c is < 'A' or > 'Z')) return false;
        try { _ = A1Formula.ColumnNumber(text); return true; }
        catch (FormatException) { return false; }
    }
    private static bool Distinct(IEnumerable<string> values) => values.Count() == values.Distinct(StringComparer.OrdinalIgnoreCase).Count();
    private static void Require([DoesNotReturnIf(false)] bool valid, string message)
    {
        if (!valid) throw new WorkbookException("INVALID_BINDING", message);
    }

    private static WorkbookBinding Freeze(WorkbookBinding binding) => binding with
    {
        RequiredSheets = Array.AsReadOnly(binding.RequiredSheets.ToArray()),
        Targets = Array.AsReadOnly(binding.Targets.Select(t => t with
        {
            ColumnMap = new ReadOnlyDictionary<string, string>(t.ColumnMap.ToDictionary()),
            RequiredFields = Array.AsReadOnly(t.RequiredFields.ToArray()),
            ExpectedFormulaColumns = Array.AsReadOnly(t.ExpectedFormulaColumns.ToArray()),
            HeaderSignature = t.HeaderSignature is { } header ? header with { Anchors = Array.AsReadOnly(header.Anchors.ToArray()) } : null
        }).ToArray())
    };

    private static WorkbookBinding CreatePayables()
    {
        const string sheet = "Unpaid RFPs - SEP";
        var columnMap = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["dateReceived"] = "B", ["payee"] = "C", ["particulars"] = "D", ["rfpNo"] = "E", ["invoice"] = "F",
            ["amount"] = "G", ["otherCharges"] = "H", ["vat"] = "I", ["totalAmount"] = "J", ["ewt"] = "K",
            ["overpayment"] = "L", ["netAmount"] = "M", ["modeOfPayment"] = "N", ["cvNo"] = "O"
        };
        var header = new SheetMatch(3, [new("B", "Date Received"), new("C", "PAYEE (VENDOR NAME)"), new("D", "Particulars"),
            new("E", "RFP No."), new("F", "Invoice #"), new("G", "Amount"), new("H", "Other Charges"), new("I", "VAT"),
            new("J", "Total Amount"), new("K", "EWT"), new("L", "Overpayment"), new("M", "Net Amount"),
            new("N", "Mode of Payment"), new("O", "CV#")]);
        WorkbookTarget Target(string id, string label)
        {
            var name = "TsekSheet_Payables_" + id.Replace('-', '_');
            return new(id, label, sheet, "namedRange", name, columnMap, [], header,
                new("F", "Sub-Total", name + "_subtotal"), ["payee", "rfpNo", "amount"], name + "_start");
        }
        var targets = new[] { Target("employee", "Employee"), Target("cash-advance", "Cash Advance"), Target("dollars", "Dollars"),
            Target("last-pay", "Last Pay"), Target("refund", "Refund") }
            .Concat(Enumerable.Range(1, 14).Select(i => Target($"member-company-{i}", $"Member Company {i:00}")))
            .Concat([Target("insurance", "Insurance"), Target("mobile-subscription", "Mobile Subscription"), Target("external", "External")]).ToArray();
        var binding = new WorkbookBinding(1, "payables-rfp", 1, new("rfp", 1), "Payables.xlsx", [sheet], "TsekSheet_Payables_Identity", targets);
        Validate(binding, DocumentTypes.Rfp);
        return Freeze(binding);
    }
}
