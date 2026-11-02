using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class FieldReviewSessionTests
{
    private static SourceEvidence Source => new(Guid.Parse("761829f0-0644-4b99-94aa-6776000bdef1"), new string('A', 64), 0, 1);
    private static WorkflowProfile Payables => WorkflowProfiles.Get("synthetic-payables");
    private static DraftField[] Candidates(WorkflowProfile profile) =>
    [
        new(profile.EntityField, "Artificial supplier or partner", Source),
        new(profile.IdentifierFields[0], "ARTIFICIAL-1", Source),
        new(profile.ContextFields[0], "125.50", Source),
        new(profile.ContextFields[1], "2026-11-02", Source)
    ];
    private static FieldReviewSnapshot Field(FieldReviewSession session, string key) => session.Snapshot.Fields.Single(f => f.Key == key);
    private static RecordReviewValidation Validate(FieldReviewSession session) => session.Validate(session.Snapshot.Version);
    private static void VerifySupplied(FieldReviewSession session)
    {
        foreach (var key in session.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Key))
            session.Verify(Field(session, key).Binding);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void GenericRecordsRequireExplicitVerificationOfEverySuppliedValue(string profileId)
    {
        var profile = WorkflowProfiles.Get(profileId);
        var drafts = Candidates(profile);
        var session = new FieldReviewSession(profile, drafts);
        var initial = session.Snapshot;
        Assert.AreEqual(0, initial.Version.Revision);
        Assert.AreEqual(profile.Fields.Count, initial.Fields.Count);
        Assert.IsTrue(initial.Fields.All(f => f.Status == FieldReviewStatus.Draft && f.ValueRevision == 0));
        CollectionAssert.AreEqual(drafts.Select(d => d.Key).ToArray(), Validate(session).Issues.Select(i => i.FieldKey).ToArray());
        Assert.IsTrue(Validate(session).Issues.All(i => i.Code == "FIELD_UNVERIFIED"));

        foreach (var draft in drafts)
        {
            var before = Field(session, draft.Key);
            var snapshot = session.Verify(before.Binding);
            var verified = snapshot.Fields.Single(f => f.Key == draft.Key);
            Assert.AreEqual(FieldReviewStatus.Verified, verified.Status);
            Assert.AreEqual(draft.Value, verified.Value);
            Assert.AreEqual(draft.Source, verified.Source);
            Assert.AreEqual(0, verified.ValueRevision);
        }
        Assert.IsTrue(Validate(session).IsValid);
        Assert.AreEqual(drafts.Length, session.Snapshot.Version.Revision);
        Assert.IsTrue(initial.Fields.All(f => f.Status == FieldReviewStatus.Draft));
        Assert.IsTrue(session.Snapshot.Fields.Where(f => f.Source == null).All(f => f.Status == FieldReviewStatus.Draft));
    }

    [TestMethod]
    [DataRow("value")]
    [DataRow("same-text")]
    [DataRow("document")]
    [DataRow("hash")]
    [DataRow("source-revision")]
    [DataRow("page")]
    public void EveryCorrectionInvalidatesVerificationAndOldCommands(string change)
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        VerifySupplied(session);
        var previous = Field(session, "vendor");
        var validated = Validate(session);
        Assert.IsTrue(validated.IsValid);
        var source = change switch
        {
            "document" => Source with { DocumentId = Guid.NewGuid() },
            "hash" => Source with { DocumentSha256 = new string('B', 64) },
            "source-revision" => Source with { DocumentRevision = 1 },
            "page" => Source with { PageNumber = 2 },
            _ => Source
        };
        var value = change == "value" ? "Corrected artificial entity" : previous.Value;
        session.Correct(previous.Binding, value, source);
        var current = Field(session, "vendor");
        Assert.AreEqual(previous.ValueRevision + 1, current.ValueRevision);
        Assert.AreEqual(FieldReviewStatus.Corrected, current.Status);
        Assert.AreEqual(value, current.Value);
        Assert.AreEqual(source, current.Source);
        Assert.AreEqual(validated.Version.Revision + 1, session.Snapshot.Version.Revision);
        Assert.AreEqual("FIELD_UNVERIFIED", Validate(session).Issues.Single().Code);
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Validate(validated.Version));
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Verify(previous.Binding));
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Correct(previous.Binding, "Stale correction", Source));

        // Even rebasing the outer record version cannot reuse old field evidence.
        var rebased = previous.Binding with { Review = session.Snapshot.Version };
        RejectedUnchanged(session, "STALE_FIELD_REVIEW", () => session.Verify(rebased));
        session.Verify(current.Binding);
        Assert.IsTrue(Validate(session).IsValid);
        Assert.AreEqual(FieldReviewStatus.Verified, previous.Status);
    }

    [TestMethod]
    public void ReturningToAnEarlierValueDoesNotRestoreItsVerification()
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        VerifySupplied(session);
        var original = Field(session, "vendor");
        session.Correct(original.Binding, "Temporary correction", Source);
        session.Correct(Field(session, "vendor").Binding, original.Value, original.Source!);
        Assert.AreEqual(2, Field(session, "vendor").ValueRevision);
        Assert.IsFalse(Validate(session).IsValid);
        RejectedUnchanged(session, "STALE_FIELD_REVIEW", () => session.Verify(original.Binding with { Review = session.Snapshot.Version }));
    }

    [TestMethod]
    public void MissingRequiredFieldsRemainVisibleAndCanBeCorrectedThenVerified()
    {
        var session = new FieldReviewSession(Payables, []);
        CollectionAssert.AreEqual(new[] { "vendor", "amount" }, Validate(session).Issues.Select(i => i.FieldKey).ToArray());
        Assert.IsTrue(Validate(session).Issues.All(i => i.Code == "REQUIRED_FIELD_MISSING"));
        RejectedUnchanged(session, "SOURCE_REQUIRED", () => session.Verify(Field(session, "vendor").Binding));
        session.Correct(Field(session, "vendor").Binding, "   ", Source);
        RejectedUnchanged(session, "REQUIRED_FIELD_MISSING", () => session.Verify(Field(session, "vendor").Binding));
        Assert.AreEqual("REQUIRED_FIELD_MISSING", Validate(session).Issues.Single(i => i.FieldKey == "vendor").Code);

        session.Correct(Field(session, "vendor").Binding, "Artificial supplier", Source);
        session.Verify(Field(session, "vendor").Binding);
        session.Correct(Field(session, "amount").Binding, "0.00", Source);
        session.Verify(Field(session, "amount").Binding);
        Assert.IsTrue(Validate(session).IsValid);
        session.Correct(Field(session, "amount").Binding, "", Source);
        Assert.IsFalse(Validate(session).IsValid);
    }

    [TestMethod]
    public void SuppliedOptionalBlankAlsoNeedsVerification()
    {
        var session = new FieldReviewSession(Payables, [.. Candidates(Payables), new("particulars", "", Source)]);
        foreach (var key in Candidates(Payables).Select(d => d.Key)) session.Verify(Field(session, key).Binding);
        Assert.AreEqual("particulars", Validate(session).Issues.Single().FieldKey);
        session.Verify(Field(session, "particulars").Binding);
        Assert.IsTrue(Validate(session).IsValid);
    }

    [TestMethod]
    public void RetractionAndReplayRequireFreshExplicitVerification()
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        var draft = Field(session, "vendor");
        RejectedUnchanged(session, "FIELD_NOT_VERIFIED", () => session.Retract(draft.Binding));
        session.Verify(draft.Binding);
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Verify(draft.Binding));
        var verified = Field(session, "vendor");
        RejectedUnchanged(session, "FIELD_ALREADY_VERIFIED", () => session.Verify(verified.Binding));
        session.Retract(verified.Binding);
        Assert.AreEqual(verified.ValueRevision, Field(session, "vendor").ValueRevision);
        Assert.AreEqual(FieldReviewStatus.Corrected, Field(session, "vendor").Status);
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Verify(verified.Binding));
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Retract(verified.Binding));
        session.Verify(Field(session, "vendor").Binding);
    }

    [TestMethod]
    [DataRow("record")]
    [DataRow("revision")]
    [DataRow("profile-id")]
    [DataRow("profile-hash")]
    [DataRow("field-revision")]
    [DataRow("source")]
    [DataRow("field")]
    public void ForeignOrAlteredBindingsCannotVerifyOrCorrectCurrentState(string change)
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        var binding = Field(session, "vendor").Binding;
        var altered = change switch
        {
            "record" => binding with { Review = binding.Review with { ReviewId = Guid.NewGuid() } },
            "revision" => binding with { Review = binding.Review with { Revision = -1 } },
            "profile-id" => binding with { Review = binding.Review with { ProfileId = "synthetic-laboratory" } },
            "profile-hash" => binding with { Review = binding.Review with { ProfileHash = new string('B', 64) } },
            "field-revision" => binding with { ValueRevision = 1 },
            "source" => binding with { Source = Source with { PageNumber = 2 } },
            _ => binding with { Key = "unknown" }
        };
        var code = change is "field-revision" or "source" ? "STALE_FIELD_REVIEW"
            : change == "field" ? "UNKNOWN_REVIEW_FIELD" : "STALE_RECORD_REVIEW";
        RejectedUnchanged(session, code, () => session.Verify(altered));
        RejectedUnchanged(session, code, () => session.Correct(altered, "Altered command", Source));
        RejectedUnchanged(session, code, () => session.Retract(altered));
    }

    [TestMethod]
    public void SeparateRecordsAndMutableInputCollectionsRemainIndependent()
    {
        var fields = Payables.Fields.ToList();
        var profile = Payables with { Fields = fields };
        var candidates = Candidates(profile).ToList();
        var first = new FieldReviewSession(profile, candidates);
        var second = new FieldReviewSession(profile, candidates);
        var before = JsonSerializer.Serialize(first.Snapshot);
        candidates.Clear();
        fields[0] = fields[0] with { Required = false, MaxLength = 1 };
        Assert.AreEqual(before, JsonSerializer.Serialize(first.Snapshot));
        VerifySupplied(first);
        Assert.IsTrue(Validate(first).IsValid);
        Assert.IsFalse(Validate(second).IsValid);
        RejectedUnchanged(second, "STALE_RECORD_REVIEW", () => second.Verify(Field(first, "vendor").Binding));
        Assert.AreEqual(FieldReviewStatus.Draft, Field(second, "vendor").Status);
    }

    [TestMethod]
    public void ClientSnapshotChangesCannotForgeVerificationOrChangeExactText()
    {
        const string text = "  =SUM(A1:A2) <script>literal</script>  ";
        var session = new FieldReviewSession(Payables, [.. Candidates(Payables), new("particulars", text, Source)]);
        var snapshot = session.Snapshot;
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<FieldReviewSnapshot>)snapshot.Fields).Clear());
        var forged = snapshot with { Fields = snapshot.Fields.Select(f => f with { Status = FieldReviewStatus.Verified, Value = "forged" }).ToArray() };
        Assert.IsTrue(forged.Fields.All(f => f.Status == FieldReviewStatus.Verified));
        Assert.IsFalse(session.Validate(forged.Version).IsValid);
        Assert.AreEqual(text, Field(session, "particulars").Value);
        VerifySupplied(session);
        Assert.IsTrue(Validate(session).IsValid);
        Assert.AreEqual(text, Field(session, "particulars").Value);
    }

    [TestMethod]
    [DataRow("duplicate")]
    [DataRow("unknown")]
    [DataRow("too-many")]
    [DataRow("null-candidates")]
    [DataRow("null-candidate")]
    [DataRow("null-value")]
    [DataRow("oversized")]
    [DataRow("invalid-text")]
    public void InvalidDraftInputsAreRejectedWithoutEchoingValues(string kind)
    {
        const string secret = "SYNTHETIC-SENSITIVE-TEXT";
        var drafts = kind switch
        {
            "duplicate" => new DraftField[] { new("vendor", secret, Source), new("vendor", secret, Source) },
            "unknown" => [new("unknown", secret, Source)],
            "too-many" => Enumerable.Repeat(new DraftField("vendor", secret, Source), 17).ToArray(),
            "null-candidates" => null!,
            "null-candidate" => [null!],
            "null-value" => [new("vendor", null!, Source)],
            "oversized" => [new("vendor", secret + new string('x', 129), Source)],
            _ => [new("vendor", secret + '\0', Source)]
        };
        var error = Assert.ThrowsExactly<FieldReviewException>(() => new FieldReviewSession(Payables, drafts));
        Assert.IsFalse(error.Message.Contains(secret, StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("document")]
    [DataRow("hash-length")]
    [DataRow("hash-characters")]
    [DataRow("revision")]
    [DataRow("page")]
    [DataRow("null")]
    public void InvalidSourceIsRejectedOnIntakeAndCorrection(string kind)
    {
        var source = kind switch
        {
            "document" => Source with { DocumentId = Guid.Empty },
            "hash-length" => Source with { DocumentSha256 = "ABC" },
            "hash-characters" => Source with { DocumentSha256 = new string('Z', 64) },
            "revision" => Source with { DocumentRevision = -1 },
            "page" => Source with { PageNumber = 0 },
            _ => null!
        };
        var error = Assert.ThrowsExactly<FieldReviewException>(() => new FieldReviewSession(Payables, [new("vendor", "Artificial", source)]));
        Assert.AreEqual("INVALID_REVIEW_SOURCE", error.Code);
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        VerifySupplied(session);
        RejectedUnchanged(session, "INVALID_REVIEW_SOURCE", () => session.Correct(Field(session, "vendor").Binding, "Artificial", source));
    }

    [TestMethod]
    public void InvalidCorrectionOrMissingCommandLeavesVerifiedRecordUnchanged()
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        VerifySupplied(session);
        var binding = Field(session, "vendor").Binding;
        RejectedUnchanged(session, "INVALID_REVIEW_VALUE", () => session.Correct(binding, new string('X', 129), Source));
        RejectedUnchanged(session, "INVALID_REVIEW_VALUE", () => session.Correct(binding, "Invalid\0", Source));
        RejectedUnchanged(session, "INVALID_REVIEW_VALUE", () => session.Correct(binding, null!, Source));
        RejectedUnchanged(session, "INVALID_REVIEW_INPUT", () => session.Verify(null!));
        RejectedUnchanged(session, "STALE_RECORD_REVIEW", () => session.Validate(null!));
        Assert.IsTrue(Validate(session).IsValid);
    }

    [TestMethod]
    public async Task CompetingCommandsAtOneRevisionHaveOnlyOneWinner()
    {
        var session = new FieldReviewSession(Payables, Candidates(Payables));
        var binding = Field(session, "vendor").Binding;
        var results = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(() =>
        {
            try { session.Verify(binding); return "verified"; }
            catch (FieldReviewException e) { return e.Code; }
        })));
        CollectionAssert.AreEquivalent(new[] { "verified", "STALE_RECORD_REVIEW" }, results);
        Assert.AreEqual(1, session.Snapshot.Version.Revision);
        Assert.AreEqual(FieldReviewStatus.Verified, Field(session, "vendor").Status);
    }

    private static void RejectedUnchanged(FieldReviewSession session, string code, Action action)
    {
        var before = JsonSerializer.Serialize(session.Snapshot);
        var error = Assert.ThrowsExactly<FieldReviewException>(action);
        Assert.AreEqual(code, error.Code);
        Assert.AreEqual(before, JsonSerializer.Serialize(session.Snapshot));
    }
}
