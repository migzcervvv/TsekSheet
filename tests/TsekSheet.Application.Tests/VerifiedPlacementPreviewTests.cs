using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedPlacementPreviewTests
{
    private static SourceEvidence Source => new(Guid.Parse("4b2a2e10-91ba-42c9-a81a-9c8857bfb40b"), new string('B', 64), 2, 3);
    private static FieldReviewSession Review(WorkflowProfile p, string entity = "Other Tools Ltd", string identifier = "NEW", bool verify = true)
    {
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, Source), new(p.IdentifierFields[0], identifier, Source),
            new(p.Fields[1].Key, "  =SUM(A1:A2)  ", Source), new(p.ContextFields[0], "+00125.5000", Source),
            new(p.ContextFields[1], "2024-02-29", Source)]);
        if (verify) Verify(review);
        return review;
    }
    private static void Verify(FieldReviewSession review)
    {
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
    }
    private static MappingDestination Destination(Session session, WorkflowProfile p, int index = 0) =>
        new(session.Current.SessionId, session.Current.Worksheets[index].Id, session.Current.Revision,
            session.Current.SourceSha256, p.Id, WorkflowProfiles.Hash(p));
    private static VerifiedPlacementPreview Preview(Session session, WorkflowProfile p, FieldReviewSession review,
        PlacementChoices? choices = null, int index = 0) =>
        VerifiedPlacement.Preview(session, Destination(session, p, index), review, review.Snapshot.Version, choices);

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void ExactAndFallbackPlacementsRetainTypedLiteralProvenanceAndIndependentBindings(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var review = Review(p, "  other   tools LTD  ");
        var before = JsonSerializer.Serialize(review.Snapshot);
        var cells = JsonSerializer.Serialize(session.Cells.Values.SelectMany(c => c.Values));
        var info = session.Current;
        var exact = Preview(session, p, review);
        Assert.AreEqual(8, exact.Proposal.Records[0].ProposedRow);
        Assert.AreEqual(p.Sections[0].Id, exact.Proposal.Records[0].SectionId);
        Assert.AreEqual("other   tools LTD", exact.Proposal.Records[0].Vendor);
        foreach (var index in new[] { 0, 1 })
        {
            var fallback = Preview(session, p, Review(p, "Novel Organization"), index: index);
            Assert.AreEqual(index == 0 ? 22 : 42, fallback.Proposal.Records[0].BaseInsertionRow);
            Assert.AreEqual(index == 0 ? 25 : 45, fallback.Proposal.Records[0].AffectedThroughRow);
            Assert.AreEqual(p.FallbackSectionId, fallback.Proposal.Records[0].SectionId);
            Assert.AreEqual(index == 0 ? 1 : p.MaxColumns, fallback.Mapping.Fields[0].Column);
            Assert.AreEqual(session.Current.Worksheets[index].Id, fallback.Mapping.Destination.WorksheetId);
            Assert.IsTrue(fallback.Proposal.ReadyForReview);
            Assert.IsFalse(fallback.CanApply);
            Assert.IsFalse(fallback.Proposal.CanApply);
            Assert.IsFalse(fallback.Mapping.CanApply);
        }
        var mapped = exact.Mapping.Fields;
        Assert.AreEqual("  other   tools LTD  ", mapped[0].Value.LiteralText);
        Assert.AreEqual("  =SUM(A1:A2)  ", mapped[1].Value.LiteralText);
        Assert.AreEqual(125.5000m, mapped.Single(f => f.Value.Kind == "decimal").Value.DecimalValue);
        Assert.AreEqual(new DateOnly(2024, 2, 29), mapped.Single(f => f.Value.Kind == "date").Value.DateValue);
        Assert.IsNull(mapped.Single(f => f.Value.Key == p.IdentifierFields[1]).Value.Source);
        Assert.IsTrue(mapped.Where(f => f.Value.Source != null).All(f => f.Value.Source == Source));
        Assert.AreEqual(review.Snapshot.Version, exact.Mapping.Review);
        foreach (var field in mapped)
        {
            var planned = exact.Proposal.Records[0].Fields.Single(f => f.Key == field.Value.Key);
            Assert.AreEqual(field.Value.NormalizedText, planned.Value);
            Assert.AreEqual(field.Column, planned.Column);
        }
        Assert.AreEqual(JsonSerializer.Serialize(exact), JsonSerializer.Serialize(Preview(session, p, review)));
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<RecordInsertionProposal>)exact.Proposal.Records).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MappedField>)exact.Proposal.Records[0].Fields).Clear());
        Assert.AreEqual(before, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(cells, JsonSerializer.Serialize(session.Cells.Values.SelectMany(c => c.Values)));
        Assert.AreEqual(info, session.Current);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void SectionAmbiguityAndDuplicatesRemainVisibleUntilExplicitValidChoice(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var review = Review(p, "Acme Trading Incorporated", "R1");
        var unresolved = Preview(session, p, review);
        var record = unresolved.Proposal.Records.Single();
        Assert.IsTrue(record.RequiresSectionChoice);
        Assert.IsFalse(unresolved.Proposal.ReadyForReview);
        Assert.AreEqual(2, record.Matches.Count);
        Assert.IsNull(record.ProposedRow);
        Assert.AreEqual(6, record.Duplicates.Single().ExistingRow);
        StringAssert.Contains(record.Duplicates[0].Reason, p.ContextFields[0]);
        foreach (var index in new[] { 0, 1 })
        {
            var chosen = Preview(session, p, review, new(p.Sections[index].Id));
            Assert.AreEqual(index == 0 ? 9 : 18, chosen.Proposal.Records[0].ProposedRow);
            Assert.AreEqual(record.Duplicates[0], chosen.Proposal.Records[0].Duplicates[0]);
            Assert.IsTrue(chosen.Proposal.ReadyForReview);
            Assert.IsFalse(chosen.CanApply);
            Assert.IsNull(typeof(IWorkbookSession).GetMethod("InsertWorkflowRows"));
        }
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<DuplicateWarning>)record.Duplicates).Clear());
        Assert.AreEqual("INVALID_SECTION_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
            Preview(session, p, review, new("foreign"))).Code);
        Assert.AreEqual("INVALID_SECTION_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
            Preview(session, p, Review(p), new(p.Sections[1].Id))).Code);
        Assert.AreEqual("INVALID_VENDOR_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
            Preview(session, p, review, new(ConfirmNewEntity: true))).Code);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void SimilarEntityRequiresRetentionChoiceOrCorrectionAndRenewedVerification(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var review = Review(p, "Other Tools Limited");
        var old = Preview(session, p, review);
        var unresolved = old.Proposal.Records.Single();
        Assert.IsTrue(unresolved.RequiresVendorChoice);
        Assert.IsNull(unresolved.ProposedRow);
        CollectionAssert.Contains(unresolved.SuggestedVendors.ToArray(), "Other Tools Ltd");
        Assert.AreEqual("Other Tools Limited", unresolved.Vendor);
        Assert.AreEqual("CHOICE_REQUIRED", Assert.ThrowsExactly<WorkbookException>(() =>
            Preview(session, p, review, new(p.FallbackSectionId))).Code);
        var retained = Preview(session, p, review, new(ConfirmNewEntity: true));
        Assert.AreEqual("Other Tools Limited", retained.Proposal.Records[0].Vendor);
        Assert.AreEqual(22, retained.Proposal.Records[0].ProposedRow);
        var source = Source with { PageNumber = 4, DocumentRevision = 3 };
        review.Correct(review.Snapshot.Fields.Single(f => f.Key == p.EntityField).Binding, "Other Tools Ltd", source);
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedPlacement.Preview(session, old.Mapping.Destination, review, old.Mapping.Review)).Code);
        Assert.AreEqual("REVIEW_INCOMPLETE", Assert.ThrowsExactly<FieldReviewException>(() => Preview(session, p, review)).Code);
        Verify(review);
        var corrected = Preview(session, p, review);
        Assert.AreEqual(8, corrected.Proposal.Records[0].ProposedRow);
        Assert.AreEqual("Other Tools Limited", old.Mapping.Fields[0].Value.LiteralText);
        Assert.AreEqual("Other Tools Ltd", corrected.Mapping.Fields[0].Value.LiteralText);
        Assert.AreEqual(source, corrected.Mapping.Fields[0].Value.Source);
        Assert.AreEqual(1, corrected.Mapping.Fields[0].Value.ValueRevision);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void ConfiguredAliasesMatchWithoutReplacingVerifiedText(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var alias = p.Aliases[0];
        var sheet = session.Current.Worksheets[0];
        session.Cells[sheet.Id][(7, 1)] = session.Cells[sheet.Id][(7, 1)] with { Value = alias.Canonical };
        var result = Preview(session, p, Review(p, alias.Alias));
        Assert.AreEqual("configured alias", result.Proposal.Records[0].MatchReason);
        Assert.AreEqual(alias.Alias, result.Proposal.Records[0].Vendor);
        Assert.AreEqual(alias.Alias, result.Mapping.Fields[0].Value.LiteralText);
        Assert.IsFalse(result.Proposal.Records[0].RequiresVendorChoice);
    }

    [TestMethod]
    [DataRow("draft", "REVIEW_INCOMPLETE")]
    [DataRow("retracted", "REVIEW_INCOMPLETE")]
    [DataRow("foreign", "STALE_RECORD_REVIEW")]
    [DataRow("stale", "STALE_RECORD_REVIEW")]
    [DataRow("null", "STALE_RECORD_REVIEW")]
    public void InvalidLiveReviewRejectsWithoutChangingEitherSession(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p, verify: scenario != "draft");
        var expected = review.Snapshot.Version;
        if (scenario == "retracted") { review.Retract(review.Snapshot.Fields[0].Binding); expected = review.Snapshot.Version; }
        if (scenario == "stale") review.Correct(review.Snapshot.Fields[0].Binding, "Changed", Source);
        if (scenario == "foreign") expected = Review(p).Snapshot.Version;
        if (scenario == "null") expected = null!;
        var before = JsonSerializer.Serialize(review.Snapshot);
        var info = session.Current;
        Assert.AreEqual(code, Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedPlacement.Preview(session, Destination(session, p), review, expected)).Code);
        Assert.AreEqual(before, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(info, session.Current);
    }

    [TestMethod]
    [DataRow("session", "INVALID_MAPPING_DESTINATION")]
    [DataRow("source", "INVALID_MAPPING_DESTINATION")]
    [DataRow("sheet", "UNKNOWN_WORKSHEET")]
    [DataRow("revision", "STALE_REVISION")]
    [DataRow("profile", "STALE_PROFILE")]
    public void InvalidDestinationRejectsBeforeReading(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        var target = Destination(session, p);
        target = scenario switch
        {
            "session" => target with { SessionId = Guid.NewGuid() },
            "source" => target with { SourceSha256 = new string('C', 64) },
            "sheet" => target with { WorksheetId = Guid.NewGuid() },
            "revision" => target with { Revision = 1 },
            _ => target with { ProfileHash = "stale" }
        };
        Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedPlacement.Preview(session, target, review, review.Snapshot.Version)).Code);
        Assert.AreEqual(0, session.Reads);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    [DataRow("review")]
    [DataRow("revision")]
    [DataRow("cancel")]
    [DataRow("final-revision")]
    [DataRow("final-review")]
    [DataRow("final-cancel")]
    public void ChangesDuringCaptureAndCancellationReject(string scenario)
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
            if (scenario == "review") review.Correct(review.Snapshot.Fields[0].Binding, "Changed", Source);
            if (scenario == "revision") session.Current = session.Current with { Revision = 1 };
            if (scenario == "cancel") cancellation.Cancel();
        };
        if (scenario.StartsWith("final-", StringComparison.Ordinal)) session.BeforeInfo = () =>
        {
            if (session.InfoReads != 4) return;
            if (scenario == "final-revision") session.Current = session.Current with { Revision = 1 };
            if (scenario == "final-review") review.Correct(review.Snapshot.Fields[0].Binding, "Changed at final capture", Source);
            if (scenario == "final-cancel") cancellation.Cancel();
        };
        void Run() => VerifiedPlacement.Preview(session, target, review, expected, token: cancellation.Token);
        if (scenario is "review" or "final-review") Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(Run).Code);
        else if (scenario is "cancel" or "final-cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(Run).Code);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    public void InvalidTypesIncompatibleSheetsAndEarlyCancellationReject()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var review = Review(p);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => VerifiedPlacement.Preview(session,
            Destination(session, p), review, review.Snapshot.Version, token: cancellation.Token));
        Assert.AreEqual(0, session.Reads);
        review.Correct(review.Snapshot.Fields.Single(f => f.Key == p.ContextFields[0]).Binding, "=1+2", Source);
        Verify(review);
        Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() => Preview(session, p, review)).Code);
        var sheet = session.Current.Worksheets[0];
        session.Cells[sheet.Id][(10, 1)] = session.Cells[sheet.Id][(10, 1)] with { Value = "Missing subtotal" };
        Assert.AreEqual("UNSAFE_BOUNDARY", Assert.ThrowsExactly<WorkbookException>(() => Preview(session, p, Review(p))).Code);
    }

    internal sealed class Session : IWorkbookSession
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
            var sheets = new[] { first.Sheet, second.Sheet with { Name = "Independent shifted", Order = 2, LastColumn = p.MaxColumns } };
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
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Placement must only read.");
        public void Dispose() { }
    }
}
