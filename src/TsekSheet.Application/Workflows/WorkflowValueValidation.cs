using System.Globalization;
using System.Xml;

namespace TsekSheet.Application.Workflows;

internal sealed record ValidatedWorkflowValue(string Text, decimal? Number, DateOnly? Date);

// Shared by proposal-only planning and the live review projection. This helper
// establishes value validity only; it cannot establish human verification.
internal static class WorkflowValueValidation
{
    internal static Dictionary<string, ValidatedWorkflowValue> Validate(IReadOnlyList<WorkflowField> fields,
        IReadOnlyList<string> identifierFields, IReadOnlyDictionary<string, string> input)
    {
        if (input == null || input.Count > fields.Count || input.Keys.Any(k => !fields.Any(f => f.Key == k)))
            throw WorkflowProfiles.Error("INVALID_RECORD", "The record contains unsupported field mappings.");
        var values = new Dictionary<string, ValidatedWorkflowValue>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            var value = input.GetValueOrDefault(field.Key, "");
            if (value == null || value.Length > field.MaxLength) throw WorkflowProfiles.Error("INVALID_RECORD", $"Field '{field.Header}' exceeds its permitted length.");
            try { XmlConvert.VerifyXmlChars(value); } catch (XmlException) { throw WorkflowProfiles.Error("INVALID_RECORD", "A record contains invalid text characters."); }
            value = value.Trim();
            if (field.Required && value.Length == 0) throw WorkflowProfiles.Error("INVALID_RECORD", $"Field '{field.Header}' is required.");
            decimal? number = null;
            DateOnly? date = null;
            if (value.Length > 0 && field.Kind == "decimal")
            {
                if (!decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)
                    || parsed < 0 || parsed > 1000000000000m || (decimal.GetBits(parsed)[3] >> 16 & 0xFF) > 4)
                    throw WorkflowProfiles.Error("INVALID_RECORD", "Decimal fields require a nonnegative decimal-point number, at most four decimal places and at most one trillion.");
                number = parsed;
                value = parsed.ToString(CultureInfo.InvariantCulture);
            }
            if (value.Length > 0 && field.Kind == "date")
            {
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    throw WorkflowProfiles.Error("INVALID_RECORD", "Dates must use YYYY-MM-DD.");
                date = parsed;
            }
            values[field.Key] = new(value, number, date);
        }
        if (!identifierFields.Any(k => values[k].Text.Length > 0)) throw WorkflowProfiles.Error("INVALID_RECORD", "At least one configured record identifier is required.");
        return values;
    }
}
