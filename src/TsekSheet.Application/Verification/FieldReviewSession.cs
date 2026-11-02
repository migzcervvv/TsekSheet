using System.Xml;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Verification;

// These are local evidence identities, never paths or authority to open a file.
public sealed record SourceEvidence(Guid DocumentId, string DocumentSha256, int DocumentRevision, int PageNumber);
public sealed record DraftField(string Key, string Value, SourceEvidence Source);
public enum FieldReviewStatus { Draft, Corrected, Verified }
public sealed record ReviewVersion(Guid ReviewId, int Revision, string ProfileId, string ProfileHash);
public sealed record FieldReviewBinding(ReviewVersion Review, string Key, int ValueRevision, SourceEvidence? Source);
public sealed record FieldReviewSnapshot(string Key, string Value, SourceEvidence? Source,
    int ValueRevision, FieldReviewStatus Status, FieldReviewBinding Binding);
public sealed record RecordReviewSnapshot(ReviewVersion Version, IReadOnlyList<FieldReviewSnapshot> Fields);
public sealed record FieldReviewIssue(string FieldKey, string Code, string Message);
public sealed record RecordReviewValidation(ReviewVersion Version, IReadOnlyList<FieldReviewIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed class FieldReviewException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// Owns one record's review in memory. Snapshots and validation results are
/// observations, not mapping authorization. Future mapping must validate the
/// live session/version, independently of client-supplied review flags.
/// </summary>
public sealed class FieldReviewSession
{
    private sealed record FieldState(string Value, SourceEvidence? Source, int ValueRevision, FieldReviewStatus Status);

    private readonly object _gate = new();
    private readonly Guid _id = Guid.NewGuid();
    private readonly string _profileId;
    private readonly string _profileHash;
    private readonly WorkflowField[] _fields;
    private readonly string[] _identifierFields;
    private readonly Dictionary<string, FieldState> _states = new(StringComparer.Ordinal);
    private int _revision;

    public FieldReviewSession(WorkflowProfile profile, IReadOnlyList<DraftField> candidates)
    {
        WorkflowProfiles.Validate(profile);
        _profileId = profile.Id;
        _profileHash = WorkflowProfiles.Hash(profile);
        _fields = profile.Fields.ToArray();
        _identifierFields = profile.IdentifierFields.ToArray();
        if (candidates == null || candidates.Count > _fields.Length)
            throw Error("INVALID_REVIEW_INPUT", "Supply at most one draft for each configured field.");

        foreach (var candidate in candidates)
        {
            if (candidate == null) throw Error("INVALID_REVIEW_INPUT", "A draft field is missing.");
            var field = Definition(candidate.Key);
            CheckValue(field, candidate.Value);
            CheckSource(candidate.Source);
            if (!_states.TryAdd(field.Key, new(candidate.Value, candidate.Source, 0, FieldReviewStatus.Draft)))
                throw Error("INVALID_REVIEW_INPUT", "Duplicate draft field keys are forbidden.");
        }
        foreach (var field in _fields)
            _states.TryAdd(field.Key, new("", null, 0, FieldReviewStatus.Draft));
    }

    // Private staging for the source-linked boundary. Immutable field states
    // may be shared; dictionaries and configuration arrays are independent.
    // This is not a new public way to supply verification or replace a review.
    private FieldReviewSession(FieldReviewSession source)
    {
        _id = source._id;
        _profileId = source._profileId;
        _profileHash = source._profileHash;
        _fields = source._fields.ToArray();
        _identifierFields = source._identifierFields.ToArray();
        _revision = source._revision;
        foreach (var state in source._states) _states.Add(state.Key, state.Value);
    }

    internal FieldReviewSession Fork()
    {
        lock (_gate) return new(this);
    }

    public RecordReviewSnapshot Snapshot
    {
        get { lock (_gate) return Capture(); }
    }

    public RecordReviewSnapshot Correct(FieldReviewBinding expected, string value, SourceEvidence source)
    {
        lock (_gate)
        {
            var (field, current) = RequireField(expected);
            CheckValue(field, value);
            CheckSource(source);
            var nextRevision = checked(_revision + 1);
            var next = new FieldState(value, source, checked(current.ValueRevision + 1), FieldReviewStatus.Corrected);
            _states[field.Key] = next;
            _revision = nextRevision;
            return Capture();
        }
    }

    // Calling this method represents explicit human intent at the application
    // boundary. Extraction/confidence must never invoke it automatically.
    public RecordReviewSnapshot Verify(FieldReviewBinding expected)
    {
        lock (_gate)
        {
            var (field, current) = RequireField(expected);
            if (current.Source == null) throw Error("SOURCE_REQUIRED", "Review a source before verifying this field.");
            if (field.Required && string.IsNullOrWhiteSpace(current.Value))
                throw Error("REQUIRED_FIELD_MISSING", "A required field needs a value before verification.");
            if (current.Status == FieldReviewStatus.Verified)
                throw Error("FIELD_ALREADY_VERIFIED", "This field is already verified at its current value and source.");
            var nextRevision = checked(_revision + 1);
            _states[field.Key] = current with { Status = FieldReviewStatus.Verified };
            _revision = nextRevision;
            return Capture();
        }
    }

    public RecordReviewSnapshot Retract(FieldReviewBinding expected)
    {
        lock (_gate)
        {
            var (field, current) = RequireField(expected);
            if (current.Status != FieldReviewStatus.Verified)
                throw Error("FIELD_NOT_VERIFIED", "Only current verification can be retracted.");
            var nextRevision = checked(_revision + 1);
            _states[field.Key] = current with { Status = FieldReviewStatus.Corrected };
            _revision = nextRevision;
            return Capture();
        }
    }

    public RecordReviewValidation Validate(ReviewVersion expected)
    {
        lock (_gate)
        {
            RequireVersion(expected);
            return ValidateCurrent();
        }
    }

    public TypedReviewRecord Project(ReviewVersion expected)
    {
        lock (_gate)
        {
            RequireVersion(expected);
            if (!ValidateCurrent().IsValid)
                throw Error("REVIEW_INCOMPLETE", "Supply required fields and explicitly verify every supplied field before projection.");
            var values = WorkflowValueValidation.Validate(_fields, _identifierFields,
                _states.ToDictionary(pair => pair.Key, pair => pair.Value.Value, StringComparer.Ordinal));
            return new(Version, _fields.Select(field =>
            {
                var state = _states[field.Key];
                var value = values[field.Key];
                return new TypedReviewField(field.Key, field.Kind, state.Value, value.Text, value.Number, value.Date,
                    state.Source, state.ValueRevision);
            }));
        }
    }

    private RecordReviewValidation ValidateCurrent()
    {
        var issues = new List<FieldReviewIssue>();
        foreach (var field in _fields)
        {
            var state = _states[field.Key];
            if (field.Required && string.IsNullOrWhiteSpace(state.Value))
                issues.Add(new(field.Key, "REQUIRED_FIELD_MISSING", "Supply and verify the required field."));
            // An absent optional field contributes no data. Every supplied
            // field, even an optional blank, requires explicit verification.
            else if (state.Source != null && state.Status != FieldReviewStatus.Verified)
                issues.Add(new(field.Key, "FIELD_UNVERIFIED", "Explicitly verify the current field value and source."));
        }
        return new(Version, issues.AsReadOnly());
    }

    private ReviewVersion Version => new(_id, _revision, _profileId, _profileHash);

    private RecordReviewSnapshot Capture()
    {
        var version = Version;
        return new(version, Array.AsReadOnly(_fields.Select(field =>
        {
            var state = _states[field.Key];
            return new FieldReviewSnapshot(field.Key, state.Value, state.Source, state.ValueRevision, state.Status,
                new(version, field.Key, state.ValueRevision, state.Source));
        }).ToArray()));
    }

    private (WorkflowField Field, FieldState State) RequireField(FieldReviewBinding expected)
    {
        if (expected == null) throw Error("INVALID_REVIEW_INPUT", "A field review binding is required.");
        RequireVersion(expected.Review);
        var field = Definition(expected.Key);
        var current = _states[field.Key];
        if (expected.ValueRevision != current.ValueRevision || expected.Source != current.Source)
            throw Error("STALE_FIELD_REVIEW", "Refresh the field value and source before reviewing it.");
        return (field, current);
    }

    private void RequireVersion(ReviewVersion expected)
    {
        if (expected == null || expected != Version)
            throw Error("STALE_RECORD_REVIEW", "Refresh this record and profile before continuing review.");
    }

    private WorkflowField Definition(string key) =>
        _fields.SingleOrDefault(field => field.Key == key)
        ?? throw Error("UNKNOWN_REVIEW_FIELD", "Choose a configured field for this review.");

    private static void CheckValue(WorkflowField field, string value)
    {
        if (value == null || value.Length > field.MaxLength)
            throw Error("INVALID_REVIEW_VALUE", "A field value exceeds its configured length or is missing.");
        try { XmlConvert.VerifyXmlChars(value); }
        catch (XmlException) { throw Error("INVALID_REVIEW_VALUE", "A field contains unsupported text characters."); }
    }

    private static void CheckSource(SourceEvidence source)
    {
        if (source == null || source.DocumentId == Guid.Empty || source.DocumentRevision < 0 || source.PageNumber < 1
            || source.DocumentSha256 is not { Length: 64 } || !source.DocumentSha256.All(char.IsAsciiHexDigit))
            throw Error("INVALID_REVIEW_SOURCE", "Supply a local document identity, SHA-256, revision and one-based page.");
    }

    private static FieldReviewException Error(string code, string message) => new(code, message);
}
