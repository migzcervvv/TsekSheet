using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class VerifiedStructuralPreflightWorkbookTests
{
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static VerifiedPlacementInput Input(WorkflowProfile p, string entity, string id)
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('C', 64), 2, 1);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, source), new(p.IdentifierFields[0], id, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "123.4500", source),
            new(p.ContextFields[1], "2026-10-29", source)]);
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
        return new(review, review.Snapshot.Version);
    }
    private static MappingDestination Destination(IWorkbookSession session, Guid sheet, WorkflowProfile p) =>
        new(session.Info.SessionId, sheet, session.Info.Revision, session.Info.SourceSha256, p.Id, WorkflowProfiles.Hash(p));
    private static string Cells(IWorkbookSession session) => JsonSerializer.Serialize(session.Info.Worksheets.Select(sheet =>
        new { sheet.Id, Cells = Enumerable.Range(0, (sheet.LastRow + 19) / 20).SelectMany(i =>
            session.ReadViewport(sheet.Id, 1 + i * 20, 1, Math.Min(20, sheet.LastRow - i * 20), sheet.LastColumn).Cells).ToArray() }));

    [TestMethod]
    [DataRow("synthetic-payables", 1)] [DataRow("synthetic-payables", 20)]
    [DataRow("synthetic-laboratory", 1)] [DataRow("synthetic-laboratory", 20)]
    public void PlainPreflightPreservesIndependentBindingsCleanAndDirtyCopiesAndEverySource(string profileId, int count)
    {
        var p = WorkflowProfiles.Get(profileId);
        foreach (var shift in new[] { 0, 20 })
        {
            using var fixture = new Fixture(p, shift);
            var hash = Hash(fixture.Path);
            using (var session = new WorkbookEngine().Open(fixture.Path))
            {
                var sheet = session.Info.Worksheets[0];
                var inputs = Enumerable.Range(0, count).Select(i => Input(p, "Novel Organization", "NEW-" + i)).ToArray();
                var reviews = JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot));
                var mainInfo = session.Info;
                var mainCells = Cells(session);
                using var clean = session.CreateWorkingCopy(sheet.Id, mainInfo.Revision);
                using var dirty = session.CreateWorkingCopy(sheet.Id, mainInfo.Revision);
                dirty.Edit(new(sheet.Id, 3, 2, "text", "Artificial copy note", dirty.Info.Revision));
                foreach (var target in new[] { session, clean, dirty })
                {
                    var before = target.Info;
                    var cells = Cells(target);
                    var changes = target.WorkingCopy?.Changes.ToArray();
                    var destination = Destination(target, sheet.Id, p);
                    var result = VerifiedPlacement.PreflightMany(target, destination, inputs);
                    Assert.AreEqual("experimental-envelope-only", result.Proposal.StructuralStatus, result.Proposal.InsertionBlockedReason);
                    Assert.IsFalse(result.CanApply);
                    Assert.IsFalse(result.Proposal.CanApply);
                    Assert.AreEqual("not-generated", result.Proposal.Review.Generation);
                    Assert.AreEqual("not-run", result.Proposal.Review.Oracle);
                    Assert.AreEqual("not-approved", result.Proposal.Review.Approval);
                    Assert.AreEqual("not-adopted", result.Proposal.Review.WorkingCopyAdoption);
                    Assert.AreEqual("production-disabled", result.Proposal.Review.Integration);
                    Assert.AreEqual("unmerged-changes-excluded", result.Proposal.Review.Export);
                    CollectionAssert.AreEqual(Enumerable.Range(22 + shift, count).ToArray(), result.Proposal.Records.Select(r => r.ProposedRow!.Value).ToArray());
                    for (var i = 0; i < count; i++)
                    {
                        var mapping = result.Mappings[i];
                        Assert.AreEqual(destination, mapping.Destination);
                        Assert.AreEqual(shift == 0 ? 1 : 6, mapping.Fields[0].Column);
                        Assert.AreEqual(22 + shift, mapping.Sections[1].SubtotalRow);
                        Assert.AreEqual(inputs[i].ExpectedReview, mapping.Review);
                        Assert.AreEqual(inputs[i].ExpectedReview.ReviewId.ToString("N"), result.Proposal.Records[i].RecordId);
                        Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
                        Assert.AreEqual(123.45m, mapping.Fields[4].Value.DecimalValue);
                        Assert.AreEqual(new DateOnly(2026, 10, 29), mapping.Fields[5].Value.DateValue);
                        Assert.AreEqual(inputs[i].Review.Snapshot.Fields[0].Source, mapping.Fields[0].Value.Source);
                    }
                    Assert.AreEqual("ROW_INSERTION_DISABLED", Assert.ThrowsExactly<WorkbookException>(() =>
                        target.InsertWorkflowRows(result.Proposal with { CanApply = true }, true, true)).Code);
                    Assert.AreEqual(before, target.Info);
                    Assert.AreEqual(cells, Cells(target));
                    if (changes != null) CollectionAssert.AreEqual(changes, target.WorkingCopy!.Changes.ToArray());
                }
                Assert.AreEqual(1, dirty.WorkingCopy!.Changes.Count);
                Assert.AreEqual(mainInfo, session.Info);
                Assert.AreEqual(mainCells, Cells(session));
                Assert.AreEqual(reviews, JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot)));
            }
            Assert.AreEqual(hash, Hash(fixture.Path));
        }
    }

    [TestMethod]
    [DataRow("synthetic-payables", "multi-boundary")]
    [DataRow("synthetic-laboratory", "multi-boundary")]
    [DataRow("synthetic-payables", "merge")]
    [DataRow("synthetic-laboratory", "merge")]
    [DataRow("synthetic-payables", "hidden-row")]
    [DataRow("synthetic-laboratory", "hidden-row")]
    [DataRow("synthetic-payables", "unsupported-formula")]
    [DataRow("synthetic-laboratory", "unsupported-formula")]
    [DataRow("synthetic-payables", "other-sheet-formula")]
    [DataRow("synthetic-laboratory", "other-sheet-formula")]
    public void UnsupportedPreflightRetainsEvidenceWithoutMutatingMainOrCopy(string profileId, string feature)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new Fixture(p, 20, feature);
        var hash = Hash(fixture.Path);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        using (var copy = session.CreateWorkingCopy(session.Info.Worksheets[0].Id, 0))
        {
            var inputs = feature == "multi-boundary" ? new[] { Input(p, "Other Tools Ltd", "NEW-A"), Input(p, "Novel Organization", "NEW-B") }
                : [Input(p, "Novel Organization", "NEW-A")];
            var reviews = JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot));
            foreach (var target in new[] { session, copy })
            {
                var before = target.Info;
                var cells = Cells(target);
                var result = VerifiedPlacement.PreflightMany(target, Destination(target, before.Worksheets[0].Id, p), inputs);
                Assert.IsTrue(result.Proposal.ReadyForReview);
                Assert.AreEqual("unsupported-structure", result.Proposal.StructuralStatus);
                Assert.IsFalse(result.CanApply);
                Assert.IsFalse(result.Proposal.CanApply);
                Assert.AreEqual("not-generated", result.Proposal.Review.Generation);
                StringAssert.Contains(result.Proposal.InsertionBlockedReason, feature switch
                {
                    "multi-boundary" => "Only one insertion boundary",
                    "merge" => "mergeCells",
                    "hidden-row" => "Hidden/grouped",
                    _ => "Only bounded same-sheet"
                });
                Assert.AreEqual(inputs.Length, result.Mappings.Count);
                Assert.AreEqual(before, target.Info);
                Assert.AreEqual(cells, Cells(target));
            }
            Assert.IsEmpty(copy.WorkingCopy!.Changes);
            Assert.AreEqual(reviews, JsonSerializer.Serialize(inputs.Select(i => i.Review.Snapshot)));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-verified-preflight-" + Guid.NewGuid().ToString("N") + ".xlsx");
        public Fixture(WorkflowProfile p, int shift, string feature = "plain")
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
                    sheet.Row(row).Height = 21;
                }
            }
            sheet.Cell(25 + shift, Column(0)).Value = "Unrelated footer";
            var column = XLHelper.GetColumnLetterFromNumber(Column(4));
            sheet.Cell(25 + shift, Column(4)).FormulaA1 = $"{column}{10 + shift}+{column}{22 + shift}";
            var context = wb.AddWorksheet("Unselected context");
            context.Cell(1, 1).Value = "Independent constant context";
            if (feature == "merge") sheet.Range(1, 1, 1, 6).Merge().FirstCell().Value = "Merged heading";
            if (feature == "hidden-row") sheet.Row(7 + shift).Hide();
            if (feature == "unsupported-formula") sheet.Cell(26 + shift, Column(4)).FormulaA1 = $"IF({column}{6 + shift}>0,1,0)";
            if (feature == "other-sheet-formula") context.Cell(2, 1).FormulaA1 = "A1+A1";
            using var output = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite);
            wb.SaveAs(output, new ClosedXML.Excel.SaveOptions { GenerateCalculationChain = false, EvaluateFormulasBeforeSaving = false });
        }
        public void Dispose() => File.Delete(Path);
    }
}
