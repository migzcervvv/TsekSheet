using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using Session = TsekSheet.Application.Tests.VerifiedPlacementPreviewTests.Session;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedPlacementBatchTests
{
    private static FieldReviewSession Review(WorkflowProfile p, string entity = "Other Tools Ltd", string id = "NEW", bool verify = true)
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('D', 64), 2, 3);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, source), new(p.IdentifierFields[0], id, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "+00125.5000", source),
            new(p.ContextFields[1], "2024-02-29", source)]);
        if (verify) Verify(review);
        return review;
    }
    private static void Verify(FieldReviewSession review)
    {
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
    }
    private static VerifiedPlacementInput Input(FieldReviewSession review, PlacementChoices? choices = null) => new(review, review.Snapshot.Version, choices);
    private static MappingDestination Destination(Session session, WorkflowProfile p, int index = 0) =>
        new(session.Current.SessionId, session.Current.Worksheets[index].Id, session.Current.Revision,
            session.Current.SourceSha256, p.Id, WorkflowProfiles.Hash(p));
    private static string Snap(object value) => JsonSerializer.Serialize(value);

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void OrderedRecordsRetainCrossRecordWarningsTypesSourcesAndIndependentDestinations(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var reviews = new[] { Review(p, "Novel Organization"), Review(p), Review(p), Review(p, "Acme Trading Incorporated", "R1") };
        var inputs = reviews.Select(r => Input(r)).ToArray();
        inputs[3] = Input(reviews[3], new(p.Sections[1].Id));
        var before = reviews.Select(r => Snap(r.Snapshot)).ToArray();
        var cells = Snap(session.Cells.Values.SelectMany(c => c.Values));
        var info = session.Current;
        foreach (var sheetIndex in new[] { 0, 1 })
        {
            var target = Destination(session, p, sheetIndex);
            var result = VerifiedPlacement.PreviewMany(session, target, inputs);
            Assert.IsTrue(result.Proposal.ReadyForReview);
            Assert.IsFalse(result.CanApply);
            Assert.IsFalse(result.Proposal.CanApply);
            CollectionAssert.AreEqual(new int?[] { sheetIndex == 0 ? 25 : 45, 8, 9, sheetIndex == 0 ? 20 : 40 },
                result.Proposal.Records.Select(r => r.ProposedRow).ToArray());
            for (var index = 0; index < inputs.Length; index++)
            {
                var mapping = result.Mappings[index];
                var proposal = result.Proposal.Records[index];
                Assert.AreEqual(inputs[index].ExpectedReview.ReviewId.ToString("N"), proposal.RecordId);
                Assert.AreEqual(inputs[index].ExpectedReview, mapping.Review);
                Assert.AreEqual(target, mapping.Destination);
                Assert.AreEqual(sheetIndex == 0 ? 1 : p.MaxColumns, mapping.Fields[0].Column);
                Assert.AreEqual(sheetIndex == 0 ? 22 : 42, mapping.Sections[1].SubtotalRow);
                Assert.IsFalse(mapping.CanApply);
                Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
                Assert.AreEqual(125.5000m, mapping.Fields.Single(f => f.Value.Kind == "decimal").Value.DecimalValue);
                Assert.AreEqual(new DateOnly(2024, 2, 29), mapping.Fields.Single(f => f.Value.Kind == "date").Value.DateValue);
                Assert.IsNull(mapping.Fields.Single(f => f.Value.Key == p.IdentifierFields[1]).Value.Source);
                foreach (var field in mapping.Fields)
                {
                    var source = reviews[index].Snapshot.Fields.Single(f => f.Key == field.Value.Key);
                    Assert.AreEqual(source.Source, field.Value.Source);
                    Assert.AreEqual(source.ValueRevision, field.Value.ValueRevision);
                    Assert.AreEqual(field.Value.NormalizedText, proposal.Fields.Single(f => f.Key == field.Value.Key).Value);
                }
            }
            foreach (var (index, other) in new[] { (1, 2), (2, 1) })
                Assert.AreEqual(inputs[other].ExpectedReview.ReviewId.ToString("N"), result.Proposal.Records[index].Duplicates.Single().BatchRecordId);
            Assert.AreEqual(6, result.Proposal.Records[3].Duplicates.Single().ExistingRow);
            Assert.AreEqual(Snap(result), Snap(VerifiedPlacement.PreviewMany(session, target, inputs)));
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappingPreview>)result.Mappings).Clear());
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappedField>)result.Mappings[0].Fields).Clear());
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<RecordInsertionProposal>)result.Proposal.Records).Clear());
            Assert.IsNull(typeof(IWorkbookSession).GetMethod("InsertWorkflowRows"));
        }
        CollectionAssert.AreEqual(before, reviews.Select(r => Snap(r.Snapshot)).ToArray());
        Assert.AreEqual(cells, Snap(session.Cells.Values.SelectMany(c => c.Values)));
        Assert.AreEqual(info, session.Current);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void OneAndTwentyRecordsRespectExistingPlannerBoundsAndInputOrder(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var inputs = Enumerable.Range(0, 20).Select(i => Input(Review(p, id: $"NEW-{i}"))).ToArray();
        var target = Destination(session, p);
        var single = VerifiedPlacement.Preview(session, target, inputs[0].Review, inputs[0].ExpectedReview);
        var one = VerifiedPlacement.PreviewMany(session, target, [inputs[0]]);
        Assert.AreEqual(Snap(single.Mapping), Snap(one.Mappings.Single()));
        Assert.AreEqual(Snap(single.Proposal), Snap(one.Proposal));
        var twenty = VerifiedPlacement.PreviewMany(session, target, inputs);
        CollectionAssert.AreEqual(Enumerable.Range(8, 20).Select(i => (int?)i).ToArray(), twenty.Proposal.Records.Select(r => r.ProposedRow).ToArray());
        Assert.IsTrue(twenty.Proposal.Records.All(r => r.Duplicates.Count == 0 && r.BaseInsertionRow == 8));
        var reversed = VerifiedPlacement.PreviewMany(session, target, inputs.Reverse().ToArray());
        CollectionAssert.AreEqual(inputs.Reverse().Select(i => i.ExpectedReview).ToArray(), reversed.Mappings.Select(m => m.Review).ToArray());
        CollectionAssert.AreEqual(twenty.Proposal.Records.Select(r => r.ProposedRow).ToArray(), reversed.Proposal.Records.Select(r => r.ProposedRow).ToArray());
        Assert.IsFalse(twenty.CanApply);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void UnresolvedChoicesRemainPerRecordAndBlockAllVirtualRows(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var a = Review(p, "Acme Trading Incorporated", "R1");
        var b = Review(p, "Other Tools Limited");
        var inputs = new[] { Input(a), Input(b), Input(Review(p)) };
        var target = Destination(session, p);
        var result = VerifiedPlacement.PreviewMany(session, target, inputs);
        Assert.IsFalse(result.Proposal.ReadyForReview);
        Assert.IsTrue(result.Proposal.Records[0].RequiresSectionChoice);
        Assert.IsTrue(result.Proposal.Records[1].RequiresVendorChoice);
        Assert.IsTrue(result.Proposal.Records.All(r => r.ProposedRow == null));
        Assert.AreEqual(6, result.Proposal.Records[0].Duplicates.Single().ExistingRow);
        var resolved = VerifiedPlacement.PreviewMany(session, target,
            [Input(a, new(p.Sections[1].Id)), Input(b, new(ConfirmNewEntity: true)), inputs[2]]);
        Assert.IsTrue(resolved.Proposal.ReadyForReview);
        Assert.AreEqual("Other Tools Limited", resolved.Mappings[1].Fields[0].Value.LiteralText);
        Assert.AreEqual("Other Tools Limited", resolved.Proposal.Records[1].Vendor);
        Assert.AreEqual("INVALID_SECTION_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedPlacement.PreviewMany(session, target, [inputs[2], Input(a, new("foreign"))])).Code);
        b.Correct(b.Snapshot.Fields[0].Binding, "Other Tools Ltd", b.Snapshot.Fields[0].Source!);
        Assert.AreEqual("REVIEW_INCOMPLETE", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedPlacement.PreviewMany(session, target, [inputs[2], Input(b)])).Code);
        Verify(b);
        var corrected = VerifiedPlacement.PreviewMany(session, target, [inputs[2], Input(b)]);
        Assert.AreEqual(1, corrected.Mappings[1].Fields[0].Value.ValueRevision);
        Assert.AreEqual("Other Tools Limited", result.Mappings[1].Fields[0].Value.LiteralText);
    }

    [TestMethod]
    [DataRow("null-list")]
    [DataRow("empty")]
    [DataRow("too-many")]
    [DataRow("null-item")]
    [DataRow("null-review")]
    [DataRow("repeat")]
    public void InvalidCollectionsRejectBeforeReading(string scenario)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var item = Input(Review(p));
        IReadOnlyList<VerifiedPlacementInput> inputs = scenario switch
        {
            "null-list" => null!, "empty" => [], "too-many" => Enumerable.Repeat(item, 21).ToArray(),
            "null-item" => [item, null!], "null-review" => [item, new(null!, item.ExpectedReview)], _ => [item, item]
        };
        var before = Snap(item.Review.Snapshot);
        Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedPlacement.PreviewMany(session, Destination(session, p), inputs)).Code);
        Assert.AreEqual(0, session.Reads);
        Assert.AreEqual(before, Snap(item.Review.Snapshot));
    }

    [TestMethod]
    [DataRow("draft", "REVIEW_INCOMPLETE")]
    [DataRow("retracted", "REVIEW_INCOMPLETE")]
    [DataRow("stale", "STALE_RECORD_REVIEW")]
    [DataRow("foreign", "STALE_RECORD_REVIEW")]
    [DataRow("null-version", "STALE_RECORD_REVIEW")]
    [DataRow("forged-id", "STALE_RECORD_REVIEW")]
    [DataRow("profile", "STALE_PROFILE")]
    [DataRow("type", "INVALID_RECORD")]
    public void InvalidLaterRecordRejectsWholeObservationWithoutChanges(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var first = Input(Review(p));
        var review = Review(p, verify: scenario != "draft");
        var last = Input(review);
        if (scenario == "retracted") { review.Retract(review.Snapshot.Fields[0].Binding); last = Input(review); }
        if (scenario == "stale") review.Correct(review.Snapshot.Fields[0].Binding, "Changed", review.Snapshot.Fields[0].Source!);
        if (scenario == "foreign") last = last with { ExpectedReview = Review(p).Snapshot.Version };
        if (scenario == "null-version") last = last with { ExpectedReview = null! };
        if (scenario == "forged-id") last = last with { ExpectedReview = last.ExpectedReview with { ReviewId = first.ExpectedReview.ReviewId } };
        if (scenario == "profile") last = Input(Review(WorkflowProfiles.Get("synthetic-laboratory")));
        if (scenario == "type")
        {
            review.Correct(review.Snapshot.Fields.Single(f => f.Key == p.ContextFields[0]).Binding, "=1+2", review.Snapshot.Fields[0].Source!);
            Verify(review);
            last = Input(review);
        }
        var before = new[] { Snap(first.Review.Snapshot), Snap(last.Review.Snapshot), Snap(session.Current) };
        Exception failure = Assert.Throws<Exception>(() => VerifiedPlacement.PreviewMany(session, Destination(session, p), [first, last]));
        Assert.AreEqual(code, failure is FieldReviewException field ? field.Code : ((WorkbookException)failure).Code);
        Assert.AreEqual(0, session.Reads);
        CollectionAssert.AreEqual(before, new[] { Snap(first.Review.Snapshot), Snap(last.Review.Snapshot), Snap(session.Current) });
    }

    [TestMethod]
    [DataRow("session", "INVALID_MAPPING_DESTINATION")]
    [DataRow("source", "INVALID_MAPPING_DESTINATION")]
    [DataRow("sheet", "UNKNOWN_WORKSHEET")]
    [DataRow("revision", "STALE_REVISION")]
    [DataRow("profile", "STALE_PROFILE")]
    [DataRow("null", "INVALID_MAPPING_REQUEST")]
    public void InvalidDestinationRejectsAllRecordsBeforeReading(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var target = Destination(session, p);
        target = scenario switch
        {
            "session" => target with { SessionId = Guid.NewGuid() }, "source" => target with { SourceSha256 = "foreign" },
            "sheet" => target with { WorksheetId = Guid.NewGuid() }, "revision" => target with { Revision = 1 },
            "null" => null!, _ => target with { ProfileHash = "stale" }
        };
        Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
            VerifiedPlacement.PreviewMany(session, target, [Input(Review(p)), Input(Review(p))])).Code);
        Assert.AreEqual(0, session.Reads);
    }

    [TestMethod]
    [DataRow(0, false)] [DataRow(1, false)] [DataRow(2, false)]
    [DataRow(0, true)] [DataRow(1, true)] [DataRow(2, true)]
    public void EveryLiveReviewIsRecheckedAfterCapture(int index, bool finalRead)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var inputs = Enumerable.Range(0, 3).Select(i => Input(Review(p, id: $"NEW-{i}"))).ToArray();
        void Change()
        {
            var review = inputs[index].Review;
            review.Correct(review.Snapshot.Fields[0].Binding, "Changed during capture", review.Snapshot.Fields[0].Source!);
        }
        if (finalRead) session.BeforeInfo = () => { if (session.InfoReads == 4) Change(); };
        else session.AfterRead = () => { session.AfterRead = null; Change(); };
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedPlacement.PreviewMany(session, Destination(session, p), inputs)).Code);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    [DataRow("early-cancel")]
    [DataRow("read-cancel")]
    [DataRow("final-cancel")]
    [DataRow("read-revision")]
    [DataRow("final-revision")]
    public void CancellationAndDestinationChangesRejectWholePreview(string scenario)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        using var cancel = new CancellationTokenSource();
        var inputs = new[] { Input(Review(p)), Input(Review(p)) };
        var target = Destination(session, p);
        if (scenario == "early-cancel") cancel.Cancel();
        void Change()
        {
            if (scenario.EndsWith("cancel", StringComparison.Ordinal)) cancel.Cancel();
            else session.Current = session.Current with { Revision = 1 };
        }
        if (scenario.StartsWith("read-", StringComparison.Ordinal)) session.AfterRead = () => { session.AfterRead = null; Change(); };
        if (scenario.StartsWith("final-", StringComparison.Ordinal)) session.BeforeInfo = () => { if (session.InfoReads == 4) Change(); };
        void Run() => VerifiedPlacement.PreviewMany(session, target, inputs, cancel.Token);
        if (scenario.EndsWith("cancel", StringComparison.Ordinal)) Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(Run).Code);
        if (scenario == "early-cancel") Assert.AreEqual(0, session.Reads);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    public void CallerListChangesCannotReplaceCapturedInputs()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var inputs = new List<VerifiedPlacementInput> { Input(Review(p)), Input(Review(p)) };
        var expected = inputs.Select(i => i.ExpectedReview).ToArray();
        session.AfterRead = () => inputs.Clear();
        var result = VerifiedPlacement.PreviewMany(session, Destination(session, p), inputs);
        CollectionAssert.AreEqual(expected, result.Mappings.Select(m => m.Review).ToArray());
    }
}
