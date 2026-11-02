using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class WorksheetAnalysisContextTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.Get("synthetic-payables");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static WorkflowPlanRequest Request(IWorkbookSession session) => new(session.Info.SessionId, session.Info.Worksheets[0].Id,
        session.Info.Revision, Profile.Id, WorkflowProfiles.Hash(Profile),
        [new("synthetic-input", new Dictionary<string, string> { ["vendor"] = "Other Tools Ltd", ["rfp"] = "NEW", ["amount"] = "125.50" })]);

    [TestMethod]
    [DataRow(9, "", false)]
    [DataRow(9, "125.5", true)]
    [DataRow(20, "", true)]
    [DataRow(20, "0", false)]
    public void FormulaContextWithoutIdentifiersRejectsPlanningAndPreservesSessionCopyAndSource(int row, string cache, bool hidden)
    {
        using var fixture = new PayablesFixture();
        var fixtureHash = Hash(fixture.Path);
        var path = fixture.Path + ".context.xlsx";
        try
        {
            CreateInput(fixture.Path, path, row, "Artificial unresolved entity", hidden);
            SetFormulaCache(path, row, cache);
            var sourceHash = Hash(path);
            using (var session = new WorkbookEngine().Open(path))
            using (var copy = session.CreateWorkingCopy(session.Info.Worksheets[0].Id, 0))
            {
                foreach (var target in new[] { session, copy })
                {
                    var before = target.Info;
                    var sheet = before.Worksheets[0].Id;
                    var cells = target.ReadViewport(sheet, row, 1, 1, 6).Cells.ToArray();
                    var context = cells.Single(c => c.Column == 5);
                    Assert.AreEqual("E6", context.Formula);
                    Assert.AreEqual(cache, context.Value);
                    Assert.IsTrue(cells.Where(c => c.Column is 3 or 4).All(c => string.IsNullOrWhiteSpace(c.Value) && c.Formula == null));

                    Assert.AreEqual("AMBIGUOUS_RECORD", Assert.ThrowsExactly<WorkbookException>(() => WorkflowPlanning.Prepare(target, Request(target))).Code);

                    Assert.AreEqual(before, target.Info);
                    Assert.AreEqual(0, target.Info.Revision);
                    Assert.IsFalse(target.Info.IsDirty);
                    CollectionAssert.AreEqual(cells, target.ReadViewport(sheet, row, 1, 1, 6).Cells.ToArray());
                }
                Assert.IsEmpty(copy.WorkingCopy!.Changes);
            }
            Assert.AreEqual(sourceHash, Hash(path));
            Assert.AreEqual(fixtureHash, Hash(fixture.Path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    [TestMethod]
    public void OutsideSectionFormulaSummaryAndConfiguredSubtotalsRemainReviewOnly()
    {
        using var fixture = new PayablesFixture();
        var fixtureHash = Hash(fixture.Path);
        var path = fixture.Path + ".summary.xlsx";
        try
        {
            CreateInput(fixture.Path, path, 25, "Artificial summary", true);
            SetFormulaCache(path, 25, "");
            var sourceHash = Hash(path);
            using (var session = new WorkbookEngine().Open(path))
            {
                var before = session.Info;
                var plan = WorkflowPlanning.Prepare(session, Request(session));
                Assert.IsTrue(plan.ReadyForReview);
                Assert.IsFalse(plan.CanApply);
                Assert.AreEqual(8, plan.Records.Single().BaseInsertionRow);
                Assert.AreEqual(before, session.Info);
            }
            Assert.AreEqual(sourceHash, Hash(path));
            Assert.AreEqual(fixtureHash, Hash(fixture.Path));
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void CreateInput(string fixturePath, string path, int row, string entity, bool hidden)
    {
        // Write a new artificial input only; leave the generated baseline fixture unchanged.
        using var workbook = new XLWorkbook(fixturePath);
        var sheet = workbook.Worksheet(1);
        sheet.Cell(row, 1).Value = entity;
        sheet.Cell(row, 5).FormulaA1 = "E6";
        if (hidden) sheet.Row(row).Hide();
        using var output = new FileStream(path, FileMode.CreateNew);
        workbook.SaveAs(output, new ClosedXML.Excel.SaveOptions { EvaluateFormulasBeforeSaving = false, GenerateCalculationChain = false });
    }

    private static void SetFormulaCache(string path, int row, string cache)
    {
        // Literal OOXML cache evidence avoids depending on formula calculation by the test library.
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        XDocument xml;
        using (var input = entry.Open()) xml = XDocument.Load(input);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var cell = xml.Descendants(ns + "c").Single(c => (string?)c.Attribute("r") == $"E{row}");
        cell.Element(ns + "v")?.Remove();
        if (cache != "") cell.Add(new XElement(ns + "v", cache));
        entry.Delete();
        using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
        xml.Save(output);
    }
}
