using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedMappingPreviewTests
{
    private static SourceEvidence Source => new(Guid.Parse("f42c651e-c873-4153-9ac3-6697415bdcbb"), new string('B', 64), 2, 3);
    private static Dictionary<string, string> Values(WorkflowProfile p) => new()
    {
        [p.EntityField] = "  Artificial organization  ",
        [p.Fields[1].Key] = "  =SUM(A1:A2)  ",
        [p.IdentifierFields[0]] = "  NEW-ID  ",
        [p.ContextFields[0]] = "+00125.5000",
        [p.ContextFields[1]] = "2024-02-29"
    };
    private static FieldReviewSession Review(WorkflowProfile p, bool verified = true, Dictionary<string, string>? values = null)
    {
        var review = new FieldReviewSession(p, (values ?? Values(p)).Select(v => new DraftField(v.Key, v.Value, Source)).ToArray());
        if (verified) Verify(review);
        return review;
    }
    private static void Verify(FieldReviewSession review)
    {
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
    }
    private static MappingDestination Destination(Session session, WorkflowProfile p, int sheet = 0) =>
        new(session.Current.SessionId, session.Current.Worksheets[sheet].Id, session.Current.Revision,
            session.Current.SourceSha256, p.Id, WorkflowProfiles.Hash(p));

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void LiveVerifiedValuesMapIndependentlyToSelectedColumnsAndSectionBounds(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var review = Review(p);
        var before = JsonSerializer.Serialize(review.Snapshot);
        var info = session.Info;
        var first = VerifiedMapping.Preview(session, Destination(session, p), review, review.Snapshot.Version);
        var second = VerifiedMapping.Preview(session, Destination(session, p, 1), review, review.Snapshot.Version);
        Assert.AreEqual("Artificial first", first.WorksheetName);
        Assert.AreEqual("Artificial shifted", second.WorksheetName);
        Assert.AreEqual(review.Snapshot.Version, first.Review);
        Assert.AreEqual(first.Review, second.Review);
        Assert.AreNotEqual(first.Destination.WorksheetId, second.Destination.WorksheetId);
        Assert.AreEqual(22, first.Sections[1].SubtotalRow);
        Assert.AreEqual(42, second.Sections[1].SubtotalRow);
        for (var index = 0; index < p.Fields.Count; index++)
        {
            var field = first.Fields[index];
            var other = second.Fields[index];
            Assert.AreEqual(index + 1, field.Column);
            Assert.AreEqual(p.MaxColumns - index, other.Column);
            Assert.AreEqual(p.Fields[index].Header, field.Header);
            Assert.AreEqual(p.Fields[index].Key, field.Value.Key);
            Assert.AreEqual(field.Value, other.Value);
            Assert.AreEqual(Values(p).GetValueOrDefault(field.Value.Key, ""), field.Value.LiteralText);
            Assert.AreEqual(Values(p).ContainsKey(field.Value.Key) ? Source : null, field.Value.Source);
        }
        var literal = first.Fields[1].Value;
        Assert.AreEqual("  =SUM(A1:A2)  ", literal.LiteralText);
        Assert.AreEqual("=SUM(A1:A2)", literal.NormalizedText);
        Assert.AreEqual("text", literal.Kind);
        Assert.AreEqual(125.5000m, first.Fields.Single(f => f.Value.Kind == "decimal").Value.DecimalValue);
        Assert.AreEqual(new DateOnly(2024, 2, 29), first.Fields.Single(f => f.Value.Kind == "date").Value.DateValue);
        Assert.IsNull(first.Fields.Single(f => f.Value.Key == p.IdentifierFields[1]).Value.Source);
        Assert.IsFalse(first.CanApply);
        Assert.IsFalse(second.CanApply);
        Assert.AreEqual(before, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(info, session.Info);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappedField>)first.Fields).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MappingSection>)first.Sections).Clear());
        Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(
            VerifiedMapping.Preview(session, first.Destination, review, first.Review)));
    }

    [TestMethod]
    [DataRow("session", "INVALID_MAPPING_DESTINATION")]
    [DataRow("source", "INVALID_MAPPING_DESTINATION")]
    [DataRow("worksheet", "UNKNOWN_WORKSHEET")]
    [DataRow("revision", "STALE_REVISION")]
    [DataRow("hash", "STALE_PROFILE")]
    [DataRow("profile", "UNKNOWN_PROFILE")]
    [DataRow("other-profile", "STALE_PROFILE")]
    [DataRow("empty-session", "INVALID_MAPPING_REQUEST")]
    [DataRow("empty-worksheet", "INVALID_MAPPING_REQUEST")]
    [DataRow("negative-revision", "INVALID_MAPPING_REQUEST")]
    [DataRow("null", "INVALID_MAPPING_REQUEST")]
    public void InvalidDestinationRejectsWithoutReadingOrChangingState(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var target = Destination(session, p);
        var other = WorkflowProfiles.Get("synthetic-laboratory");
        target = scenario switch
        {
            "session" => target with { SessionId = Guid.NewGuid() },
            "source" => target with { SourceSha256 = new string('C', 64) },
            "worksheet" => target with { WorksheetId = Guid.NewGuid() },
            "revision" => target with { Revision = 1 },
            "hash" => target with { ProfileHash = "stale" },
            "profile" => target with { ProfileId = "unknown" },
            "other-profile" => target with { ProfileId = other.Id, ProfileHash = WorkflowProfiles.Hash(other) },
            "empty-session" => target with { SessionId = Guid.Empty },
            "empty-worksheet" => target with { WorksheetId = Guid.Empty },
            "negative-revision" => target with { Revision = -1 },
            _ => null!
        };
        var before = JsonSerializer.Serialize(review.Snapshot);
        var info = session.Current;
        Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedMapping.Preview(session, target, review, review.Snapshot.Version)).Code);
        Assert.AreEqual(0, session.Reads);
        Assert.AreEqual(info, session.Current);
        Assert.AreEqual(before, JsonSerializer.Serialize(review.Snapshot));
    }

    [TestMethod]
    [DataRow("draft", "REVIEW_INCOMPLETE")]
    [DataRow("corrected", "REVIEW_INCOMPLETE")]
    [DataRow("retracted", "REVIEW_INCOMPLETE")]
    [DataRow("foreign", "STALE_RECORD_REVIEW")]
    [DataRow("stale", "STALE_RECORD_REVIEW")]
    [DataRow("forged-profile", "STALE_RECORD_REVIEW")]
    [DataRow("null", "STALE_RECORD_REVIEW")]
    [DataRow("missing-required", "REVIEW_INCOMPLETE")]
    public void LiveReviewRejectsUnverifiedStaleOrForgedObservations(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var values = Values(p);
        if (scenario == "missing-required") values.Remove(p.ContextFields[0]);
        var review = Review(p, scenario != "draft", values);
        var expected = review.Snapshot.Version;
        var field = review.Snapshot.Fields[0];
        if (scenario is "corrected" or "stale") review.Correct(field.Binding, field.Value, Source);
        if (scenario == "retracted") review.Retract(field.Binding);
        if (scenario != "stale") expected = review.Snapshot.Version;
        if (scenario == "foreign") expected = Review(p).Snapshot.Version;
        if (scenario == "null") expected = null!;
        if (scenario == "forged-profile")
        {
            var other = WorkflowProfiles.Get("synthetic-laboratory");
            review = Review(other);
            expected = review.Snapshot.Version with { ProfileId = p.Id, ProfileHash = WorkflowProfiles.Hash(p) };
        }
        var before = JsonSerializer.Serialize(review.Snapshot);
        var info = session.Current;
        Assert.AreEqual(code, Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedMapping.Preview(session, Destination(session, p), review, expected)).Code);
        Assert.AreEqual(before, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(info, session.Current);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void VerifiedInvalidTypesRejectAndOptionalAbsenceDoesNotInventValues(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var values = Values(p);
        values[p.ContextFields[0]] = "=1+2";
        var review = Review(p, values: values);
        Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedMapping.Preview(session, Destination(session, p), review, review.Snapshot.Version)).Code);
        values[p.ContextFields[0]] = "0";
        values.Remove(p.ContextFields[1]);
        review = Review(p, values: values);
        var preview = VerifiedMapping.Preview(session, Destination(session, p), review, review.Snapshot.Version);
        var optional = preview.Fields.Single(f => f.Value.Kind == "date").Value;
        Assert.IsNull(optional.DateValue);
        Assert.IsNull(optional.DecimalValue);
        Assert.IsNull(optional.Source);
        Assert.AreEqual("", optional.LiteralText);
        Assert.AreEqual(0m, preview.Fields.Single(f => f.Value.Kind == "decimal").Value.DecimalValue);
    }

    [TestMethod]
    [DataRow("headers", "AMBIGUOUS_HEADERS")]
    [DataRow("boundary", "UNSAFE_BOUNDARY")]
    [DataRow("formula-identity", "AMBIGUOUS_RECORD")]
    [DataRow("truncated", "ANALYSIS_UNSUPPORTED")]
    [DataRow("bounds", "ANALYSIS_LIMIT")]
    public void IncompatibleWorksheetRejectsWithoutMutations(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var target = Destination(session, p);
        var cells = session.Cells[target.WorksheetId];
        if (scenario == "headers") cells[(2, 1)] = cells[(2, 1)] with { Value = "Missing header" };
        if (scenario == "boundary") cells[(10, 1)] = cells[(10, 1)] with { Value = "Missing subtotal" };
        if (scenario == "formula-identity") cells[(6, 1)] = cells[(6, 1)] with { Formula = "A7" };
        if (scenario == "truncated") cells[(3, 1)] = cells[(3, 1)] with { Truncated = true };
        if (scenario == "bounds") session.Current = session.Current with
        { Worksheets = [session.Current.Worksheets[0] with { LastRow = p.MaxRows + 1 }] };
        var before = cells.Values.ToArray();
        var beforeReview = JsonSerializer.Serialize(review.Snapshot);
        var info = session.Current;
        Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedMapping.Preview(session, target, review, review.Snapshot.Version)).Code);
        CollectionAssert.AreEqual(before, cells.Values.ToArray());
        Assert.AreEqual(beforeReview, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(info, session.Current);
    }

    [TestMethod]
    [DataRow("review")]
    [DataRow("revision")]
    [DataRow("source")]
    [DataRow("session")]
    [DataRow("worksheet")]
    [DataRow("cancel")]
    public void ChangeDuringCaptureRejectsRatherThanReturningMixedVersions(string scenario)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var expected = review.Snapshot.Version;
        var target = Destination(session, p);
        using var cancellation = new CancellationTokenSource();
        session.AfterRead = () =>
        {
            session.AfterRead = null;
            if (scenario == "review") review.Correct(review.Snapshot.Fields[0].Binding, "Changed during analysis", Source);
            if (scenario == "revision") session.Current = session.Current with { Revision = 1 };
            if (scenario == "source") session.Current = session.Current with { SourceSha256 = new string('C', 64) };
            if (scenario == "session") session.Current = session.Current with { SessionId = Guid.NewGuid() };
            if (scenario == "worksheet") session.Current = session.Current with { Worksheets = [] };
            if (scenario == "cancel") cancellation.Cancel();
        };
        if (scenario == "review") Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedMapping.Preview(session, target, review, expected)).Code);
        else if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(() =>
            VerifiedMapping.Preview(session, target, review, expected, cancellation.Token));
        else Assert.ThrowsExactly<WorkbookException>(() => VerifiedMapping.Preview(session, target, review, expected));
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    public void DestinationRecheckedAfterLiveProjectionAndCancellationBeforeReading()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var target = Destination(session, p);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() =>
            VerifiedMapping.Preview(session, target, review, review.Snapshot.Version, cancellation.Token));
        Assert.AreEqual(0, session.Reads);
        session.BeforeInfo = () =>
        {
            if (session.InfoReads == 4) session.Current = session.Current with { Revision = 1 };
        };
        Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedMapping.Preview(session, target, review, review.Snapshot.Version)).Code);
        Assert.AreEqual(4, session.InfoReads);
    }

    [TestMethod]
    [DataRow("correct")]
    [DataRow("retract")]
    [DataRow("cancel")]
    public void ReviewAndCancellationAreRecheckedAfterFinalDestinationRead(string scenario)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var expected = review.Snapshot.Version;
        var target = Destination(session, p);
        using var cancellation = new CancellationTokenSource();
        session.BeforeInfo = () =>
        {
            if (session.InfoReads != 4) return;
            if (scenario == "correct") review.Correct(review.Snapshot.Fields[0].Binding, "Changed at final capture", Source);
            if (scenario == "retract") review.Retract(review.Snapshot.Fields[0].Binding);
            if (scenario == "cancel") cancellation.Cancel();
        };
        void Run() => VerifiedMapping.Preview(session, target, review, expected, cancellation.Token);
        if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(Run).Code);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    public void CorrectionInvalidatesOldPreviewAndFreshVerificationRetainsNewProvenance()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var old = VerifiedMapping.Preview(session, Destination(session, p), review, review.Snapshot.Version);
        var literal = old.Fields[0].Value.LiteralText;
        var source = Source with { DocumentRevision = 3, PageNumber = 4 };
        review.Correct(review.Snapshot.Fields[0].Binding, "New artificial organization", source);
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedMapping.Preview(session, old.Destination, review, old.Review)).Code);
        Assert.AreEqual("REVIEW_INCOMPLETE", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedMapping.Preview(session, old.Destination, review, review.Snapshot.Version)).Code);
        Verify(review);
        var fresh = VerifiedMapping.Preview(session, old.Destination, review, review.Snapshot.Version);
        Assert.AreEqual(literal, old.Fields[0].Value.LiteralText);
        Assert.AreEqual("New artificial organization", fresh.Fields[0].Value.LiteralText);
        Assert.AreEqual(1, fresh.Fields[0].Value.ValueRevision);
        Assert.AreEqual(source, fresh.Fields[0].Value.Source);
        Assert.AreNotEqual(old.Review, fresh.Review);
    }

    private sealed class Session : IWorkbookSession
    {
        public WorkbookInfo Current;
        public readonly Dictionary<Guid, Dictionary<(int Row, int Column), CellInfo>> Cells = [];
        public Action? AfterRead;
        public Action? BeforeInfo;
        public int Reads;
        public int InfoReads;
        public Session(WorkflowProfile p)
        {
            var first = WorkflowPlanningTests.Capture(p);
            var second = WorkflowPlanningTests.Capture(p, 20);
            var sheets = new[] { first.Sheet with { Name = "Artificial first" },
                second.Sheet with { Name = "Artificial shifted", Order = 2, LastColumn = p.MaxColumns } };
            Current = first.Info with { Worksheets = sheets };
            Cells[sheets[0].Id] = first.Cells;
            Cells[sheets[1].Id] = second.Cells.Values.ToDictionary(c => (c.Row, p.MaxColumns + 1 - c.Column),
                c => c with { Column = p.MaxColumns + 1 - c.Column });
        }
        public WorkbookInfo Info { get { InfoReads++; BeforeInfo?.Invoke(); return Current; } }
        public CellViewport ReadViewport(Guid id, int row, int column, int rows, int columns)
        {
            Reads++;
            var result = new CellViewport(Current.SessionId, id, row, column, rows, columns,
                Cells[id].Values.Where(c => c.Row >= row && c.Row < row + rows && c.Column >= column && c.Column < column + columns).ToArray());
            AfterRead?.Invoke();
            return result;
        }
        public WorkingCopyInfo? WorkingCopy => null;
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public void Dispose() { }
    }
}
