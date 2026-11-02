namespace TsekSheet.Application.Verification;

public sealed record TypedReviewField(string Key, string Kind, string LiteralText, string NormalizedText,
    decimal? DecimalValue, DateOnly? DateValue, SourceEvidence? Source, int ValueRevision);

/// <summary>
/// A read-only observation at Version, never authorization for a future write.
/// Later mapping must recheck the live review and destination versions.
/// </summary>
public sealed class TypedReviewRecord
{
    internal TypedReviewRecord(ReviewVersion version, IEnumerable<TypedReviewField> fields)
    {
        Version = version;
        Fields = Array.AsReadOnly(fields.ToArray());
    }

    public ReviewVersion Version { get; }
    public IReadOnlyList<TypedReviewField> Fields { get; }
}
