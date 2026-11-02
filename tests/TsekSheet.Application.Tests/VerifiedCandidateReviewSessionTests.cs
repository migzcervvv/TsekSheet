using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using Context = TsekSheet.Application.Tests.VerifiedCandidatePreparationTests.Context;
using static TsekSheet.Application.Tests.VerifiedCandidatePreparationTests;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class VerifiedCandidateReviewSessionTests
{
    internal static CandidateReviewApproval Command(VerifiedCandidatePreview preview, bool duplicates = false)
    {
        var d = preview.Destination;
        var r = preview.Candidate;
        return new(d.Copy.SessionId, d.Parent.SessionId, d.Copy.WorksheetId, r.CandidateId,
            r.ReviewSha256, r.CandidateSha256, r.ManifestSha256, d.Copy.SourceSha256,
            d.Copy.Revision, d.Parent.Revision, d.Copy.ProfileId, d.Copy.ProfileHash, duplicates);
    }

    [TestMethod]
    [DataRow("synthetic-payables", 1)] [DataRow("synthetic-payables", 20)]
    [DataRow("synthetic-laboratory", 1)] [DataRow("synthetic-laboratory", 20)]
    public void DeliberateExactApprovalRetainsOrderedProvenanceAndChangesOnlyMetadata(string profile, int count)
    {
        using var c = new Context(profile);
        var inputs = Enumerable.Range(0, count).Select(i => Input(c.Profile, "NEW-" + i)).ToList();
        var versions = inputs.Select(i => i.ExpectedReview).ToArray();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, inputs);
        var before = session.Snapshot;
        inputs.Clear();
        var command = Command(before.Preview);
        var approved = session.Approve(command);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Pending, before.Status);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Approved, approved.Status);
        Assert.AreEqual("not-approved", before.Preview.Candidate.Lifecycle.Approval);
        Assert.AreEqual("approved", approved.Preview.Candidate.Lifecycle.Approval);
        Assert.AreEqual(before.Preview.Candidate.Lifecycle with { Approval = "approved" }, approved.Preview.Candidate.Lifecycle);
        Assert.AreEqual(before.Preview.Candidate.CandidateId, approved.Preview.Candidate.CandidateId);
        Assert.AreEqual(command, c.Copy.Approval);
        Assert.IsFalse(approved.CanApply);
        Assert.IsFalse(approved.Preview.CanApply);
        Assert.IsFalse(approved.Preview.Candidate.Proposal.CanApply);
        Assert.IsTrue(approved.Preview.Candidate.ReviewOnly);
        CollectionAssert.AreEqual(versions, approved.Preview.Mappings.Select(m => m.Review).ToArray());
        Assert.AreEqual(" =SUM(A1:A2) ", approved.Preview.Mappings[0].Fields[1].Value.LiteralText);
        Assert.AreEqual(125.5m, approved.Preview.Mappings[0].Fields[4].Value.DecimalValue);
        Assert.AreEqual(0, c.Copy.Inner.Current.Revision);
        Assert.AreEqual(0, c.Parent.Inner.Current.Revision);
        Assert.IsEmpty(c.Copy.Working!.Changes);
        Assert.AreEqual("APPROVAL_REPLAY", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
        Assert.AreEqual(1, c.Copy.Approvals);
        session.Discard();
        Assert.AreEqual(VerifiedCandidateReviewStatus.Discarded, session.Snapshot.Status);
        Assert.IsNull(c.Copy.Ticket);
        Assert.AreEqual("STALE_REVIEW", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
        Assert.ThrowsExactly<WorkbookException>(() => session.Discard());
    }

    [TestMethod]
    [DataRow("copy")] [DataRow("parent")] [DataRow("sheet")] [DataRow("candidate")]
    [DataRow("review-hash")] [DataRow("candidate-hash")] [DataRow("manifest")]
    [DataRow("source")] [DataRow("copy-revision")] [DataRow("parent-revision")]
    [DataRow("profile")] [DataRow("profile-hash")] [DataRow("null")]
    public void ForeignOrTamperedCommandsNeverReachEngineOrDiscardOwnedTicket(string field)
    {
        using var c = new Context();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [Input(c.Profile)]);
        var exact = Command(session.Snapshot.Preview);
        var bad = field switch
        {
            "copy" => exact with { SessionId = Guid.NewGuid() },
            "parent" => exact with { DestinationSessionId = Guid.NewGuid() },
            "sheet" => exact with { WorksheetId = Guid.NewGuid() },
            "candidate" => exact with { CandidateId = Guid.NewGuid() },
            "review-hash" => exact with { ReviewSha256 = "changed" },
            "candidate-hash" => exact with { CandidateSha256 = "changed" },
            "manifest" => exact with { ManifestSha256 = "changed" },
            "source" => exact with { SourceSha256 = "changed" },
            "copy-revision" => exact with { ExpectedCopyRevision = 1 },
            "parent-revision" => exact with { ExpectedDestinationRevision = 1 },
            "profile" => exact with { ProfileId = "synthetic-laboratory" },
            "profile-hash" => exact with { ProfileHash = "changed" },
            _ => null!
        };
        Assert.AreEqual("INVALID_REVIEW", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(bad)).Code);
        Assert.AreEqual(0, c.Copy.Approvals);
        Assert.IsEmpty(c.Copy.Cancellations);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Pending, session.Snapshot.Status);
        session.Approve(exact);
    }

    [TestMethod]
    public void SnapshotsAndEngineResponsesCannotMutateRetainedTicket()
    {
        using var c = new Context();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [Input(c.Profile)]);
        var snapshot = session.Snapshot.Preview;
        var command = Command(snapshot);
        ((IList<string>)snapshot.Candidate.FormulaImpacts).Clear();
        ((IList<string>)snapshot.Candidate.RejectionReasons).Add("forged");
        ((IList<RecordInsertionProposal>)snapshot.Candidate.Proposal.Records).Clear();
        var approved = session.Approve(command);
        Assert.HasCount(1, approved.Preview.Candidate.Proposal.Records);
        Assert.IsEmpty(approved.Preview.Candidate.RejectionReasons);
        c.Copy.Ticket = c.Copy.Ticket! with { CandidateSha256 = "changed" };
        Assert.AreEqual(command.CandidateSha256, session.Snapshot.Preview.Candidate.CandidateSha256);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DuplicateAndFormattingGatesNeverSynthesizeApproval(bool formatting)
    {
        using var c = new Context();
        c.Copy.AfterGeneration = () => { };
        var input = Input(c.Profile, formatting ? "NEW" : "OLD-6");
        // Install findings in the generated response, before the owning session captures it.
        var wrapper = new FindingsSession(c.Copy, formatting);
        var session = VerifiedCandidateReviewSession.Prepare(wrapper, c.Parent, c.Destination, [input]);
        var command = Command(session.Snapshot.Preview);
        Assert.AreEqual(formatting ? "NATIVE_FORMATTING_UNVERIFIED" : "REVIEW_REQUIRED",
            Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
        Assert.AreEqual(0, c.Copy.Approvals);
        Assert.IsEmpty(c.Copy.Cancellations);
        if (!formatting) session.Approve(command with { DuplicatesReviewed = true });
        else Assert.AreEqual("NATIVE_FORMATTING_UNVERIFIED", Assert.ThrowsExactly<WorkbookException>(() =>
            session.Approve(command with { DuplicatesReviewed = true })).Code);
    }

    [TestMethod]
    [DataRow(0, false, false)] [DataRow(1, false, false)] [DataRow(2, false, false)]
    [DataRow(0, true, false)] [DataRow(1, true, false)] [DataRow(2, true, false)]
    [DataRow(0, false, true)] [DataRow(1, false, true)] [DataRow(2, false, true)]
    [DataRow(0, true, true)] [DataRow(1, true, true)] [DataRow(2, true, true)]
    public void EveryLiveReviewInvalidatesBeforeOrDuringApproval(int index, bool during, bool retract)
    {
        using var c = new Context();
        var inputs = Enumerable.Range(0, 3).Select(i => Input(c.Profile, "NEW-" + i)).ToArray();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, inputs);
        void Change()
        {
            var review = inputs[index].Review;
            var field = review.Snapshot.Fields[0];
            if (retract) review.Retract(field.Binding);
            else { review.Correct(field.Binding, field.Value, field.Source!); review.Verify(review.Snapshot.Fields[0].Binding); }
        }
        if (during) c.Copy.AfterApproval = Change; else Change();
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() => session.Approve(Command(session.Snapshot.Preview))).Code);
        Assert.AreEqual(during ? 1 : 0, c.Copy.Approvals);
        Assert.AreEqual(c.Copy.GeneratedId, c.Copy.Cancellations.Single().Id);
        Assert.IsNull(c.Copy.Ticket);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Discarded, session.Snapshot.Status);
    }

    [TestMethod]
    [DataRow("copy-revision", false)] [DataRow("parent-revision", false)] [DataRow("base", false)]
    [DataRow("copy-source", false)] [DataRow("parent-source", false)] [DataRow("parent-link", false)]
    [DataRow("cancel", false)] [DataRow("copy-revision", true)] [DataRow("parent-revision", true)]
    [DataRow("base", true)] [DataRow("copy-source", true)] [DataRow("parent-source", true)]
    [DataRow("parent-link", true)] [DataRow("cancel", true)] [DataRow("final-review", true)] [DataRow("final-cancel", true)]
    public void AuthorityChangesAndCancellationDiscardAtCurrentRevision(string scenario, bool during)
    {
        using var c = new Context();
        using var cts = new CancellationTokenSource();
        var input = Input(c.Profile);
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [input]);
        void Change()
        {
            switch (scenario)
            {
                case "copy-revision": c.Copy.Inner.Current = c.Copy.Inner.Current with { Revision = 1 }; break;
                case "parent-revision": c.Parent.Inner.Current = c.Parent.Inner.Current with { Revision = 1 }; break;
                case "base": c.Copy.Working = c.Copy.Working! with { BaseRevision = 1 }; break;
                case "copy-source": c.Copy.Inner.Current = c.Copy.Inner.Current with { SourceSha256 = "changed" }; break;
                case "parent-source": c.Parent.Inner.Current = c.Parent.Inner.Current with { SourceSha256 = "changed" }; break;
                case "parent-link": c.Copy.Inner.Current = c.Copy.Inner.Current with { ParentSessionId = Guid.NewGuid() }; break;
                case "cancel": cts.Cancel(); break;
                default:
                    c.Parent.Inner.BeforeInfo = () =>
                    {
                        c.Parent.Inner.BeforeInfo = null;
                        if (scenario == "final-cancel") cts.Cancel();
                        else input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
                    }; break;
            }
        }
        if (during) c.Copy.AfterApproval = Change; else Change();
        void Approve() => session.Approve(Command(session.Snapshot.Preview), cts.Token);
        if (scenario.Contains("cancel")) Assert.ThrowsExactly<OperationCanceledException>(Approve);
        else if (scenario == "final-review") Assert.ThrowsExactly<FieldReviewException>(Approve);
        else Assert.ThrowsExactly<WorkbookException>(Approve);
        Assert.AreEqual(during ? 1 : 0, c.Copy.Approvals);
        Assert.AreEqual(c.Copy.GeneratedId, c.Copy.Cancellations.Single().Id);
        Assert.AreEqual(scenario == "copy-revision" ? 1 : 0, c.Copy.Cancellations.Single().Revision);
        Assert.IsNull(c.Copy.Ticket);
    }

    [TestMethod]
    [DataRow("replacement")] [DataRow("cleanup")] [DataRow("engine-failure")]
    [DataRow("response-ticket")] [DataRow("response-apply")] [DataRow("response-adoption")]
    public void EngineFailureUnexpectedResponseAndCleanupFailureAreExplicit(string scenario)
    {
        using var c = new Context();
        var input = Input(c.Profile);
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [input]);
        var command = Command(session.Snapshot.Preview);
        var replacement = Guid.NewGuid();
        if (scenario == "engine-failure") c.Copy.ApprovalFailure = new IOException("Synthetic approval failure");
        if (scenario.StartsWith("response")) c.Copy.ApprovalResponse = r => scenario switch
        {
            "response-ticket" => r with { CandidateId = replacement },
            "response-apply" => r with { Proposal = r.Proposal with { CanApply = true } },
            _ => r with { Lifecycle = r.Lifecycle with { WorkingCopyAdoption = "adopted" } }
        };
        if (scenario is "replacement" or "cleanup") c.Copy.AfterApproval = () =>
        {
            input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
            if (scenario == "replacement") c.Copy.Ticket = c.Copy.Ticket! with { CandidateId = replacement };
            else c.Copy.CleanupFailure = new IOException("Synthetic cleanup failure");
        };
        if (scenario is "replacement" or "cleanup")
        {
            var failure = Assert.ThrowsExactly<AggregateException>(() => session.Approve(command));
            Assert.IsInstanceOfType<FieldReviewException>(failure.InnerExceptions[0]);
            if (scenario == "replacement")
            {
                Assert.AreEqual("STALE_REVIEW", ((WorkbookException)failure.InnerExceptions[1]).Code);
                Assert.AreEqual(replacement, c.Copy.Ticket!.CandidateId);
            }
            else Assert.IsInstanceOfType<IOException>(failure.InnerExceptions[1]);
        }
        else if (scenario == "engine-failure") Assert.ThrowsExactly<IOException>(() => session.Approve(command));
        else Assert.AreEqual("INVALID_REVIEW", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
        Assert.AreEqual(command.CandidateId, c.Copy.Cancellations.Single().Id);
        Assert.AreEqual(scenario is "replacement" or "cleanup" ? VerifiedCandidateReviewStatus.Invalidated
            : VerifiedCandidateReviewStatus.Discarded, session.Snapshot.Status);
    }

    [TestMethod]
    public void ConcurrentApprovalCommandsAreSerializedAndReplayRejected()
    {
        using var c = new Context();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [Input(c.Profile)]);
        var command = Command(session.Snapshot.Preview);
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        Parallel.For(0, 4, _ => { try { session.Approve(command); } catch (WorkbookException e) { errors.Add(e.Code); } });
        Assert.AreEqual(1, c.Copy.Approvals);
        Assert.HasCount(3, errors);
        Assert.IsTrue(errors.All(e => e == "APPROVAL_REPLAY"));
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void DiscardFailureAndSupersededTicketNeverClaimSuccessfulCleanup(bool replacement)
    {
        using var c = new Context();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [Input(c.Profile)]);
        var command = Command(session.Snapshot.Preview);
        var nextId = Guid.NewGuid();
        if (replacement) c.Copy.Ticket = c.Copy.Ticket! with { CandidateId = nextId };
        else c.Copy.CleanupFailure = new IOException("Synthetic cleanup failure");
        if (replacement) Assert.AreEqual("STALE_REVIEW", Assert.ThrowsExactly<WorkbookException>(() => session.Discard()).Code);
        else Assert.ThrowsExactly<IOException>(() => session.Discard());
        Assert.AreEqual(VerifiedCandidateReviewStatus.Invalidated, session.Snapshot.Status);
        Assert.AreEqual(command.CandidateId, c.Copy.Cancellations.Single().Id);
        Assert.AreEqual(replacement ? nextId : command.CandidateId, c.Copy.Ticket!.CandidateId);
        Assert.AreEqual("STALE_REVIEW", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
    }

    [TestMethod]
    public void StaleLiveReviewAfterApprovalInvalidatesRatherThanReportingReplay()
    {
        using var c = new Context();
        var input = Input(c.Profile);
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [input]);
        var command = Command(session.Snapshot.Preview);
        session.Approve(command);
        input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
        Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() => session.Approve(command)).Code);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Discarded, session.Snapshot.Status);
        Assert.AreEqual("approved", session.Snapshot.Preview.Candidate.Lifecycle.Approval);
        Assert.IsNull(c.Copy.Ticket);
        Assert.AreEqual(1, c.Copy.Approvals);
    }

    [TestMethod]
    public void SupersededEngineTicketCannotBeApprovedAndRemainsUntouchedByCleanup()
    {
        using var c = new Context();
        var session = VerifiedCandidateReviewSession.Prepare(c.Copy, c.Parent, c.Destination, [Input(c.Profile)]);
        var command = Command(session.Snapshot.Preview);
        var replacementId = Guid.NewGuid();
        c.Copy.Ticket = c.Copy.Ticket! with { CandidateId = replacementId };
        var failure = Assert.ThrowsExactly<AggregateException>(() => session.Approve(command));
        Assert.AreEqual("STALE_REVIEW", ((WorkbookException)failure.InnerExceptions[0]).Code);
        Assert.AreEqual("STALE_REVIEW", ((WorkbookException)failure.InnerExceptions[1]).Code);
        Assert.AreEqual(replacementId, c.Copy.Ticket!.CandidateId);
        Assert.AreEqual(VerifiedCandidateReviewStatus.Invalidated, session.Snapshot.Status);
    }

    private sealed class FindingsSession(Session inner, bool formatting) : IWorkbookSession
    {
        public WorkbookInfo Info => inner.Info;
        public WorkingCopyInfo? WorkingCopy => inner.WorkingCopy;
        public CellViewport ReadViewport(Guid id, int row, int column, int rows, int columns) => inner.ReadViewport(id, row, column, rows, columns);
        public StructuralCandidateReview PrepareCandidateReview(IWorkbookSession parent, WorkflowPlanRequest request, int revision, CancellationToken token = default)
        {
            var review = inner.PrepareCandidateReview(parent, request, revision, token);
            if (formatting) review = review with { RejectionReasons = ["Unqualified formatting"] };
            else
            {
                var records = review.Proposal.Records.ToArray();
                records[0] = records[0] with { Duplicates = [new(6, null, null, "Synthetic duplicate")] };
                review = review with { Proposal = review.Proposal with { Records = records } };
            }
            inner.Ticket = review;
            return review;
        }
        public StructuralCandidateReview ApproveCandidateReview(IWorkbookSession parent, CandidateReviewApproval request, CancellationToken token = default) => inner.ApproveCandidateReview(parent, request, token);
        public void CancelCandidateReview(Guid id, int revision) => inner.CancelCandidateReview(id, revision);
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected save");
        public void Dispose() { }
    }
}
