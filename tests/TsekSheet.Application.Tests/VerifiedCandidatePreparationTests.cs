using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using ReadSession = TsekSheet.Application.Tests.VerifiedPlacementPreviewTests.Session;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedCandidatePreparationTests
{
    internal static VerifiedPlacementInput Input(WorkflowProfile p, string id = "NEW")
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('A', 64), 1, 2);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, "Other Tools Ltd", source), new(p.IdentifierFields[0], id, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "+00125.5000", source),
            new(p.ContextFields[1], "2024-02-29", source)]);
        foreach (var field in review.Snapshot.Fields.Where(f => f.Source != null))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == field.Key).Binding);
        return new(review, review.Snapshot.Version);
    }

    internal sealed class Context : IDisposable
    {
        internal readonly WorkflowProfile Profile;
        internal readonly Session Copy;
        internal readonly Session Parent;
        internal CandidateDestination Destination;
        internal Context(string profileId = "synthetic-payables")
        {
            Profile = WorkflowProfiles.Get(profileId);
            Copy = new(Profile);
            Parent = new(Profile);
            Parent.Inner.Current = Copy.Inner.Current with { SessionId = Guid.NewGuid() };
            Copy.Inner.Current = Copy.Inner.Current with { ParentSessionId = Parent.Inner.Current.SessionId };
            var info = Copy.Inner.Current;
            Copy.Working = new(info.SessionId, info.Worksheets[0].Id, 0, 0, false, []);
            MappingDestination Bind(Session s) => new(s.Inner.Current.SessionId, info.Worksheets[0].Id, 0,
                info.SourceSha256, Profile.Id, WorkflowProfiles.Hash(Profile));
            Destination = new(Bind(Copy), Bind(Parent), 0);
        }
        internal VerifiedCandidatePreview Prepare(IReadOnlyList<VerifiedPlacementInput> inputs, CancellationToken token = default)
            => VerifiedCandidatePreparation.Prepare(Copy, Parent, Destination, inputs, token);
        public void Dispose() { Copy.Dispose(); Parent.Dispose(); }
    }

    [TestMethod]
    [DataRow("synthetic-payables", 1)] [DataRow("synthetic-payables", 20)]
    [DataRow("synthetic-laboratory", 1)] [DataRow("synthetic-laboratory", 20)]
    public void CapturesOrderedTypedProvenanceAndActualReviewWithoutApproval(string profile, int count)
    {
        using var c = new Context(profile);
        var inputs = Enumerable.Range(0, count).Select(i => Input(c.Profile, "NEW-" + i)).ToList();
        var versions = inputs.Select(i => i.ExpectedReview).ToArray();
        var info = c.Copy.Inner.Current;
        c.Copy.AfterGeneration = () => inputs.Clear();
        var result = c.Prepare(inputs);
        Assert.IsFalse(result.CanApply);
        Assert.AreEqual(c.Destination, result.Destination);
        Assert.AreSame(c.Copy.Ticket, result.Candidate);
        Assert.IsTrue(result.Candidate.ReviewOnly);
        Assert.IsFalse(result.Candidate.Proposal.CanApply);
        Assert.AreEqual("generated", result.Candidate.Lifecycle.Generation);
        Assert.AreEqual("accepted", result.Candidate.Lifecycle.Oracle);
        Assert.AreEqual("not-approved", result.Candidate.Lifecycle.Approval);
        CollectionAssert.AreEqual(versions, result.Mappings.Select(m => m.Review).ToArray());
        CollectionAssert.AreEqual(versions.Select(v => v.ReviewId.ToString("N")).ToArray(),
            c.Copy.Request!.Records.Select(r => r.Id).ToArray());
        foreach (var mapping in result.Mappings)
        {
            Assert.AreEqual(125.5m, mapping.Fields[4].Value.DecimalValue);
            Assert.AreEqual(new DateOnly(2024, 2, 29), mapping.Fields[5].Value.DateValue);
            Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
            Assert.AreEqual(2, mapping.Fields[0].Value.Source!.PageNumber);
            Assert.IsFalse(mapping.CanApply);
        }
        Assert.AreEqual("=SUM(A1:A2)", c.Copy.Request.Records[0].Values[c.Profile.Fields[1].Key]);
        Assert.AreEqual(info, c.Copy.Inner.Current);
        Assert.IsEmpty(c.Copy.Working!.Changes);
        Assert.AreEqual(1, c.Copy.Generations);
        Assert.AreEqual(0, c.Copy.Cancellations.Count);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappingPreview>)result.Mappings).Clear());
    }

    [TestMethod]
    [DataRow("copy-session")] [DataRow("copy-source")] [DataRow("copy-revision")]
    [DataRow("parent-session")] [DataRow("parent-source")] [DataRow("parent-revision")]
    [DataRow("copy-profile")] [DataRow("parent-profile")] [DataRow("copy-sheet")]
    [DataRow("parent-sheet")] [DataRow("parent-is-copy")] [DataRow("not-copy")]
    [DataRow("parent-link")] [DataRow("working-session")] [DataRow("working-sheet")]
    [DataRow("working-revision")] [DataRow("base")] [DataRow("expected-base")]
    [DataRow("different-profile")]
    public void EveryExplicitBindingRejectsBeforeGeneration(string scenario)
    {
        using var c = new Context();
        var d = c.Destination;
        switch (scenario)
        {
            case "copy-session": d = d with { Copy = d.Copy with { SessionId = Guid.NewGuid() } }; break;
            case "copy-source": d = d with { Copy = d.Copy with { SourceSha256 = "foreign" } }; break;
            case "copy-revision": d = d with { Copy = d.Copy with { Revision = 1 } }; break;
            case "parent-session": d = d with { Parent = d.Parent with { SessionId = Guid.NewGuid() } }; break;
            case "parent-source": d = d with { Parent = d.Parent with { SourceSha256 = "foreign" } }; break;
            case "parent-revision": d = d with { Parent = d.Parent with { Revision = 1 } }; break;
            case "copy-profile": d = d with { Copy = d.Copy with { ProfileHash = "stale" } }; break;
            case "parent-profile": d = d with { Parent = d.Parent with { ProfileHash = "stale" } }; break;
            case "copy-sheet": d = d with { Copy = d.Copy with { WorksheetId = Guid.NewGuid() } }; break;
            case "parent-sheet": d = d with { Parent = d.Parent with { WorksheetId = c.Parent.Inner.Current.Worksheets[1].Id } }; break;
            case "parent-is-copy": c.Parent.Inner.Current = c.Parent.Inner.Current with { ParentSessionId = Guid.NewGuid() }; break;
            case "not-copy": c.Copy.Working = null; break;
            case "parent-link": c.Copy.Inner.Current = c.Copy.Inner.Current with { ParentSessionId = Guid.NewGuid() }; break;
            case "working-session": c.Copy.Working = c.Copy.Working! with { SessionId = Guid.NewGuid() }; break;
            case "working-sheet": c.Copy.Working = c.Copy.Working! with { SourceWorksheetId = Guid.NewGuid() }; break;
            case "working-revision": c.Copy.Working = c.Copy.Working! with { Revision = 1 }; break;
            case "base": c.Copy.Working = c.Copy.Working! with { BaseRevision = 1 }; break;
            case "expected-base": d = d with { BaseRevision = 1 }; break;
            case "different-profile":
                var other = WorkflowProfiles.Get("synthetic-laboratory");
                d = d with { Parent = d.Parent with { ProfileId = other.Id, ProfileHash = WorkflowProfiles.Hash(other) } }; break;
        }
        c.Destination = d;
        Assert.ThrowsExactly<WorkbookException>(() => c.Prepare([Input(c.Profile)]));
        Assert.AreEqual(0, c.Copy.Generations);
        Assert.AreEqual(0, c.Copy.Inner.Reads);
    }

    [TestMethod]
    [DataRow("empty")] [DataRow("too-many")] [DataRow("null-input")] [DataRow("repeated")]
    [DataRow("draft")] [DataRow("stale")] [DataRow("foreign")] [DataRow("wrong-profile")]
    [DataRow("invalid-type")] [DataRow("cancelled")]
    public void InvalidRecordsRejectBeforeGeneration(string scenario)
    {
        using var c = new Context();
        var input = Input(c.Profile);
        using var cts = new CancellationTokenSource();
        var inputs = new[] { input };
        switch (scenario)
        {
            case "empty": inputs = []; break;
            case "too-many": inputs = Enumerable.Range(0, 21).Select(i => Input(c.Profile, "NEW-" + i)).ToArray(); break;
            case "null-input": inputs = [null!]; break;
            case "repeated": inputs = [input, input]; break;
            case "draft": input.Review.Retract(input.Review.Snapshot.Fields[0].Binding); inputs = [input with { ExpectedReview = input.Review.Snapshot.Version }]; break;
            case "stale": input.Review.Retract(input.Review.Snapshot.Fields[0].Binding); break;
            case "foreign": inputs = [input with { ExpectedReview = Input(c.Profile).ExpectedReview }]; break;
            case "wrong-profile": inputs = [Input(WorkflowProfiles.Get("synthetic-laboratory"))]; break;
            case "invalid-type":
                input.Review.Correct(input.Review.Snapshot.Fields[4].Binding, "=1+2", input.Review.Snapshot.Fields[4].Source!);
                input.Review.Verify(input.Review.Snapshot.Fields[4].Binding);
                inputs = [input with { ExpectedReview = input.Review.Snapshot.Version }]; break;
            case "cancelled": cts.Cancel(); break;
        }
        if (scenario == "cancelled") Assert.ThrowsExactly<OperationCanceledException>(() => c.Prepare(inputs, cts.Token));
        else if (scenario is "draft" or "stale" or "foreign")
            Assert.AreEqual(scenario == "draft" ? "REVIEW_INCOMPLETE" : "STALE_RECORD_REVIEW",
                Assert.ThrowsExactly<FieldReviewException>(() => c.Prepare(inputs)).Code);
        else Assert.AreEqual(scenario == "wrong-profile" ? "STALE_PROFILE" : "INVALID_RECORD",
            Assert.ThrowsExactly<WorkbookException>(() => c.Prepare(inputs)).Code);
        Assert.AreEqual(0, c.Copy.Generations);
        Assert.IsNull(c.Copy.Ticket);
    }

    [TestMethod]
    [DataRow(0, false)] [DataRow(1, false)] [DataRow(2, false)]
    [DataRow(0, true)] [DataRow(1, true)] [DataRow(2, true)]
    public void EveryReviewIsRecheckedAndGeneratedTicketIsDiscarded(int index, bool retract)
    {
        using var c = new Context();
        var inputs = Enumerable.Range(0, 3).Select(i => Input(c.Profile, "NEW-" + i)).ToArray();
        c.Copy.AfterGeneration = () =>
        {
            var review = inputs[index].Review;
            var field = review.Snapshot.Fields[0];
            if (retract) review.Retract(field.Binding);
            else review.Correct(field.Binding, field.Value, field.Source!);
        };
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() => c.Prepare(inputs)).Code);
        Assert.AreEqual(c.Copy.GeneratedId, c.Copy.Cancellations.Single().Id);
        Assert.IsNull(c.Copy.Ticket);
        Assert.AreEqual(0, c.Copy.Inner.Current.Revision);
        Assert.AreEqual(0, c.Parent.Inner.Current.Revision);
    }

    [TestMethod]
    [DataRow("copy-revision")] [DataRow("parent-revision")] [DataRow("copy-source")]
    [DataRow("parent-source")] [DataRow("parent-link")] [DataRow("base")]
    [DataRow("cancel")] [DataRow("final-review")] [DataRow("final-cancel")]
    public void PostGenerationChangesAndFinalReadCancellationDiscardExactTicket(string scenario)
    {
        using var c = new Context();
        var input = Input(c.Profile);
        using var cts = new CancellationTokenSource();
        c.Copy.AfterGeneration = () =>
        {
            switch (scenario)
            {
                case "copy-revision": c.Copy.Inner.Current = c.Copy.Inner.Current with { Revision = 1 }; break;
                case "parent-revision": c.Parent.Inner.Current = c.Parent.Inner.Current with { Revision = 1 }; break;
                case "copy-source": c.Copy.Inner.Current = c.Copy.Inner.Current with { SourceSha256 = "changed" }; break;
                case "parent-source": c.Parent.Inner.Current = c.Parent.Inner.Current with { SourceSha256 = "changed" }; break;
                case "parent-link": c.Copy.Inner.Current = c.Copy.Inner.Current with { ParentSessionId = Guid.NewGuid() }; break;
                case "base": c.Copy.Working = c.Copy.Working! with { BaseRevision = 1 }; break;
                case "cancel": cts.Cancel(); break;
                default:
                    c.Parent.Inner.BeforeInfo = () =>
                    {
                        c.Parent.Inner.BeforeInfo = null;
                        if (scenario == "final-cancel") cts.Cancel();
                        else input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
                    }; break;
            }
        };
        if (scenario is "cancel" or "final-cancel") Assert.ThrowsExactly<OperationCanceledException>(() => c.Prepare([input], cts.Token));
        else if (scenario == "final-review") Assert.ThrowsExactly<FieldReviewException>(() => c.Prepare([input], cts.Token));
        else Assert.ThrowsExactly<WorkbookException>(() => c.Prepare([input], cts.Token));
        var cancelled = c.Copy.Cancellations.Single();
        Assert.AreEqual(c.Copy.GeneratedId, cancelled.Id);
        Assert.AreEqual(scenario == "copy-revision" ? 1 : 0, cancelled.Revision);
        Assert.IsNull(c.Copy.Ticket);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CleanupNeverDiscardsReplacementAndReportsUnexpectedFailure(bool cleanupFails)
    {
        using var c = new Context();
        var input = Input(c.Profile);
        Guid replacement = Guid.NewGuid();
        c.Copy.AfterGeneration = () =>
        {
            input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
            if (cleanupFails) c.Copy.CleanupFailure = new IOException("Synthetic cleanup failure");
            else c.Copy.Ticket = c.Copy.Ticket! with { CandidateId = replacement };
        };
        if (cleanupFails)
        {
            var failure = Assert.ThrowsExactly<AggregateException>(() => c.Prepare([input]));
            Assert.IsInstanceOfType<FieldReviewException>(failure.InnerExceptions[0]);
            Assert.IsInstanceOfType<IOException>(failure.InnerExceptions[1]);
        }
        else
        {
            var failure = Assert.ThrowsExactly<AggregateException>(() => c.Prepare([input]));
            Assert.IsInstanceOfType<FieldReviewException>(failure.InnerExceptions[0]);
            Assert.AreEqual("STALE_REVIEW", ((WorkbookException)failure.InnerExceptions[1]).Code);
            Assert.AreEqual(replacement, c.Copy.Ticket!.CandidateId);
        }
        Assert.AreEqual(c.Copy.GeneratedId, c.Copy.Cancellations.Single().Id);
    }

    internal sealed class Session : IWorkbookSession
    {
        internal readonly ReadSession Inner;
        internal WorkingCopyInfo? Working;
        internal WorkflowPlanRequest? Request;
        internal StructuralCandidateReview? Ticket;
        internal Guid GeneratedId;
        internal int Generations;
        internal Action? AfterGeneration;
        internal Exception? CleanupFailure;
        internal Action? AfterApproval;
        internal Exception? ApprovalFailure;
        internal int Approvals;
        internal CandidateReviewApproval? Approval;
        internal Func<StructuralCandidateReview, StructuralCandidateReview>? ApprovalResponse;
        internal readonly List<(Guid Id, int Revision)> Cancellations = [];
        internal Session(WorkflowProfile p) { Inner = new(p); Inner.Inspect = (plan, _) => plan with { StructuralStatus = "experimental-envelope-only" }; }
        public WorkbookInfo Info => Inner.Info;
        public WorkingCopyInfo? WorkingCopy => Working;
        public CellViewport ReadViewport(Guid id, int row, int column, int rows, int columns) => Inner.ReadViewport(id, row, column, rows, columns);
        public WorkflowPlan InspectStructuralInsertion(WorkflowPlan plan, CancellationToken token = default) => Inner.InspectStructuralInsertion(plan, token);
        public StructuralCandidateReview PrepareCandidateReview(IWorkbookSession destination, WorkflowPlanRequest request, int expectedDestinationRevision, CancellationToken token = default)
        {
            Generations++;
            Request = request;
            var plan = WorkflowPlanning.Prepare(this, request, token);
            GeneratedId = Guid.NewGuid();
            Ticket = new(GeneratedId, "actual-review", "actual-candidate", "actual-manifest", Info.DisplayName,
                Info.Worksheets[0].Name, plan, new("resolved", "eligible-experimental", "generated", "accepted", "not-approved", "not-adopted", "not-integrated", "not-exported"),
                [], [], new(8, request.Records.Count, 6, 6, 10, "Synthetic policy"), [], 1, 1)
                { ReviewOnly = true, DestinationSessionId = destination.Info.SessionId, ExpectedDestinationRevision = expectedDestinationRevision,
                    OracleReport = new(true, ["Synthetic allowed change"], []) };
            var generated = Ticket;
            AfterGeneration?.Invoke();
            return generated;
        }
        public void CancelCandidateReview(Guid candidateId, int expectedRevision)
        {
            Cancellations.Add((candidateId, expectedRevision));
            if (CleanupFailure != null) throw CleanupFailure;
            if (Ticket?.CandidateId != candidateId || Inner.Current.Revision != expectedRevision)
                throw new WorkbookException("STALE_REVIEW", "Superseded ticket");
            Ticket = null;
        }
        public StructuralCandidateReview ApproveCandidateReview(IWorkbookSession parent, CandidateReviewApproval request, CancellationToken token = default)
        {
            Approvals++;
            Approval = request;
            token.ThrowIfCancellationRequested();
            if (ApprovalFailure != null) throw ApprovalFailure;
            if (Ticket?.CandidateId != request.CandidateId || Ticket.ReviewSha256 != request.ReviewSha256)
                throw new WorkbookException("STALE_REVIEW", "Superseded ticket");
            Ticket = Ticket with { Lifecycle = Ticket.Lifecycle with { Approval = "approved" } };
            var result = Ticket;
            AfterApproval?.Invoke();
            return ApprovalResponse?.Invoke(result) ?? result;
        }
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected save");
        public void Dispose() => Inner.Dispose();
    }
}
