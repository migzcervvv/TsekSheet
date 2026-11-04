using System.Globalization;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class TypedReviewRecordTests
{
    private static SourceEvidence Source => new(Guid.Parse("3fa438fe-ec41-41a3-83ba-dbfdf5a9d762"), new string('B', 64), 2, 3);
    private static Dictionary<string, string> Values(WorkflowProfile p) => new()
    {
        [p.EntityField] = "  Artificial organization  ",
        [p.IdentifierFields[0]] = "  ID-0007  ",
        [p.ContextFields[0]] = "+00125.5000",
        [p.ContextFields[1]] = "2024-02-29"
    };
    private static FieldReviewSession Review(WorkflowProfile p, IReadOnlyDictionary<string, string> values) =>
        new(p, values.Select(v => new DraftField(v.Key, v.Value, Source)).ToArray());
    private static void Verify(FieldReviewSession session)
    {
        foreach (var key in session.Snapshot.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
            session.Verify(session.Snapshot.Fields.Single(f => f.Key == key).Binding);
    }
    private static WorkflowPlan Plan(WorkflowProfile p, IReadOnlyDictionary<string, string> values)
    {
        var capture = WorkflowPlanningTests.Capture(p);
        var analysis = WorksheetSectionAnalyzer.Analyze(capture.Info, capture.Sheet, p, capture.Cells);
        return WorkflowPlanning.Plan(analysis, p, [new("artificial", values)]);
    }
    private static void Reject(FieldReviewSession session, string code, Action action)
    {
        var before = JsonSerializer.Serialize(session.Snapshot);
        var error = Assert.ThrowsExactly<FieldReviewException>(action);
        Assert.AreEqual(code, error.Code);
        Assert.AreEqual(before, JsonSerializer.Serialize(session.Snapshot));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void ProjectionRetainsLiteralTypedAndSourceValuesWithoutChangingReview(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        var values = Values(p);
        var textKey = p.Fields[1].Key;
        values[textKey] = "  =SUM(A1:A2) <script>literal</script>  ";
        var session = Review(p, values);
        Verify(session);
        var before = JsonSerializer.Serialize(session.Snapshot);
        var version = session.Snapshot.Version;
        var projected = session.Project(version);
        Assert.AreEqual(version, projected.Version);
        Assert.AreEqual(p.Id, projected.Version.ProfileId);
        Assert.AreEqual(WorkflowProfiles.Hash(p), projected.Version.ProfileHash);
        CollectionAssert.AreEqual(p.Fields.Select(f => f.Key).ToArray(), projected.Fields.Select(f => f.Key).ToArray());
        var planned = Plan(p, values).Records.Single();
        foreach (var field in projected.Fields)
        {
            Assert.AreEqual(values.GetValueOrDefault(field.Key, ""), field.LiteralText);
            Assert.AreEqual(planned.Fields.Single(f => f.Key == field.Key).Value, field.NormalizedText);
            Assert.AreEqual(p.Fields.Single(f => f.Key == field.Key).Kind, field.Kind);
            Assert.AreEqual(values.ContainsKey(field.Key) ? Source : null, field.Source);
            Assert.AreEqual(0, field.ValueRevision);
        }
        var number = projected.Fields.Single(f => f.Kind == "decimal");
        Assert.AreEqual(125.5000m, number.DecimalValue);
        Assert.IsNull(number.DateValue);
        Assert.AreEqual("125.5000", number.NormalizedText);
        var date = projected.Fields.Single(f => f.Kind == "date");
        Assert.AreEqual(new DateOnly(2024, 2, 29), date.DateValue);
        Assert.IsNull(date.DecimalValue);
        var text = projected.Fields.Single(f => f.Key == textKey);
        Assert.AreEqual("=SUM(A1:A2) <script>literal</script>", text.NormalizedText);
        Assert.IsNull(text.DecimalValue);
        Assert.IsNull(text.DateValue);
        Assert.IsFalse(Plan(p, values).CanApply);
        Assert.AreEqual(before, JsonSerializer.Serialize(session.Snapshot));
        Assert.AreEqual(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(session.Project(version)));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<TypedReviewField>)projected.Fields).Clear());
    }

    [TestMethod]
    [DataRow("decimal", "0", "0")]
    [DataRow("decimal", "-0", "0")]
    [DataRow("decimal", "+.5", "0.5")]
    [DataRow("decimal", " .0001 ", "0.0001")]
    [DataRow("decimal", "1000000000000", "1000000000000")]
    [DataRow("decimal", "000125.5000", "125.5000")]
    [DataRow("date", "2024-02-29", "2024-02-29")]
    [DataRow("date", "0001-01-01", "0001-01-01")]
    [DataRow("date", "9999-12-31", "9999-12-31")]
    public void AcceptedValueBoundariesMatchPlannerAcrossProfilesAndCultures(string kind, string input, string expected)
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
            foreach (var p in WorkflowProfiles.BuiltInProfiles)
            {
                var values = Values(p);
                var key = p.Fields.Single(f => f.Kind == kind).Key;
                values[key] = input;
                var session = Review(p, values);
                Verify(session);
                var field = session.Project(session.Snapshot.Version).Fields.Single(f => f.Key == key);
                Assert.AreEqual(input, field.LiteralText);
                Assert.AreEqual(expected, field.NormalizedText);
                Assert.AreEqual(expected, Plan(p, values).Records.Single().Fields.Single(f => f.Key == key).Value);
                if (kind == "decimal") Assert.AreEqual(decimal.Parse(expected, CultureInfo.InvariantCulture), field.DecimalValue);
                else Assert.AreEqual(DateOnly.ParseExact(expected, "yyyy-MM-dd", CultureInfo.InvariantCulture), field.DateValue);
            }
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [TestMethod]
    [DataRow("decimal", "-0.0001")]
    [DataRow("decimal", "1000000000000.0001")]
    [DataRow("decimal", "0.00001")]
    [DataRow("decimal", "1.00000")]
    [DataRow("decimal", "1,000")]
    [DataRow("decimal", "1,23")]
    [DataRow("decimal", "1e2")]
    [DataRow("decimal", "NaN")]
    [DataRow("decimal", "Infinity")]
    [DataRow("decimal", "=1+2")]
    [DataRow("date", "2023-02-29")]
    [DataRow("date", "2026-13-01")]
    [DataRow("date", "2026-11-31")]
    [DataRow("date", "2026-1-2")]
    [DataRow("date", "02/11/2026")]
    [DataRow("date", "tomorrow")]
    public void ExplicitlyVerifiedButInvalidTypesStillRejectWithoutChangingState(string kind, string input)
    {
        foreach (var p in WorkflowProfiles.BuiltInProfiles)
        {
            var values = Values(p);
            values[p.Fields.Single(f => f.Kind == kind).Key] = input;
            var session = Review(p, values);
            Verify(session);
            Assert.IsTrue(session.Validate(session.Snapshot.Version).IsValid);
            var before = JsonSerializer.Serialize(session.Snapshot);
            var error = Assert.ThrowsExactly<WorkbookException>(() => session.Project(session.Snapshot.Version));
            var plannerError = Assert.ThrowsExactly<WorkbookException>(() => Plan(p, values));
            Assert.AreEqual("INVALID_RECORD", error.Code);
            Assert.AreEqual(plannerError.Message, error.Message);
            Assert.IsFalse(error.Message.Contains(input, StringComparison.Ordinal));
            Assert.AreEqual(before, JsonSerializer.Serialize(session.Snapshot));
        }
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void VerificationAloneDoesNotSatisfyIdentifierConstraint(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        var values = Values(p);
        values[p.IdentifierFields[0]] = "  ";
        var session = Review(p, values);
        Verify(session);
        Assert.IsTrue(session.Validate(session.Snapshot.Version).IsValid);
        var before = JsonSerializer.Serialize(session.Snapshot);
        var error = Assert.ThrowsExactly<WorkbookException>(() => session.Project(session.Snapshot.Version));
        Assert.AreEqual("INVALID_RECORD", error.Code);
        Assert.AreEqual(Assert.ThrowsExactly<WorkbookException>(() => Plan(p, values)).Message, error.Message);
        Assert.AreEqual(before, JsonSerializer.Serialize(session.Snapshot));
    }

    [TestMethod]
    public void AbsentAndExplicitBlankOptionalTypedFieldsNeverBecomeDefaultValues()
    {
        var original = WorkflowProfiles.Get("synthetic-payables");
        var p = original with { Fields = original.Fields.Select(f => f.Kind == "decimal" ? f with { Required = false } : f).ToArray() };
        foreach (var supplied in new[] { false, true })
        {
            var values = Values(p);
            foreach (var field in p.Fields.Where(f => f.Kind != "text"))
            {
                if (supplied) values[field.Key] = "  ";
                else values.Remove(field.Key);
            }
            var session = Review(p, values);
            Verify(session);
            foreach (var field in session.Project(session.Snapshot.Version).Fields.Where(f => f.Kind != "text"))
            {
                Assert.AreEqual(supplied ? "  " : "", field.LiteralText);
                Assert.AreEqual("", field.NormalizedText);
                Assert.IsNull(field.DecimalValue);
                Assert.IsNull(field.DateValue);
                Assert.AreEqual(supplied ? Source : null, field.Source);
            }
        }
    }

    [TestMethod]
    public void ForgedObservationsMissingRequiredAndOptionalDraftsCannotProject()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var values = Values(p);
        var session = Review(p, values);
        var snapshot = session.Snapshot;
        var forged = snapshot with { Fields = snapshot.Fields.Select(f => f with { Status = FieldReviewStatus.Verified }).ToArray() };
        Reject(session, "REVIEW_INCOMPLETE", () => session.Project(forged.Version));
        Verify(session);
        var previousValidation = session.Validate(session.Snapshot.Version);
        var optional = session.Snapshot.Fields.Single(f => f.Key == "particulars");
        session.Correct(optional.Binding, "", Source);
        Reject(session, "STALE_RECORD_REVIEW", () => session.Project(previousValidation.Version));
        Reject(session, "REVIEW_INCOMPLETE", () => session.Project(session.Snapshot.Version));
        Verify(session);
        Assert.IsNotNull(session.Project(session.Snapshot.Version));

        values.Remove(p.EntityField);
        var missing = Review(p, values);
        Verify(missing);
        Reject(missing, "REVIEW_INCOMPLETE", () => missing.Project(missing.Snapshot.Version));
    }

    [TestMethod]
    [DataRow("null")]
    [DataRow("review")]
    [DataRow("revision")]
    [DataRow("profile-id")]
    [DataRow("profile-hash")]
    public void InvalidVersionsNeverProjectOrChangeReview(string change)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var session = Review(p, Values(p));
        Verify(session);
        var v = session.Snapshot.Version;
        var expected = change switch
        {
            "null" => null!,
            "review" => v with { ReviewId = Guid.NewGuid() },
            "revision" => v with { Revision = v.Revision + 1 },
            "profile-id" => v with { ProfileId = "synthetic-laboratory" },
            _ => v with { ProfileHash = new string('0', 64) }
        };
        Reject(session, "STALE_RECORD_REVIEW", () => session.Project(expected));
    }

    [TestMethod]
    public void CorrectionRetractionAndReverificationPreserveOldProjectionAndInvalidateCurrentAuthority()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var session = Review(p, Values(p));
        Verify(session);
        var old = session.Project(session.Snapshot.Version);
        var serialized = JsonSerializer.Serialize(old);
        var amount = session.Snapshot.Fields.Single(f => f.Key == "amount");
        var source = Source with { DocumentRevision = 3, PageNumber = 4 };
        session.Correct(amount.Binding, "999.25", source);
        Reject(session, "STALE_RECORD_REVIEW", () => session.Project(old.Version));
        Reject(session, "REVIEW_INCOMPLETE", () => session.Project(session.Snapshot.Version));
        Verify(session);
        var current = session.Project(session.Snapshot.Version).Fields.Single(f => f.Key == "amount");
        Assert.AreEqual(999.25m, current.DecimalValue);
        Assert.AreEqual(source, current.Source);
        Assert.AreEqual(amount.ValueRevision + 1, current.ValueRevision);
        session.Retract(session.Snapshot.Fields.Single(f => f.Key == "amount").Binding);
        Reject(session, "REVIEW_INCOMPLETE", () => session.Project(session.Snapshot.Version));
        Assert.AreEqual(serialized, JsonSerializer.Serialize(old));
    }

    [TestMethod]
    public void CallerProfileCollectionChangesCannotAlterProjectionRulesOrProvenance()
    {
        var original = WorkflowProfiles.Get("synthetic-payables");
        var fields = original.Fields.ToArray();
        var identifiers = original.IdentifierFields.ToArray();
        var p = original with { Fields = fields, IdentifierFields = identifiers };
        var hash = WorkflowProfiles.Hash(p);
        var session = Review(p, Values(p));
        fields[4] = fields[4] with { Kind = "text", MaxLength = 1 };
        identifiers[0] = "particulars";
        identifiers[1] = "vendor";
        Verify(session);
        var projected = session.Project(session.Snapshot.Version);
        Assert.AreEqual(hash, projected.Version.ProfileHash);
        Assert.AreEqual(125.5000m, projected.Fields.Single(f => f.Key == "amount").DecimalValue);
        foreach (var id in original.IdentifierFields)
            session.Correct(session.Snapshot.Fields.Single(f => f.Key == id).Binding, "", Source);
        Verify(session);
        Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() => session.Project(session.Snapshot.Version)).Code);
    }

    [TestMethod]
    public async Task ConcurrentCorrectionCannotMixOldVerificationWithNewValue()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        for (var iteration = 0; iteration < 20; iteration++)
        {
            var session = Review(p, Values(p));
            Verify(session);
            var version = session.Snapshot.Version;
            var binding = session.Snapshot.Fields.Single(f => f.Key == "amount").Binding;
            using var start = new ManualResetEventSlim();
            var project = Task.Run(() =>
            {
                start.Wait();
                try { return session.Project(version); }
                catch (FieldReviewException e) { Assert.AreEqual("STALE_RECORD_REVIEW", e.Code); return null; }
            });
            var correct = Task.Run(() => { start.Wait(); session.Correct(binding, "999.25", Source); });
            start.Set();
            await Task.WhenAll(project, correct);
            if (project.Result is { } result)
            {
                Assert.AreEqual(version, result.Version);
                Assert.AreEqual(125.5000m, result.Fields.Single(f => f.Key == "amount").DecimalValue);
                Assert.AreEqual(0, result.Fields.Single(f => f.Key == "amount").ValueRevision);
            }
            Assert.AreEqual(version.Revision + 1, session.Snapshot.Version.Revision);
            Reject(session, "REVIEW_INCOMPLETE", () => session.Project(session.Snapshot.Version));
        }
    }

    [TestMethod]
    [DataRow("unknown")]
    [DataRow("null-values")]
    [DataRow("null-value")]
    [DataRow("raw-length")]
    [DataRow("xml")]
    public void ExtractedPlannerValidationStillRejectsMalformedRawInputs(string kind)
    {
        foreach (var p in WorkflowProfiles.BuiltInProfiles)
        {
            var values = Values(p);
            switch (kind)
            {
                case "unknown": values["unknown"] = "artificial"; break;
                case "null-values": values = null!; break;
                case "null-value": values[p.EntityField] = null!; break;
                case "raw-length": values[p.EntityField] = new string(' ', 128) + "x"; break;
                case "xml": values[p.EntityField] = "artificial\0"; break;
            }
            Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() => Plan(p, values)).Code);
        }
    }
}
