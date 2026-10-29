using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class StructuralLifecycleTests
{
    private sealed class Context : IDisposable
    {
        internal readonly ReferenceCorpusFixture Fixture = new();
        internal readonly WorkbookEngine Engine = new() { StructuralExperimentsEnabled = true, StructuralIntegrationExperimentsEnabled = true };
        internal readonly IWorkbookSession Main;
        internal readonly WorkbookSession Copy;
        internal readonly string OutputFolder = Path.Combine(Path.GetTempPath(), "TsekSheet-structural-export-" + Guid.NewGuid().ToString("N"));
        internal readonly string SourceHash;
        internal StructuralCandidateReview? Review;
        internal WorkflowPlanRequest Request;
        internal Context(bool destinationAlreadyEdited = false, bool duplicate = false, bool batch = false)
        {
            Directory.CreateDirectory(OutputFolder); SourceHash = ReferenceCorpusFixture.Hash(Fixture.Path); Main = Engine.Open(Fixture.Path);
            if (destinationAlreadyEdited) Main.Edit(new(Main.Info.Worksheets[0].Id, 25, 1, "text", "Accepted prior main-session edit", 0));
            Copy = (WorkbookSession)Main.CreateWorkingCopy(Main.Info.Worksheets[0].Id, Main.Info.Revision);
            var profile = WorkflowProfiles.Get("synthetic-payables");
            WorkflowRecord Record(string id, string vendor) => new(id, new Dictionary<string, string> { ["vendor"] = vendor, ["rfp"] = duplicate ? "R2" : "NEW-" + id, ["amount"] = "25", ["date"] = "2026-10-29" });
            Request = new(Copy.Info.SessionId, Copy.Info.Worksheets[0].Id, 0, profile.Id, WorkflowProfiles.Hash(profile),
                batch ? [Record("one", "Novel Company"), Record("two", "Distinct Supplier")] : [Record("one", "Other Tools Ltd")]);
        }
        internal StructuralApproval Approval => new(Review!.CandidateId, Review.ReviewSha256, Review.CandidateSha256, Review.Proposal.Revision, true);
        internal StructuralIntegrationRequest Integration => new(Review!.CandidateId, Review.ReviewSha256, Review.CandidateSha256,
            Copy.Info.SessionId, Copy.Info.Worksheets[0].Id, Copy.Info.Revision, Main.Info.Revision, Copy.WorkingCopy!.BaseRevision);
        internal void Prepare() => Review = Copy.PrepareStructuralReview(Request, WorkflowPlanning.Prepare(Copy, Request));
        internal void Approve() => Review = Copy.ApproveStructuralReview(Approval);
        internal void Adopt() => Review = Copy.AdoptStructuralReview(Approval);
        internal void Merge() => ((WorkbookSession)Main).IntegrateReviewedStructure(Copy, Integration);
        internal string Output => Path.Combine(OutputFolder, "export.xlsx");
        public void Dispose()
        {
            Copy.Dispose(); Main.Dispose();
            Assert.AreEqual(SourceHash, ReferenceCorpusFixture.Hash(Fixture.Path)); Fixture.Dispose();
            Assert.IsEmpty(Directory.GetFiles(OutputFolder, ".tseksheet-*.tmp"), "Owned staging files must be cleaned.");
            foreach (var file in Directory.GetFiles(OutputFolder)) File.Delete(file);
            Directory.Delete(OutputFolder);
        }
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ReviewedCandidateSurvivesSeparateAdoptionIntegrationAndExport(bool batch)
    {
        using var c = new Context(batch: batch); var initial = c.Copy.Info; c.Prepare();
        Assert.AreEqual(initial, c.Copy.Info); Assert.AreEqual("generated", c.Review!.Lifecycle.Generation); Assert.AreEqual("accepted", c.Review.Lifecycle.Oracle);
        Assert.AreEqual("not-approved", c.Review.Lifecycle.Approval); Assert.AreEqual("not-adopted", c.Review.Lifecycle.WorkingCopyAdoption);
        Assert.IsNotEmpty(c.Review.FormulaImpacts); Assert.IsNotEmpty(c.Review.StyleAssumptions);
        Assert.AreEqual(batch ? 22 : 8, c.Review.Operation.InsertionBoundary); Assert.AreEqual(batch ? 16 : 6, c.Review.Operation.TemplateRow);
        Assert.AreEqual(batch ? 2 : 1, c.Review.Operation.RowCount); Assert.IsEmpty(c.Review.RejectionReasons);
        Assert.ThrowsExactly<WorkbookException>(() => c.Copy.AdoptStructuralReview(c.Approval));
        c.Approve(); Assert.AreEqual("approved", c.Review.Lifecycle.Approval); Assert.AreEqual(0, c.Copy.Info.Revision);
        c.Adopt(); Assert.AreEqual(1, c.Copy.Info.Revision); Assert.AreEqual("adopted", c.Review.Lifecycle.WorkingCopyAdoption);
        var unmerged = Path.Combine(c.OutputFolder, "unmerged.xlsx"); c.Main.SaveAsNew(unmerged, 0);
        Assert.AreEqual(c.SourceHash, ReferenceCorpusFixture.Hash(unmerged), "Unmerged rows must not silently enter an export.");
        Assert.AreEqual(0, c.Main.Info.Revision); c.Merge(); Assert.AreEqual(1, c.Main.Info.Revision); Assert.IsTrue(c.Main.Info.IsDirty);
        Assert.AreEqual("integrated", ((WorkbookSession)c.Main).CurrentStructuralReview!.Lifecycle.Integration);
        Assert.AreEqual("eligible-experimental", ((WorkbookSession)c.Main).CurrentStructuralReview!.Lifecycle.Export);
        var receipt = c.Main.SaveAsNew(c.Output, 1); Assert.AreEqual(1, receipt.SavedRevision); Assert.AreEqual(c.Review.CandidateSha256, receipt.DestinationSha256);
        Assert.AreEqual(c.Review.CandidateSha256, ReferenceCorpusFixture.Hash(c.Output));
        Assert.AreEqual("exported", ((WorkbookSession)c.Main).CurrentStructuralReview!.Lifecycle.Export);
        using var reopened = c.Engine.Open(c.Output);
        var sheet = reopened.Info.Worksheets[0].Id; var row = batch ? 22 : 8;
        Assert.AreEqual("NEW-one", reopened.ReadViewport(sheet, row, 3, 1, 1).Cells[0].Value);
        if (batch) Assert.AreEqual("NEW-two", reopened.ReadViewport(sheet, 23, 3, 1, 1).Cells[0].Value);
        Assert.AreEqual("Artificial constant-only context", reopened.ReadViewport(reopened.Info.Worksheets[1].Id, 1, 1, 1, 1).Cells[0].Value);
        var manifest = new StructuralInsertionManifest("Ledger", row, batch ? 16 : 6, batch ? 16 : 6, batch ? 22 : 10,
            c.Review.Proposal.Records.Select(r => new StructuralRecord(r.Fields.Select(f => new StructuralValue(f.Column, f.Kind, f.Value)).ToArray())).ToArray());
        Assert.IsTrue(StructuralPreservationOracle.Evaluate(File.ReadAllBytes(c.Fixture.Path), manifest, File.ReadAllBytes(c.Output)).Accepted);
        Assert.AreEqual(c.SourceHash, ReferenceCorpusFixture.Hash(unmerged));
        Assert.ThrowsExactly<WorkbookException>(() => c.Main.SaveAsNew(c.Output, 1));
        Assert.ThrowsExactly<WorkbookException>(() => c.Copy.AdoptStructuralReview(c.Approval));
        Assert.ThrowsExactly<WorkbookException>(c.Merge);
    }

    [TestMethod]
    public void CopyAndDestinationRevisionsHaveIndependentBindings()
    {
        using var c = new Context(destinationAlreadyEdited: true); Assert.AreEqual(1, c.Main.Info.Revision); Assert.AreEqual(0, c.Copy.Info.Revision);
        c.Prepare(); Assert.AreEqual(1, c.Review!.IntendedWorkingCopyRevision); Assert.AreEqual(2, c.Review.IntendedDestinationRevision);
        c.Approve(); c.Adopt(); Assert.AreEqual(1, c.Copy.Info.Revision); c.Merge(); Assert.AreEqual(2, c.Main.Info.Revision);
        c.Main.SaveAsNew(c.Output, 2); Assert.AreEqual(c.Review!.CandidateSha256, ReferenceCorpusFixture.Hash(c.Output));
    }

    [TestMethod]
    [DataRow("candidate")] [DataRow("review")] [DataRow("bytes")] [DataRow("revision")] [DataRow("edit")] [DataRow("regenerated")]
    public void ApprovalCannotApplyAnAlteredOrStaleReview(string mutation)
    {
        using var c = new Context(); c.Prepare(); var approval = c.Approval;
        switch (mutation)
        {
            case "candidate": approval = approval with { CandidateId = Guid.NewGuid() }; break;
            case "review": approval = approval with { ReviewSha256 = new string('0', 64) }; break;
            case "bytes": approval = approval with { CandidateSha256 = new string('0', 64) }; break;
            case "revision": approval = approval with { ExpectedCopyRevision = 9 }; break;
            case "edit": c.Copy.Edit(new(c.Copy.Info.Worksheets[0].Id, 25, 1, "text", "Changed after review", 0)); break;
            case "regenerated": c.Prepare(); break;
        }
        var before = c.Copy.Info; Assert.ThrowsExactly<WorkbookException>(() => c.Copy.ApproveStructuralReview(approval)); Assert.AreEqual(before, c.Copy.Info);
        Assert.AreEqual(0, c.Main.Info.Revision);
    }

    [TestMethod]
    public void ReturnedReviewMutationCannotAlterTheServerCandidateAndDuplicatesRequireApproval()
    {
        using var c = new Context(duplicate: true); c.Prepare(); Assert.IsNotEmpty(c.Review!.Proposal.Records[0].Duplicates);
        Assert.ThrowsExactly<WorkbookException>(() => c.Copy.ApproveStructuralReview(c.Approval with { DuplicatesReviewed = false }));
        c.Review = c.Review with { FormulaImpacts = ["forged display"], Proposal = c.Review.Proposal with { Records = [] } };
        Assert.IsNotEmpty(c.Copy.CurrentStructuralReview!.Proposal.Records);
        c.Approve(); c.Adopt(); c.Merge(); c.Main.SaveAsNew(c.Output, 1);
    }

    [TestMethod]
    public void GeneratedCandidateRequiresTheExplicitVendorSectionChoice()
    {
        using var c = new Context();
        var input = c.Request.Records[0] with { Values = new Dictionary<string, string> { ["vendor"] = "Acme Trading Incorporated", ["rfp"] = "NEW-SECTION", ["amount"] = "25" } };
        c.Request = c.Request with { Records = [input] };
        Assert.ThrowsExactly<WorkbookException>(c.Prepare); Assert.AreEqual(0, c.Copy.Info.Revision);
        c.Request = c.Request with { Records = [input with { SelectedSectionId = "external" }] };
        c.Prepare(); Assert.AreEqual("external", c.Review!.Proposal.Records[0].SectionId); Assert.AreEqual(18, c.Review.Proposal.Records[0].BaseInsertionRow);
        c.Approve(); c.Adopt(); c.Merge(); c.Main.SaveAsNew(c.Output, 1);
        using var reopened = c.Engine.Open(c.Output);
        Assert.AreEqual("NEW-SECTION", reopened.ReadViewport(reopened.Info.Worksheets[0].Id, 18, 3, 1, 1).Cells[0].Value);
    }

    [TestMethod]
    [DataRow("destination")] [DataRow("copy")] [DataRow("token")] [DataRow("worksheet")] [DataRow("capability")]
    public void StructuralMergeRejectsEveryUnreviewedDivergence(string divergence)
    {
        using var c = new Context(); c.Prepare(); c.Approve(); c.Adopt(); var request = c.Integration;
        if (divergence == "destination") c.Main.Edit(new(c.Main.Info.Worksheets[0].Id, 25, 1, "text", "Concurrent destination edit", 0));
        if (divergence == "copy") c.Copy.Edit(new(c.Copy.Info.Worksheets[0].Id, 26, 1, "text", "Unreviewed copy edit", 1));
        if (divergence == "token") request = request with { ReviewSha256 = "forged" };
        if (divergence == "worksheet") request = request with { WorksheetId = Guid.NewGuid() };
        if (divergence == "capability") c.Engine.StructuralIntegrationExperimentsEnabled = false;
        var before = c.Main.Info; Assert.ThrowsExactly<WorkbookException>(() => ((WorkbookSession)c.Main).IntegrateReviewedStructure(c.Copy, request));
        Assert.AreEqual(before, c.Main.Info); Assert.AreEqual(c.SourceHash, ReferenceCorpusFixture.Hash(c.Fixture.Path));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)]
    public void FullLifecycleFaultsAndCancellationPreserveStateAndPermitRetry(int selected)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var c = new Context(); using var cts = new CancellationTokenSource();
            var stage = (StructuralLifecycleStage)selected;
            if (selected > 0) c.Prepare(); if (selected > 1) c.Approve(); if (selected > 2) c.Adopt(); if (selected > 5) c.Merge();
            var mainBefore = c.Main.Info; var copyBefore = c.Copy.Info;
            c.Engine.StructuralLifecycleFaultForTesting = current => { if (current != stage) return; if (cancel) cts.Cancel(); else throw new WorkbookException("INJECTED", "Deterministic lifecycle failure"); };
            void Attempt()
            {
                switch (stage)
                {
                    case StructuralLifecycleStage.Planning: c.Copy.PrepareStructuralReview(c.Request, WorkflowPlanning.Prepare(c.Copy, c.Request), cts.Token); break;
                    case StructuralLifecycleStage.Approval: c.Copy.ApproveStructuralReview(c.Approval, cts.Token); break;
                    case StructuralLifecycleStage.WorkingCopyAdoption: c.Copy.AdoptStructuralReview(c.Approval, cts.Token); break;
                    case StructuralLifecycleStage.MergeCandidate: case StructuralLifecycleStage.MergeOracle: case StructuralLifecycleStage.MergeAdoption:
                        ((WorkbookSession)c.Main).IntegrateReviewedStructure(c.Copy, c.Integration, cts.Token); break;
                    default: c.Main.SaveAsNew(c.Output, c.Main.Info.Revision, cts.Token); break;
                }
            }
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(Attempt); else Assert.ThrowsExactly<WorkbookException>(Attempt);
            Assert.AreEqual(mainBefore, c.Main.Info); Assert.AreEqual(copyBefore, c.Copy.Info); Assert.IsFalse(File.Exists(c.Output));
            c.Engine.StructuralLifecycleFaultForTesting = null;
            if (selected == 0) c.Prepare(); if (selected <= 1) c.Approve(); if (selected <= 2) c.Adopt(); if (selected <= 5) c.Merge();
            c.Main.SaveAsNew(c.Output, c.Main.Info.Revision); Assert.AreEqual(c.Review!.CandidateSha256, ReferenceCorpusFixture.Hash(c.Output));
        }
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void NativeCommitFaultOrCancellationLeavesNoPartialExportAndCanRetry(bool cancel)
    {
        using var c = new Context(); c.Prepare(); c.Approve(); c.Adopt(); c.Merge(); using var cts = new CancellationTokenSource(); var before = c.Main.Info;
        c.Engine.CommitFaultsForTesting = new() { BeforeCommit = () => { if (cancel) cts.Cancel(); else throw new IOException("Injected native boundary failure"); } };
        if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => c.Main.SaveAsNew(c.Output, 1, cts.Token));
        else Assert.ThrowsExactly<WorkbookException>(() => c.Main.SaveAsNew(c.Output, 1));
        Assert.AreEqual(before, c.Main.Info); Assert.IsFalse(File.Exists(c.Output)); Assert.IsEmpty(Directory.GetFiles(c.OutputFolder));
        c.Engine.CommitFaultsForTesting = null; c.Main.SaveAsNew(c.Output, 1); Assert.AreEqual(c.Review!.CandidateSha256, ReferenceCorpusFixture.Hash(c.Output));
    }

    [TestMethod]
    public void ExportRejectsPostIntegrationEditsAndDisabledCapability()
    {
        using var c = new Context(); c.Prepare(); c.Approve(); c.Adopt(); c.Merge();
        c.Engine.StructuralIntegrationExperimentsEnabled = false; Assert.ThrowsExactly<WorkbookException>(() => c.Main.SaveAsNew(c.Output, 1));
        c.Engine.StructuralIntegrationExperimentsEnabled = true;
        c.Main.Edit(new(c.Main.Info.Worksheets[0].Id, 26, 1, "text", "Post-integration divergence", 1));
        Assert.ThrowsExactly<WorkbookException>(() => c.Main.SaveAsNew(c.Output, 2)); Assert.IsFalse(File.Exists(c.Output));
    }

    [TestMethod]
    [DataRow("merge-corrupt")] [DataRow("merge-content")] [DataRow("export-content")]
    public void ActualCandidateCorruptionCannotEscapeIntegrationOrExport(string defect)
    {
        using var c = new Context(); c.Prepare(); c.Approve(); c.Adopt();
        if (defect == "export-content") c.Merge();
        var before = c.Main.Info;
        c.Engine.CandidateFaultForTesting = bytes =>
        {
            if (defect == "merge-corrupt") return [0, 1, 2];
            using var output = new MemoryStream(); output.Write(bytes);
            using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
                doc.Descendants(StructuralPreservationOracle.S + "c").Single(e => (string?)e.Attribute("r") == "E6").Element(StructuralPreservationOracle.S + "v")!.Value = "999";
                entry.Delete(); using var target = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); doc.Save(target);
            }
            return output.ToArray();
        };
        if (defect == "export-content") Assert.ThrowsExactly<WorkbookException>(() => c.Main.SaveAsNew(c.Output, 1));
        else Assert.ThrowsExactly<WorkbookException>(c.Merge);
        Assert.AreEqual(before, c.Main.Info); Assert.IsFalse(File.Exists(c.Output));
        c.Engine.CandidateFaultForTesting = null; if (defect != "export-content") c.Merge(); c.Main.SaveAsNew(c.Output, 1);
        Assert.AreEqual(c.Review!.CandidateSha256, ReferenceCorpusFixture.Hash(c.Output));
    }

    [TestMethod]
    [DataRow("absolute-reference")] [DataRow("missed-row-shift")] [DataRow("incomplete-subtotal")] [DataRow("duplicate-record")]
    public void OracleRejectsPlausibleEditorTranslationDefects(string defect)
    {
        using var fixture = new ReferenceCorpusFixture(); var before = File.ReadAllBytes(fixture.Path);
        var manifest = new StructuralInsertionManifest("Ledger", 8, 6, 6, 10, [new StructuralRecord([new(1, "text", "Other Tools Ltd"), new(3, "text", "NEW"), new(5, "decimal", "25")])]);
        var bytes = StructuralRowCandidate.Build(before, manifest, _ => { }, default);
        using var output = new MemoryStream(); output.Write(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
            var s = StructuralPreservationOracle.S;
            XElement Cell(string address) => doc.Descendants(s + "c").Single(c => (string?)c.Attribute("r") == address);
            if (defect == "absolute-reference") Cell("E27").Element(s + "f")!.Value = "$E$10+E$23+$E6";
            if (defect == "missed-row-shift") Cell("A29").SetAttributeValue("r", "A28");
            if (defect == "incomplete-subtotal") Cell("E11").Element(s + "f")!.Value = "SUM(E6:E7)";
            if (defect == "duplicate-record") { var cell = Cell("C8"); cell.RemoveNodes(); cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(s + "is", new XElement(s + "t", "R2"))); }
            entry.Delete(); using var target = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); doc.Save(target);
        }
        Assert.IsFalse(StructuralPreservationOracle.Evaluate(before, manifest, output.ToArray()).Accepted, defect);
    }
}
