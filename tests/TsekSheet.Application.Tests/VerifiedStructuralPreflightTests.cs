using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using Session = TsekSheet.Application.Tests.VerifiedPlacementPreviewTests.Session;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedStructuralPreflightTests
{
    private static VerifiedPlacementInput Input(WorkflowProfile p, string entity = "Other Tools Ltd", string id = "NEW", PlacementChoices? choices = null)
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('A', 64), 1, 2);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, source), new(p.IdentifierFields[0], id, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "+00125.5000", source),
            new(p.ContextFields[1], "2024-02-29", source)]);
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
        return new(review, review.Snapshot.Version, choices);
    }

    private static MappingDestination Destination(Session session, WorkflowProfile p, int index = 0) =>
        new(session.Current.SessionId, session.Current.Worksheets[index].Id, session.Current.Revision,
            session.Current.SourceSha256, p.Id, WorkflowProfiles.Hash(p));

    internal static void AssertLifecycle(WorkflowPlan plan, string status)
    {
        Assert.IsFalse(plan.CanApply);
        Assert.AreEqual(status, plan.StructuralStatus);
        Assert.AreEqual(new WorkflowReviewState(plan.ReadyForReview ? "resolved" : "choices-required", status,
            "not-generated", "not-run", "not-approved", "not-adopted", "production-disabled", "unmerged-changes-excluded"), plan.Review);
    }

    [TestMethod]
    [DataRow("synthetic-payables", 1)]
    [DataRow("synthetic-payables", 20)]
    [DataRow("synthetic-laboratory", 1)]
    [DataRow("synthetic-laboratory", 20)]
    public void PreflightRetainsOrderedTypedProvenanceIndependentBindingsAndLifecycle(string profileId, int count)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var session = new Session(p);
        var inputs = Enumerable.Range(0, count).Select(i => Input(p, id: "NEW-" + i)).ToArray();
        var before = inputs.Select(i => JsonSerializer.Serialize(i.Review.Snapshot)).ToArray();
        var cells = JsonSerializer.Serialize(session.Cells.Values.SelectMany(c => c.Values));
        var info = session.Current;
        session.Inspect = (plan, _) => plan with { StructuralStatus = "experimental-envelope-only" };
        foreach (var index in new[] { 0, 1 })
        {
            var destination = Destination(session, p, index);
            var pure = VerifiedPlacement.PreviewMany(session, destination, inputs);
            Assert.AreEqual(index, session.Inspections, "PreviewMany must remain pure.");
            var result = VerifiedPlacement.PreflightMany(session, destination, inputs);
            AssertLifecycle(result.Proposal, "experimental-envelope-only");
            Assert.IsFalse(result.CanApply);
            Assert.AreEqual(JsonSerializer.Serialize(pure.Proposal), JsonSerializer.Serialize(result.Proposal with
                { StructuralStatus = pure.Proposal.StructuralStatus }));
            CollectionAssert.AreEqual(inputs.Select(i => i.ExpectedReview).ToArray(), result.Mappings.Select(m => m.Review).ToArray());
            CollectionAssert.AreEqual(inputs.Select(i => i.ExpectedReview.ReviewId.ToString("N")).ToArray(), result.Proposal.Records.Select(r => r.RecordId).ToArray());
            for (var i = 0; i < count; i++)
            {
                var mapping = result.Mappings[i];
                Assert.AreEqual(destination, mapping.Destination);
                Assert.AreEqual(index == 0 ? 1 : p.MaxColumns, mapping.Fields[0].Column);
                Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
                Assert.AreEqual(125.5m, mapping.Fields[4].Value.DecimalValue);
                Assert.AreEqual(new DateOnly(2024, 2, 29), mapping.Fields[5].Value.DateValue);
                Assert.AreEqual(inputs[i].Review.Snapshot.Fields[0].Source, mapping.Fields[0].Value.Source);
                Assert.AreEqual(8 + i, result.Proposal.Records[i].ProposedRow);
                Assert.AreEqual(22 + index * 20, mapping.Sections[1].SubtotalRow);
                Assert.IsFalse(mapping.CanApply);
            }
            Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappingPreview>)result.Mappings).Clear());
        }
        Assert.AreEqual(2, session.Inspections);
        Assert.AreEqual(info, session.Current);
        Assert.AreEqual(cells, JsonSerializer.Serialize(session.Cells.Values.SelectMany(c => c.Values)));
        CollectionAssert.AreEqual(before, inputs.Select(i => JsonSerializer.Serialize(i.Review.Snapshot)).ToArray());
    }

    [TestMethod]
    [DataRow("ambiguous", "ambiguous-destination")]
    [DataRow("similar", "ambiguous-destination")]
    [DataRow("existing-duplicate", "duplicate-review-required")]
    [DataRow("batch-duplicate", "duplicate-review-required")]
    public void ChoicesPrecedeDuplicatesAndBothAvoidStructuralInspection(string scenario, string expected)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        VerifiedPlacementInput[] inputs = scenario switch
        {
            "ambiguous" => [Input(p, "Acme Trading Incorporated", "R1")],
            "similar" => [Input(p, "Acme Trading", "R1")],
            "existing-duplicate" => [Input(p, "Acme Trading Incorporated", "R1", new(p.Sections[0].Id))],
            _ => [Input(p), Input(p)]
        };
        var result = VerifiedPlacement.PreflightMany(session, Destination(session, p), inputs);
        AssertLifecycle(result.Proposal, expected);
        Assert.AreEqual(0, session.Inspections);
        if (scenario == "batch-duplicate")
        {
            Assert.AreEqual(inputs[1].ExpectedReview.ReviewId.ToString("N"), result.Proposal.Records[0].Duplicates.Single().BatchRecordId);
            Assert.AreEqual(inputs[0].ExpectedReview.ReviewId.ToString("N"), result.Proposal.Records[1].Duplicates.Single().BatchRecordId);
        }
        if (scenario == "ambiguous")
        {
            Assert.IsTrue(result.Proposal.Records[0].Duplicates.Count > 0);
            Assert.IsTrue(result.Proposal.Records[0].RequiresSectionChoice);
            Assert.IsNull(result.Proposal.Records[0].ProposedRow);
        }
    }

    [TestMethod]
    [DataRow("experimental-envelope-only")]
    [DataRow("unsupported-structure")]
    public void CallerListMutationDuringPreflightCannotReplaceCapturedReviews(string status)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var inputs = new List<VerifiedPlacementInput> { Input(p, id: "A"), Input(p, id: "B") };
        var expected = inputs.Select(i => i.ExpectedReview).ToArray();
        session.Inspect = (plan, _) => { inputs.Clear(); return plan with { StructuralStatus = status }; };
        var result = VerifiedPlacement.PreflightMany(session, Destination(session, p), inputs);
        AssertLifecycle(result.Proposal, status);
        CollectionAssert.AreEqual(expected, result.Mappings.Select(m => m.Review).ToArray());
        Assert.AreEqual(1, session.Inspections);
    }

    [TestMethod]
    [DataRow(0, false)] [DataRow(1, false)] [DataRow(2, false)]
    [DataRow(0, true)] [DataRow(1, true)] [DataRow(2, true)]
    public void EveryLiveReviewIsRecheckedAfterPreflight(int index, bool retract)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        var inputs = Enumerable.Range(0, 3).Select(i => Input(p, id: "NEW-" + i)).ToArray();
        session.Inspect = (plan, _) =>
        {
            var review = inputs[index].Review;
            var field = review.Snapshot.Fields[0];
            if (retract) review.Retract(field.Binding);
            else review.Correct(field.Binding, field.Value, field.Source!);
            return plan with { StructuralStatus = "experimental-envelope-only" };
        };
        var before = session.Current;
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
            VerifiedPlacement.PreflightMany(session, Destination(session, p), inputs)).Code);
        Assert.AreEqual(before, session.Current);
        Assert.AreEqual(1, session.Inspections);
    }

    [TestMethod]
    [DataRow("revision", "STALE_REVISION")]
    [DataRow("session", "INVALID_MAPPING_DESTINATION")]
    [DataRow("source", "INVALID_MAPPING_DESTINATION")]
    [DataRow("sheet", "UNKNOWN_WORKSHEET")]
    [DataRow("cancel", "cancel")]
    public void DestinationAndCancellationAreRecheckedAfterPreflight(string scenario, string code)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        using var cancellation = new CancellationTokenSource();
        var destination = Destination(session, p);
        session.Inspect = (plan, _) =>
        {
            session.Current = scenario switch
            {
                "revision" => session.Current with { Revision = 1 },
                "session" => session.Current with { SessionId = Guid.NewGuid() },
                "source" => session.Current with { SourceSha256 = new string('F', 64) },
                "sheet" => session.Current with { Worksheets = [] },
                _ => session.Current
            };
            if (scenario == "cancel") cancellation.Cancel();
            return plan with { StructuralStatus = "unsupported-structure" };
        };
        void Run() => VerifiedPlacement.PreflightMany(session, destination, [Input(p)], cancellation.Token);
        if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(Run).Code);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    [DataRow("ambiguous", false)] [DataRow("duplicate", false)] [DataRow("inspected", false)]
    [DataRow("ambiguous", true)] [DataRow("duplicate", true)] [DataRow("inspected", true)]
    public void FinalDestinationReadCannotConcealReviewChangesOrCancellation(string branch, bool cancel)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        using var cancellation = new CancellationTokenSource();
        var input = branch == "ambiguous" ? Input(p, "Acme Trading Incorporated", "R1")
            : branch == "duplicate" ? Input(p, "Acme Trading Incorporated", "R1", new(p.Sections[0].Id)) : Input(p);
        session.Inspect = (plan, _) => plan with { StructuralStatus = "experimental-envelope-only" };
        session.BeforeInfo = () =>
        {
            if (session.InfoReads != 4) return;
            if (cancel) cancellation.Cancel();
            else input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
        };
        void Run() => VerifiedPlacement.PreflightMany(session, Destination(session, p), [input], cancellation.Token);
        if (cancel) Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(Run).Code);
        Assert.AreEqual(branch == "inspected" ? 1 : 0, session.Inspections);
        Assert.IsFalse(session.Current.IsDirty);
    }

    [TestMethod]
    [DataRow("empty")] [DataRow("oversize")] [DataRow("null")]
    [DataRow("repeated")] [DataRow("draft")] [DataRow("foreign")]
    [DataRow("type")] [DataRow("destination")] [DataRow("cancel")]
    public void InvalidInputsRejectBeforeStructuralInspection(string scenario)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var session = new Session(p);
        using var cancellation = new CancellationTokenSource();
        var input = Input(p);
        VerifiedPlacementInput[] inputs = [Input(p, id: "FIRST"), input];
        var destination = Destination(session, p);
        switch (scenario)
        {
            case "empty": inputs = []; break;
            case "oversize": inputs = Enumerable.Repeat(input, 21).ToArray(); break;
            case "null": inputs[1] = null!; break;
            case "repeated": inputs = [input, input]; break;
            case "foreign": inputs[1] = input with { ExpectedReview = Input(p).ExpectedReview }; break;
            case "draft": input.Review.Retract(input.Review.Snapshot.Fields[0].Binding); inputs[1] = input with { ExpectedReview = input.Review.Snapshot.Version }; break;
            case "type":
                var field = input.Review.Snapshot.Fields.Single(f => f.Key == p.ContextFields[0]);
                input.Review.Correct(field.Binding, "=1+2", field.Source!);
                input.Review.Verify(input.Review.Snapshot.Fields.Single(f => f.Key == field.Key).Binding);
                inputs[1] = input with { ExpectedReview = input.Review.Snapshot.Version }; break;
            case "destination": destination = destination with { SourceSha256 = "foreign" }; break;
            case "cancel": cancellation.Cancel(); break;
        }
        var before = JsonSerializer.Serialize(inputs.Where(i => i != null).Select(i => i.Review.Snapshot));
        void Run() => VerifiedPlacement.PreflightMany(session, destination, inputs, cancellation.Token);
        if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else if (scenario is "foreign" or "draft") Assert.ThrowsExactly<FieldReviewException>(Run);
        else Assert.ThrowsExactly<WorkbookException>(Run);
        Assert.AreEqual(0, session.Inspections);
        Assert.AreEqual(0, session.Reads);
        Assert.AreEqual(before, JsonSerializer.Serialize(inputs.Where(i => i != null).Select(i => i.Review.Snapshot)));
        Assert.IsFalse(session.Current.IsDirty);
    }
}
