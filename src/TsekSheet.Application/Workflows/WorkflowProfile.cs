using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Workflows;

public sealed record WorkflowField(string Key, string Header, string Kind, bool Required, int MaxLength);
public sealed record WorkflowSectionRule(string Id, string Label, string Heading, string Subtotal);
public sealed record WorkflowAlias(string Alias, string Canonical);
public sealed record WorkflowProfileSummary(string Id, int Revision, string DisplayName, string ProfileHash, IReadOnlyList<WorkflowField> Fields);
public sealed record WorkflowProfile(int SchemaVersion, string Id, int Revision, string DisplayName,
    string WorksheetRule, int MaxRows, int MaxColumns, string EntityField, IReadOnlyList<string> IdentifierFields,
    IReadOnlyList<string> ContextFields, IReadOnlyList<WorkflowField> Fields, IReadOnlyList<WorkflowSectionRule> Sections,
    IReadOnlyList<WorkflowAlias> Aliases, bool FoldCase, bool CollapseWhitespace, string FallbackSectionId,
    string ExistingDestination, string MissingDestination, string DuplicatePolicy, string InsertionPolicy,
    string StylePolicy, bool RequireReview, bool RequireDuplicateReview);

public static class WorkflowProfiles
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 12 };
    private static readonly Lazy<IReadOnlyList<WorkflowProfile>> BuiltIns = new(() => Array.AsReadOnly(
        typeof(WorkflowProfiles).Assembly.GetManifestResourceNames().Where(n => n.EndsWith(".workflow.json", StringComparison.Ordinal)).Order()
        .Select(n => { using var stream = typeof(WorkflowProfiles).Assembly.GetManifestResourceStream(n)!; using var reader = new StreamReader(stream); return Parse(reader.ReadToEnd()); }).ToArray()));
    public static IReadOnlyList<WorkflowProfile> All => BuiltIns.Value;
    public static IReadOnlyList<WorkflowProfileSummary> Catalog => Array.AsReadOnly(All.Select(p => new WorkflowProfileSummary(p.Id, p.Revision, p.DisplayName, Hash(p), p.Fields)).ToArray());
    internal static WorkflowPlanRequest ParseRequest(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        try { CheckDuplicates(document.RootElement); }
        catch (WorkbookException) { throw Error("INVALID_REQUEST", "Duplicate planning request members are forbidden."); }
        return JsonSerializer.Deserialize<WorkflowPlanRequest>(json, Json) ?? throw Error("INVALID_REQUEST", "Planning request is empty.");
    }
    public static WorkflowProfile Get(string id) => All.SingleOrDefault(p => p.Id == id) ?? throw Error("UNKNOWN_PROFILE", "Choose an approved built-in workflow profile.");
    public static string Hash(WorkflowProfile profile) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(profile, Json))));
    public static string Normalize(string value, WorkflowProfile profile)
    {
        var text = value.Normalize(NormalizationForm.FormC).Trim();
        if (profile.CollapseWhitespace) text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return profile.FoldCase ? text.ToUpperInvariant() : text;
    }
    public static string Entity(string value, WorkflowProfile profile)
    {
        var normalized = Normalize(value, profile);
        var alias = profile.Aliases.SingleOrDefault(a => Normalize(a.Alias, profile) == normalized);
        return alias == null ? normalized : Normalize(alias.Canonical, profile);
    }
    public static WorkflowProfile Parse(string json)
    {
        if (json == null || json.Length > 65536) throw Error("INVALID_PROFILE", "Profile JSON exceeds its supported size.");
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 12 });
            CheckDuplicates(doc.RootElement);
            var p = JsonSerializer.Deserialize<WorkflowProfile>(json, Json) ?? throw Error("INVALID_PROFILE", "Profile is empty.");
            Validate(p);
            return p with { Fields = Array.AsReadOnly(p.Fields.ToArray()), Sections = Array.AsReadOnly(p.Sections.ToArray()),
                Aliases = Array.AsReadOnly(p.Aliases.ToArray()), IdentifierFields = Array.AsReadOnly(p.IdentifierFields.ToArray()), ContextFields = Array.AsReadOnly(p.ContextFields.ToArray()) };
        }
        catch (JsonException) { throw Error("INVALID_PROFILE", "Profile JSON has unsupported, missing or malformed members."); }
    }
    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject()) { if (!names.Add(property.Name)) throw Error("INVALID_PROFILE", "Duplicate profile JSON members are forbidden."); CheckDuplicates(property.Value); }
        }
        else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) CheckDuplicates(item);
    }
    public static void Validate(WorkflowProfile p)
    {
        bool Label(string? s) => !string.IsNullOrWhiteSpace(s) && s.Length <= 128 && !s.Any(char.IsControl);
        bool Id(string? s) => s is { Length: > 0 and <= 48 } && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
        void Check(bool condition) { if (!condition) throw Error("INVALID_PROFILE", "Profile configuration is invalid or outside the supported schema/policies."); }
        Check(p != null && p.SchemaVersion == 1 && p.Revision > 0 && Id(p.Id) && Label(p.DisplayName));
        Check(p!.WorksheetRule == "selected-with-unique-headers" && p.MaxRows is >= 1 and <= 2000 && p.MaxColumns is >= 1 and <= 32 && p.MaxRows * p.MaxColumns <= 40000);
        Check(p.Fields is { Count: >= 3 and <= 16 } && p.Sections is { Count: >= 1 and <= 16 } && p.Aliases is { Count: <= 64 });
        Check(p.Fields!.All(f => f != null && Id(f.Key) && Label(f.Header) && f.Kind is "text" or "decimal" or "date" && f.MaxLength is >= 1 and <= 256));
        Check(p.Fields.Select(f => f.Key).Distinct().Count() == p.Fields.Count && p.Fields.Select(f => Normalize(f.Header, p)).Distinct().Count() == p.Fields.Count);
        Check(p.Fields.Any(f => f.Key == p.EntityField && f.Kind == "text" && f.Required));
        Check(p.IdentifierFields is { Count: >= 1 and <= 4 } && p.ContextFields is { Count: <= 8 });
        Check(p.IdentifierFields!.Distinct().Count() == p.IdentifierFields.Count && p.ContextFields!.Distinct().Count() == p.ContextFields.Count);
        Check(p.IdentifierFields.All(k => p.Fields.Any(f => f.Key == k && f.Kind == "text") && k != p.EntityField)
            && p.ContextFields.All(k => p.Fields.Any(f => f.Key == k)));
        Check(p.Sections!.All(s => s != null && Id(s.Id) && Label(s.Label) && Label(s.Heading) && Label(s.Subtotal)));
        Check(p.Sections.Select(s => s.Id).Distinct().Count() == p.Sections.Count && p.Sections.Select(s => Normalize(s.Heading, p)).Distinct().Count() == p.Sections.Count);
        Check(!p.Sections.Select(s => Normalize(s.Heading, p)).Intersect(p.Sections.Select(s => Normalize(s.Subtotal, p))).Any());
        Check(p.Sections.Any(s => s.Id == p.FallbackSectionId));
        Check(p.Aliases!.All(a => a != null && Label(a.Alias) && Label(a.Canonical)));
        Check(p.Aliases.Select(a => Normalize(a.Alias, p)).Distinct().Count() == p.Aliases.Count);
        Check(!p.Aliases.Select(a => Normalize(a.Alias, p)).Intersect(p.Aliases.Select(a => Normalize(a.Canonical, p))).Any());
        Check(p.ExistingDestination == "after-last-match" && p.MissingDestination == "before-subtotal" && p.DuplicatePolicy == "entity-and-any-identifier"
            && p.InsertionPolicy == "disabled" && p.StylePolicy == "explicit-template-required" && p.RequireReview && p.RequireDuplicateReview);
    }
    internal static WorkbookException Error(string code, string message) => new(code, message);
}
