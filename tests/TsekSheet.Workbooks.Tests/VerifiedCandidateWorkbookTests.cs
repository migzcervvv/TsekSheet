using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class VerifiedCandidateWorkbookTests
{
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    internal static VerifiedPlacementInput Input(WorkflowProfile p, string id = "NEW", string entity = "Other Tools Ltd")
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('C', 64), 2, 1);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, source), new(p.IdentifierFields[0], id, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "+00123.4500", source),
            new(p.ContextFields[1], "2026-10-29", source)]);
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
        return new(review, review.Snapshot.Version);
    }

    internal static CandidateDestination Bind(IWorkbookSession copy, IWorkbookSession parent, WorkflowProfile p)
    {
        MappingDestination Destination(IWorkbookSession session) => new(session.Info.SessionId, copy.WorkingCopy!.SourceWorksheetId,
            session.Info.Revision, session.Info.SourceSha256, p.Id, WorkflowProfiles.Hash(p));
        return new(Destination(copy), Destination(parent), copy.WorkingCopy!.BaseRevision);
    }

    internal static string Cells(IWorkbookSession session) => JsonSerializer.Serialize(session.Info.Worksheets.Select(sheet =>
        new { sheet.Id, Cells = Enumerable.Range(0, (sheet.LastRow + 19) / 20).SelectMany(i =>
            session.ReadViewport(sheet.Id, 1 + i * 20, 1, Math.Min(20, sheet.LastRow - i * 20), sheet.LastColumn).Cells).ToArray() }));

    [TestMethod]
    [DataRow("synthetic-payables", 1)] [DataRow("synthetic-payables", 20)]
    [DataRow("synthetic-laboratory", 1)] [DataRow("synthetic-laboratory", 20)]
    public void RealReviewReopensAndPassesOracleWithIndependentBindingsWithoutChangingWorkbooks(string profileId, int count)
    {
        var p = WorkflowProfiles.Get(profileId);
        foreach (var shift in new[] { 0, 20 })
        {
            using var fixture = new Fixture(p, shift);
            var hash = Hash(fixture.Path);
            using (var parent = new WorkbookEngine().Open(fixture.Path))
            using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
            using (var sibling = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
            {
                var inputs = Enumerable.Range(0, count).Select(i => Input(p, "NEW-" + i)).ToArray();
                var reviews = JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot));
                var before = new[] { parent, copy, sibling }.Select(s => (Info: s.Info, Cells: Cells(s))).ToArray();
                var destination = Bind(copy, parent, p);
                var result = VerifiedCandidatePreparation.Prepare(copy, parent, destination, inputs);
                var candidate = result.Candidate;
                Assert.IsFalse(result.CanApply);
                Assert.IsFalse(candidate.Proposal.CanApply);
                Assert.IsTrue(candidate.ReviewOnly);
                Assert.IsTrue(candidate.OracleReport!.Accepted);
                Assert.IsEmpty(candidate.OracleReport.Findings);
                Assert.IsNotEmpty(candidate.OracleReport.AllowedChanges);
                Assert.IsEmpty(candidate.RejectionReasons);
                Assert.AreEqual(new WorkflowReviewState("resolved", "eligible-experimental", "generated", "accepted", "not-approved", "not-adopted", "not-integrated", "not-exported"), candidate.Lifecycle);
                Assert.AreEqual(parent.Info.SessionId, candidate.DestinationSessionId);
                Assert.AreEqual(0, candidate.ExpectedDestinationRevision);
                Assert.AreEqual(1, candidate.IntendedWorkingCopyRevision);
                Assert.AreEqual(1, candidate.IntendedDestinationRevision);
                foreach (var fingerprint in new[] { candidate.CandidateSha256, candidate.ManifestSha256, candidate.ReviewSha256 })
                    Assert.IsTrue(fingerprint.Length == 64 && fingerprint.All(Uri.IsHexDigit));
                Assert.AreEqual(8 + shift, candidate.Operation.InsertionBoundary);
                Assert.AreEqual(count, candidate.Operation.RowCount);
                Assert.IsNotEmpty(candidate.FormulaImpacts);
                for (var i = 0; i < count; i++)
                {
                    var mapping = result.Mappings[i];
                    Assert.AreEqual(inputs[i].ExpectedReview, mapping.Review);
                    Assert.AreEqual(destination.Copy, mapping.Destination);
                    Assert.AreEqual(shift == 0 ? 1 : 6, mapping.Fields[0].Column);
                    Assert.AreEqual(inputs[i].ExpectedReview.ReviewId.ToString("N"), candidate.Proposal.Records[i].RecordId);
                    Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
                    Assert.AreEqual(123.45m, mapping.Fields[4].Value.DecimalValue);
                    Assert.AreEqual(new DateOnly(2026, 10, 29), mapping.Fields[5].Value.DateValue);
                    Assert.AreEqual(inputs[i].Review.Snapshot.Fields[0].Source, mapping.Fields[0].Value.Source);
                    Assert.AreEqual(8 + shift + i, candidate.Proposal.Records[i].ProposedRow);
                }
                Assert.AreEqual(candidate.CandidateId, ((WorkbookSession)copy).CurrentStructuralReview!.CandidateId);
                foreach (var (session, index) in new[] { parent, copy, sibling }.Select((s, i) => (s, i)))
                {
                    Assert.AreEqual(before[index].Info, session.Info);
                    Assert.AreEqual(before[index].Cells, Cells(session));
                }
                Assert.IsEmpty(copy.WorkingCopy!.Changes);
                Assert.IsEmpty(sibling.WorkingCopy!.Changes);
                Assert.IsNull(((WorkbookSession)sibling).CurrentStructuralReview);
                Assert.AreEqual(reviews, JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot)));
                Assert.AreEqual("ROW_INSERTION_DISABLED", Assert.ThrowsExactly<WorkbookException>(() =>
                    copy.InsertWorkflowRows(candidate.Proposal with { CanApply = true }, true, true)).Code);
                copy.CancelCandidateReview(candidate.CandidateId, 0);
                Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            }
            Assert.AreEqual(hash, Hash(fixture.Path));
        }
    }

    [TestMethod]
    [DataRow("synthetic-payables")] [DataRow("synthetic-laboratory")]
    public void UnqualifiedFormattingAndDuplicatesRemainExplicitReviewFindings(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p, height: 22);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            var cells = Cells(copy);
            var result = VerifiedCandidatePreparation.Prepare(copy, parent, Bind(copy, parent, p), [Input(p, "OLD-6")]);
            Assert.IsTrue(result.Candidate.OracleReport!.Accepted);
            Assert.IsNotEmpty(result.Candidate.RejectionReasons);
            StringAssert.Contains(result.Candidate.RejectionReasons[0], "21-point");
            Assert.IsNotEmpty(result.Candidate.Proposal.Records[0].Duplicates);
            Assert.AreEqual("not-approved", result.Candidate.Lifecycle.Approval);
            Assert.AreEqual("not-adopted", result.Candidate.Lifecycle.WorkingCopyAdoption);
            Assert.IsFalse(result.CanApply);
            Assert.AreEqual(cells, Cells(copy));
            Assert.AreEqual(0, copy.Info.Revision);
            Assert.IsFalse(parent.Info.IsDirty);
            copy.CancelCandidateReview(result.Candidate.CandidateId, 0);
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "multi-boundary")] [DataRow("synthetic-laboratory", "multi-boundary")]
    [DataRow("synthetic-payables", "merge")] [DataRow("synthetic-laboratory", "merge")]
    [DataRow("synthetic-payables", "hidden-row")] [DataRow("synthetic-laboratory", "hidden-row")]
    [DataRow("synthetic-payables", "unsupported-formula")] [DataRow("synthetic-laboratory", "unsupported-formula")]
    [DataRow("synthetic-payables", "dirty")] [DataRow("synthetic-laboratory", "dirty")]
    [DataRow("synthetic-payables", "destination-changed")] [DataRow("synthetic-laboratory", "destination-changed")]
    public void UnsupportedGenerationRetainsAllCellsChangesReviewsAndSource(string profileId, string feature)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p, 20, feature);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        {
            if (feature == "dirty") copy.Edit(new(copy.Info.Worksheets[0].Id, 3, 2, "text", "Copy change", 0));
            if (feature == "destination-changed") parent.Edit(new(parent.Info.Worksheets[0].Id, 3, 2, "text", "Parent change", 0));
            var inputs = feature == "multi-boundary" ? new[] { Input(p), Input(p, "NEW-B", "Novel Organization") } : [Input(p)];
            var before = new[] { parent, copy }.Select(s => (Info: s.Info, Cells: Cells(s))).ToArray();
            var changes = copy.WorkingCopy!.Changes.ToArray();
            var reviews = JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot));
            Assert.ThrowsExactly<WorkbookException>(() => VerifiedCandidatePreparation.Prepare(copy, parent, Bind(copy, parent, p), inputs));
            Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            foreach (var (session, index) in new[] { parent, copy }.Select((s, i) => (s, i)))
            {
                Assert.AreEqual(before[index].Info, session.Info);
                Assert.AreEqual(before[index].Cells, Cells(session));
            }
            CollectionAssert.AreEqual(changes, copy.WorkingCopy.Changes.ToArray());
            Assert.AreEqual(reviews, JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot)));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "review")] [DataRow("synthetic-laboratory", "review")]
    [DataRow("synthetic-payables", "cancel")] [DataRow("synthetic-laboratory", "cancel")]
    [DataRow("synthetic-payables", "oracle-reject")] [DataRow("synthetic-laboratory", "oracle-reject")]
    public void GenerationChangesAndFailuresCleanUpAndAllowFreshRetry(string profileId, string scenario)
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
            var destination = Bind(copy, parent, p);
            var cells = Cells(copy);
            engine.StructuralFaultForTesting = stage =>
            {
                if (stage != StructuralStage.Oracle) return;
                if (scenario == "review") input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
                if (scenario == "cancel") cts.Cancel();
            };
            if (scenario == "oracle-reject") engine.CandidateFaultForTesting = bytes =>
                ExcelFormattingReferenceTests.Rewrite(bytes, "xl/worksheets/sheet1.xml", xml =>
                    xml.Descendants(StructuralPreservationOracle.S + "row").First().SetAttributeValue("ht", "37"));
            if (scenario == "review") Assert.ThrowsExactly<FieldReviewException>(() => VerifiedCandidatePreparation.Prepare(copy, parent, destination, [input]));
            else if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(() => VerifiedCandidatePreparation.Prepare(copy, parent, destination, [input], cts.Token));
            else Assert.AreEqual("STRUCTURAL_VALIDATION_FAILED", Assert.ThrowsExactly<WorkbookException>(() =>
                VerifiedCandidatePreparation.Prepare(copy, parent, destination, [input])).Code);
            Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            Assert.AreEqual(cells, Cells(copy));
            Assert.AreEqual(0, copy.Info.Revision);
            Assert.IsEmpty(copy.WorkingCopy!.Changes);
            Assert.AreEqual(0, parent.Info.Revision);
            engine.StructuralFaultForTesting = null;
            engine.CandidateFaultForTesting = null;
            var retry = VerifiedCandidatePreparation.Prepare(copy, parent, destination, [Input(p)]);
            Assert.IsTrue(retry.Candidate.OracleReport!.Accepted);
            copy.CancelCandidateReview(retry.Candidate.CandidateId, 0);
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "parent")] [DataRow("synthetic-laboratory", "parent")]
    [DataRow("synthetic-payables", "copy")] [DataRow("synthetic-laboratory", "copy")]
    [DataRow("synthetic-payables", "review")] [DataRow("synthetic-laboratory", "review")]
    [DataRow("synthetic-payables", "cancel")] [DataRow("synthetic-laboratory", "cancel")]
    public void PostGenerationAuthorityChangesDiscardRealTicketAtCurrentCopyRevision(string profileId, string scenario)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p);
        var hash = Hash(fixture.Path);
        using (var parent = new WorkbookEngine().Open(fixture.Path))
        using (var copy = parent.CreateWorkingCopy(parent.Info.Worksheets[0].Id, 0))
        using (var cts = new CancellationTokenSource())
        {
            var input = Input(p);
            var binding = Bind(copy, parent, p);
            var copyCells = Cells(copy);
            var parentCells = Cells(parent);
            var observed = new AfterGenerationSession(copy, () =>
            {
                if (scenario == "parent") parent.Edit(new(binding.Parent.WorksheetId, 3, 2, "text", "Deliberate parent edit", 0));
                if (scenario == "copy") copy.Edit(new(binding.Copy.WorksheetId, 3, 2, "text", "Deliberate copy edit", 0));
                if (scenario == "review") input.Review.Retract(input.Review.Snapshot.Fields[0].Binding);
                if (scenario == "cancel") cts.Cancel();
            });
            void Prepare() => VerifiedCandidatePreparation.Prepare(observed, parent, binding, [input], cts.Token);
            if (scenario == "review") Assert.ThrowsExactly<FieldReviewException>(Prepare);
            else if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Prepare);
            else Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(Prepare).Code);
            Assert.IsNull(((WorkbookSession)copy).CurrentStructuralReview);
            Assert.AreEqual(observed.GeneratedId, observed.CancelledId);
            Assert.AreEqual(scenario == "copy" ? 1 : 0, observed.CancelledRevision);
            Assert.AreEqual(scenario == "copy" ? 1 : 0, copy.Info.Revision);
            Assert.AreEqual(scenario == "parent" ? 1 : 0, parent.Info.Revision);
            Assert.AreEqual(scenario == "copy" ? 1 : 0, copy.WorkingCopy!.Changes.Count);
            if (scenario != "copy") Assert.AreEqual(copyCells, Cells(copy));
            if (scenario != "parent") Assert.AreEqual(parentCells, Cells(parent));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    // Observe the existing public engine contract; no internal capability flags
    // or approval/adoption operations are used by this deterministic race seam.
    private sealed class AfterGenerationSession(IWorkbookSession inner, Action after) : IWorkbookSession
    {
        internal Guid GeneratedId;
        internal Guid CancelledId;
        internal int CancelledRevision;
        public WorkbookInfo Info => inner.Info;
        public WorkingCopyInfo? WorkingCopy => inner.WorkingCopy;
        public CellViewport ReadViewport(Guid id, int row, int column, int rows, int columns) => inner.ReadViewport(id, row, column, rows, columns);
        public StructuralCandidateReview PrepareCandidateReview(IWorkbookSession parent, WorkflowPlanRequest request, int revision, CancellationToken token = default)
        {
            var result = inner.PrepareCandidateReview(parent, request, revision, token);
            GeneratedId = result.CandidateId;
            after();
            return result;
        }
        public void CancelCandidateReview(Guid id, int revision)
        {
            CancelledId = id; CancelledRevision = revision;
            inner.CancelCandidateReview(id, revision);
        }
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Unexpected snapshot");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected copy");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Unexpected mutation");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Unexpected save");
        public void Dispose() { }
    }

    internal sealed class Fixture : IDisposable
    {
        internal string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-verified-candidate-" + Guid.NewGuid().ToString("N") + ".xlsx");
        internal Fixture(WorkflowProfile p, int shift = 0, string feature = "plain", double height = 21)
        {
            using var wb = new XLWorkbook();
            var sheet = wb.AddWorksheet(shift == 0 ? "Artificial first" : "Independent shifted");
            int Column(int field) => shift == 0 ? field + 1 : p.Fields.Count - field;
            for (var field = 0; field < p.Fields.Count; field++) sheet.Cell(2, Column(field)).Value = p.Fields[field].Header;
            sheet.Row(2).Style.Font.Bold = true;
            sheet.Cell(3, 2).Value = "Artificial note";
            for (var section = 0; section < 2; section++)
            {
                var heading = (section == 0 ? 5 : 15) + shift;
                var subtotal = (section == 0 ? 10 : 22) + shift;
                sheet.Cell(heading, Column(0)).Value = p.Sections[section].Heading;
                sheet.Cell(subtotal, Column(0)).Value = p.Sections[section].Subtotal;
                var amount = XLHelper.GetColumnLetterFromNumber(Column(4));
                sheet.Cell(subtotal, Column(4)).FormulaA1 = $"SUM({amount}{heading + 1}:{amount}{subtotal - 1})";
                for (var row = heading + 1; row <= heading + 2; row++)
                {
                    sheet.Cell(row, Column(0)).Value = section == 0 ? "Other Tools Ltd" : "Cross Vendor Ltd";
                    sheet.Cell(row, Column(1)).Value = "Original particulars";
                    sheet.Cell(row, Column(2)).Value = "OLD-" + row;
                    sheet.Cell(row, Column(4)).Value = 100m;
                    sheet.Cell(row, Column(5)).Value = new DateTime(2024, 1, 2);
                    sheet.Range(row, 1, row, 6).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                    sheet.Cell(row, Column(4)).Style.NumberFormat.Format = "#,##0.00";
                    sheet.Cell(row, Column(4)).Style.Fill.BackgroundColor = XLColor.LightBlue;
                    sheet.Cell(row, Column(5)).Style.NumberFormat.Format = "yyyy-mm-dd";
                    sheet.Row(row).Height = height;
                }
            }
            sheet.Cell(25 + shift, Column(0)).Value = "Unrelated footer";
            var column = XLHelper.GetColumnLetterFromNumber(Column(4));
            sheet.Cell(25 + shift, Column(4)).FormulaA1 = $"{column}{10 + shift}+{column}{22 + shift}";
            wb.AddWorksheet("Unselected context").Cell(1, 1).Value = "Independent constant context";
            if (feature == "merge") sheet.Range(1, 1, 1, 6).Merge().FirstCell().Value = "Merged heading";
            if (feature == "hidden-row") sheet.Row(7 + shift).Hide();
            if (feature == "unsupported-formula") sheet.Cell(26 + shift, Column(4)).FormulaA1 = $"IF({column}{6 + shift}>0,1,0)";
            using var output = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite);
            wb.SaveAs(output, new ClosedXML.Excel.SaveOptions { GenerateCalculationChain = false, EvaluateFormulasBeforeSaving = false });
        }
        public void Dispose() => File.Delete(Path);
    }
}
