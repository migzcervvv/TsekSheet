using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using static TsekSheet.Workbooks.Tests.VerifiedCandidateWorkbookTests;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class VerifiedCandidateReviewWorkbookTests
{
    private static CandidateReviewApproval Command(VerifiedCandidatePreview preview, bool duplicates = false)
    {
        var d = preview.Destination;
        var r = preview.Candidate;
        return new(d.Copy.SessionId, d.Parent.SessionId, d.Copy.WorksheetId, r.CandidateId,
            r.ReviewSha256, r.CandidateSha256, r.ManifestSha256, d.Copy.SourceSha256,
            d.Copy.Revision, d.Parent.Revision, d.Copy.ProfileId, d.Copy.ProfileHash, duplicates);
    }

    [TestMethod]
    [DataRow("synthetic-payables", 0, 1)] [DataRow("synthetic-laboratory", 0, 1)]
    [DataRow("synthetic-payables", 20, 1)] [DataRow("synthetic-laboratory", 20, 1)]
    [DataRow("synthetic-payables", 0, 20)] [DataRow("synthetic-laboratory", 0, 20)]
    [DataRow("synthetic-payables", 20, 20)] [DataRow("synthetic-laboratory", 20, 20)]
    public void QualifiedApprovalIsMetadataOnlyWithExactHashesAndOrderedLiveProvenance(string profileId, int shift, int count)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p, shift);
        var hash = Hash(fixture.Path);
        var engine = new WorkbookEngine();
        using (var parent = engine.Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        using (var sibling = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            var inputs = Enumerable.Range(0, count).Select(i => Input(p, "NEW-" + i)).ToArray();
            var reviews = JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot));
            var sessions = new[] { parent, copy, sibling };
            var before = sessions.Select(s => (s.Info, Cells: Cells(s))).ToArray();
            var lifecycle = VerifiedCandidateReviewSession.Prepare(copy, parent, Bind(copy, parent, p), inputs);
            var preview = lifecycle.Snapshot.Preview;
            var command = Command(preview);
            Assert.AreEqual("INVALID_REVIEW", Assert.ThrowsExactly<WorkbookException>(() =>
                lifecycle.Approve(command with { ManifestSha256 = "foreign" })).Code);
            Assert.AreEqual("not-approved", ((WorkbookSession)copy).CurrentStructuralReview!.Lifecycle.Approval);
            var approved = lifecycle.Approve(command);
            var actual = ((WorkbookSession)copy).CurrentStructuralReview!;
            Assert.AreEqual(JsonSerializer.Serialize(preview.Candidate with
            { Lifecycle = preview.Candidate.Lifecycle with { Approval = "approved" } }), JsonSerializer.Serialize(actual));
            Assert.AreEqual(JsonSerializer.Serialize(actual), JsonSerializer.Serialize(approved.Preview.Candidate));
            Assert.AreEqual(VerifiedCandidateReviewStatus.Approved, approved.Status);
            Assert.IsFalse(approved.CanApply);
            Assert.IsFalse(approved.Preview.Candidate.Proposal.CanApply);
            Assert.IsTrue(actual.ReviewOnly);
            Assert.IsTrue(actual.OracleReport!.Accepted);
            CollectionAssert.AreEqual(inputs.Select(i => i.ExpectedReview).ToArray(), approved.Preview.Mappings.Select(m => m.Review).ToArray());
            Assert.AreEqual(shift == 0 ? 1 : 6, approved.Preview.Mappings[0].Fields[0].Column);
            Assert.AreEqual(" =SUM(A1:A2) ", approved.Preview.Mappings[0].Fields[1].Value.LiteralText);
            Assert.AreEqual(123.45m, approved.Preview.Mappings[0].Fields[4].Value.DecimalValue);
            Assert.AreEqual(inputs[0].Review.Snapshot.Fields[0].Source, approved.Preview.Mappings[0].Fields[0].Value.Source);
            Assert.AreEqual("APPROVAL_REPLAY", Assert.ThrowsExactly<WorkbookException>(() => lifecycle.Approve(command)).Code);
            engine.StructuralExperimentsEnabled = true;
            engine.StructuralIntegrationExperimentsEnabled = true;
            Assert.AreEqual("PRODUCTION_APPLY_DISABLED", Assert.ThrowsExactly<WorkbookException>(() =>
                ((WorkbookSession)copy).AdoptStructuralReview(new(actual.CandidateId, actual.ReviewSha256, actual.CandidateSha256, 0, true))).Code);
            Assert.AreEqual("ROW_INSERTION_DISABLED", Assert.ThrowsExactly<WorkbookException>(() =>
                copy.InsertWorkflowRows(actual.Proposal with { CanApply = true }, true, true)).Code);
            for (var i = 0; i < sessions.Length; i++)
            {
                Assert.AreEqual(before[i].Info, sessions[i].Info);
                Assert.AreEqual(before[i].Cells, Cells(sessions[i]));
            }
            Assert.IsEmpty(copy.WorkingCopy!.Changes);
            Assert.IsEmpty(sibling.WorkingCopy!.Changes);
            Assert.IsNull(((WorkbookSession)sibling).CurrentStructuralReview);
            Assert.AreEqual(reviews, JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot)));
            lifecycle.Discard();
            Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            var retry = VerifiedCandidateReviewSession.Prepare(copy, parent, Bind(copy, parent, p), inputs);
            Assert.AreNotEqual(actual.CandidateId, retry.Snapshot.Preview.Candidate.CandidateId);
            retry.Discard();
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", false)] [DataRow("synthetic-laboratory", false)]
    [DataRow("synthetic-payables", true)] [DataRow("synthetic-laboratory", true)]
    public void ExistingAndBatchDuplicatesRequireAcknowledgmentForThisExactTicket(string profileId, bool batch)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            var cells = Cells(copy);
            var inputs = batch ? new[] { Input(p, "NEW"), Input(p, "NEW") } : [Input(p, "OLD-6")];
            var session = VerifiedCandidateReviewSession.Prepare(copy, parent, Bind(copy, parent, p), inputs);
            Assert.IsTrue(session.Snapshot.Preview.Candidate.Proposal.Records.All(r => r.Duplicates.Count != 0));
            var command = Command(session.Snapshot.Preview);
            Assert.AreEqual("REVIEW_REQUIRED", Assert.ThrowsExactly<WorkbookException>(() => session.Approve(command)).Code);
            Assert.AreEqual("not-approved", ((WorkbookSession)copy).CurrentStructuralReview!.Lifecycle.Approval);
            session.Approve(command with { DuplicatesReviewed = true });
            Assert.AreEqual("approved", ((WorkbookSession)copy).CurrentStructuralReview!.Lifecycle.Approval);
            Assert.AreEqual(cells, Cells(copy));
            Assert.AreEqual(0, copy.Info.Revision);
            Assert.AreEqual(0, parent.Info.Revision);
            Assert.IsEmpty(copy.WorkingCopy!.Changes);
            session.Discard();
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", 20.0)] [DataRow("synthetic-laboratory", 20.0)]
    [DataRow("synthetic-payables", 22.0)] [DataRow("synthetic-laboratory", 22.0)]
    [DataRow("synthetic-payables", 21.95)] [DataRow("synthetic-laboratory", 21.95)]
    [DataRow("synthetic-payables", 24.0)] [DataRow("synthetic-laboratory", 24.0)]
    public void ExactNativeFormattingGateCannotBeAcknowledgedAway(string profileId, double height)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p, height: height);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            var cells = Cells(copy);
            var session = VerifiedCandidateReviewSession.Prepare(copy, parent, Bind(copy, parent, p), [Input(p)]);
            Assert.IsTrue(session.Snapshot.Preview.Candidate.OracleReport!.Accepted);
            Assert.IsNotEmpty(session.Snapshot.Preview.Candidate.RejectionReasons);
            Assert.AreEqual("NATIVE_FORMATTING_UNVERIFIED", Assert.ThrowsExactly<WorkbookException>(() =>
                session.Approve(Command(session.Snapshot.Preview, true))).Code);
            Assert.AreEqual("not-approved", ((WorkbookSession)copy).CurrentStructuralReview!.Lifecycle.Approval);
            Assert.AreEqual(cells, Cells(copy));
            Assert.AreEqual(0, copy.Info.Revision);
            Assert.AreEqual(0, parent.Info.Revision);
            session.Discard();
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "before-review")] [DataRow("synthetic-laboratory", "before-review")]
    [DataRow("synthetic-payables", "before-copy")] [DataRow("synthetic-laboratory", "before-copy")]
    [DataRow("synthetic-payables", "before-parent")] [DataRow("synthetic-laboratory", "before-parent")]
    [DataRow("synthetic-payables", "before-cancel")] [DataRow("synthetic-laboratory", "before-cancel")]
    [DataRow("synthetic-payables", "during-review")] [DataRow("synthetic-laboratory", "during-review")]
    [DataRow("synthetic-payables", "during-cancel")] [DataRow("synthetic-laboratory", "during-cancel")]
    [DataRow("synthetic-payables", "after-review")] [DataRow("synthetic-laboratory", "after-review")]
    [DataRow("synthetic-payables", "after-copy")] [DataRow("synthetic-laboratory", "after-copy")]
    [DataRow("synthetic-payables", "after-parent")] [DataRow("synthetic-laboratory", "after-parent")]
    [DataRow("synthetic-payables", "after-cancel")] [DataRow("synthetic-laboratory", "after-cancel")]
    public void LiveChangesBeforeDuringAndAfterApprovalDiscardRealTicket(string profileId, string scenario)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p);
        var hash = Hash(fixture.Path);
        var engine = new WorkbookEngine();
        using (var parent = engine.Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        using (var cts = new CancellationTokenSource())
        {
            var input = Input(p);
            var binding = Bind(copy, parent, p);
            var observed = new ApprovalSession(copy);
            var session = VerifiedCandidateReviewSession.Prepare(observed, parent, binding, [input]);
            var copyCells = Cells(copy);
            var parentCells = Cells(parent);
            void Change()
            {
                if (scenario.EndsWith("review"))
                {
                    var field = input.Review.Snapshot.Fields[0];
                    input.Review.Correct(field.Binding, field.Value, field.Source!);
                    input.Review.Verify(input.Review.Snapshot.Fields[0].Binding);
                }
                if (scenario.EndsWith("copy")) copy.Edit(new(binding.Copy.WorksheetId, 3, 2, "text", "Deliberate copy edit", 0));
                if (scenario.EndsWith("parent")) parent.Edit(new(binding.Parent.WorksheetId, 3, 2, "text", "Deliberate parent edit", 0));
                if (scenario.EndsWith("cancel")) cts.Cancel();
            }
            if (scenario.StartsWith("before")) Change();
            else if (scenario.StartsWith("during")) engine.StructuralLifecycleFaultForTesting = stage =>
            { if (stage == StructuralLifecycleStage.Approval) Change(); };
            else observed.AfterApproval = Change;
            void Approve() => session.Approve(Command(session.Snapshot.Preview), cts.Token);
            if (scenario.EndsWith("review")) Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(Approve).Code);
            else if (scenario.EndsWith("cancel")) Assert.ThrowsExactly<OperationCanceledException>(Approve);
            else Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(Approve).Code);
            Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            Assert.AreEqual(session.Snapshot.Preview.Candidate.CandidateId, observed.CancelledId);
            Assert.AreEqual(scenario.EndsWith("copy") ? 1 : 0, observed.CancelledRevision);
            Assert.AreEqual(VerifiedCandidateReviewStatus.Discarded, session.Snapshot.Status);
            Assert.AreEqual(scenario.StartsWith("before") || scenario == "during-cancel" ? "not-approved" : "approved",
                session.Snapshot.Preview.Candidate.Lifecycle.Approval);
            Assert.AreEqual(scenario.EndsWith("copy") ? 1 : 0, copy.Info.Revision);
            Assert.AreEqual(scenario.EndsWith("parent") ? 1 : 0, parent.Info.Revision);
            Assert.AreEqual(scenario.EndsWith("copy") ? 1 : 0, copy.WorkingCopy!.Changes.Count);
            if (!scenario.EndsWith("copy")) Assert.AreEqual(copyCells, Cells(copy));
            if (!scenario.EndsWith("parent")) Assert.AreEqual(parentCells, Cells(parent));
            if (scenario.EndsWith("copy")) Assert.AreEqual("Deliberate copy edit", copy.ReadViewport(binding.Copy.WorksheetId, 3, 2, 1, 1).Cells[0].Value);
            if (scenario.EndsWith("parent")) Assert.AreEqual("Deliberate parent edit", parent.ReadViewport(binding.Parent.WorksheetId, 3, 2, 1, 1).Cells[0].Value);
            engine.StructuralLifecycleFaultForTesting = null;
            if (copy.Info.Revision == 0 && parent.Info.Revision == 0)
            {
                var retry = VerifiedCandidateReviewSession.Prepare(copy, parent, binding, [Input(p)]);
                retry.Approve(Command(retry.Snapshot.Preview));
                retry.Discard();
            }
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", false)] [DataRow("synthetic-laboratory", false)]
    [DataRow("synthetic-payables", true)] [DataRow("synthetic-laboratory", true)]
    public void CleanupFailuresAreReportedAndReplacementTicketsAreProtected(string profileId, bool replacement)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            var observed = new ApprovalSession(copy);
            var binding = Bind(copy, parent, p);
            var input = Input(p);
            var session = VerifiedCandidateReviewSession.Prepare(observed, parent, binding, [input]);
            var command = Command(session.Snapshot.Preview);
            var cells = Cells(copy);
            Guid? replacementId = null;
            observed.AfterApproval = () =>
            {
                input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
                if (replacement) replacementId = VerifiedCandidatePreparation.Prepare(copy, parent, binding, [Input(p)]).Candidate.CandidateId;
                else observed.CleanupFailure = new IOException("Synthetic cleanup failure");
            };
            var failure = Assert.ThrowsExactly<AggregateException>(() => session.Approve(command));
            Assert.IsInstanceOfType<FieldReviewException>(failure.InnerExceptions[0]);
            if (replacement) Assert.AreEqual("STALE_REVIEW", ((WorkbookException)failure.InnerExceptions[1]).Code);
            else Assert.IsInstanceOfType<IOException>(failure.InnerExceptions[1]);
            Assert.AreEqual(command.CandidateId, observed.CancelledId);
            Assert.AreEqual(replacementId ?? command.CandidateId, ((WorkbookSession)copy).CurrentStructuralReview!.CandidateId);
            Assert.AreEqual(replacement ? "not-approved" : "approved", ((WorkbookSession)copy).CurrentStructuralReview!.Lifecycle.Approval);
            Assert.AreEqual(VerifiedCandidateReviewStatus.Invalidated, session.Snapshot.Status);
            Assert.AreEqual(cells, Cells(copy));
            Assert.AreEqual(0, copy.Info.Revision);
            Assert.AreEqual(0, parent.Info.Revision);
            copy.CancelCandidateReview(replacementId ?? command.CandidateId, 0);
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    private sealed class ApprovalSession(IWorkbookSession inner) : IWorkbookSession
    {
        internal Action? AfterApproval;
        internal Exception? CleanupFailure;
        internal Guid CancelledId;
        internal int CancelledRevision;
        public WorkbookInfo Info => inner.Info;
        public WorkingCopyInfo? WorkingCopy => inner.WorkingCopy;
        public CellViewport ReadViewport(Guid id, int row, int column, int rows, int columns) => inner.ReadViewport(id, row, column, rows, columns);
        public StructuralCandidateReview PrepareCandidateReview(IWorkbookSession parent, WorkflowPlanRequest request, int revision, CancellationToken token = default) =>
            inner.PrepareCandidateReview(parent, request, revision, token);
        public StructuralCandidateReview ApproveCandidateReview(IWorkbookSession parent, CandidateReviewApproval request, CancellationToken token = default)
        {
            var result = inner.ApproveCandidateReview(parent, request, token);
            AfterApproval?.Invoke();
            return result;
        }
        public void CancelCandidateReview(Guid id, int revision)
        {
            CancelledId = id; CancelledRevision = revision;
            if (CleanupFailure != null) throw CleanupFailure;
            inner.CancelCandidateReview(id, revision);
        }
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Unexpected snapshot");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected copy");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected save");
        public void Dispose() { }
    }
}
