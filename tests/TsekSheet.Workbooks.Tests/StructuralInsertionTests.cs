using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class StructuralInsertionTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.Get("synthetic-payables");
    private static WorkflowRecord Record(string vendor = "Other Tools Ltd", string id = "one") => new(id,
        new Dictionary<string, string> { ["vendor"] = vendor, ["rfp"] = "NEW-" + id, ["amount"] = "123.45", ["date"] = "2026-10-29" });
    private static WorkflowPlanRequest Request(IWorkbookSession session, params WorkflowRecord[] records) => new(session.Info.SessionId,
        session.Info.Worksheets[0].Id, session.Info.Revision, Profile.Id, WorkflowProfiles.Hash(Profile), records.Length == 0 ? [Record()] : records);
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    private sealed class Fixture : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-structural-" + Guid.NewGuid().ToString("N") + ".xlsx");
        public Fixture(string feature = "plain")
        {
            using var wb = new XLWorkbook(); var sheet = wb.AddWorksheet("Ledger");
            var headers = new[] { "Vendor name", "Particulars", "RFP number", "Invoice number", "Amount", "Record date" };
            for (var c = 1; c <= 6; c++) sheet.Cell(2, c).Value = headers[c - 1];
            sheet.Cell("A5").Value = "2025 PAYABLES (INTERNAL)";
            sheet.Cell("A15").Value = "2025 PAYABLES (EXTERNAL)";
            Add(6, "Acme Trading Incorporated", "R1"); Add(7, "Other Tools Ltd", "R2");
            Add(16, "Cross Vendor Ltd", "R3"); Add(17, "Acme Trading Incorporated", "R4");
            sheet.Cell("A10").Value = "INTERNAL SUBTOTAL"; sheet.Cell("E10").FormulaA1 = "SUM(E6:E9)";
            sheet.Cell("A22").Value = "EXTERNAL SUBTOTAL"; sheet.Cell("E22").FormulaA1 = "SUM(E16:E21)";
            sheet.Cell("A25").Value = "Summary"; sheet.Cell("E25").FormulaA1 = "E10+E22";
            sheet.Cell("E26").FormulaA1 = "$E$10+E$22+$E6";
            sheet.Cell("E27").FormulaA1 = "E6+E7";
            sheet.Cell("A28").Value = "Unrelated lower content";
            wb.AddWorksheet("Context").Cell("A1").Value = "Unchanged context";
            switch (feature)
            {
                case "name": wb.DefinedNames.Add("Amounts", sheet.Range("E6:E9")); break;
                case "local-name": sheet.DefinedNames.Add("LocalAmounts", sheet.Range("E6:E9")); break;
                case "table": sheet.Range("H1:I2").CreateTable(); break;
                case "merge": sheet.Range("A1:F1").Merge(); break;
                case "crossing-merge": sheet.Range("G7:G9").Merge(); break;
                case "filter": sheet.Range("A2:F7").SetAutoFilter(); break;
                case "hidden-row": sheet.Row(7).Hide(); break;
                case "hidden-column": sheet.Column(2).Hide(); break;
                case "validation": sheet.Cell("E7").CreateDataValidation().WholeNumber.Between(0, 100); break;
                case "conditional": sheet.Cell("E7").AddConditionalFormat().WhenGreaterThan(0).Fill.SetBackgroundColor(XLColor.Red); break;
                case "cross-sheet": wb.Worksheet("Context").Cell("A2").FormulaA1 = "Ledger!E10"; break;
                case "unsupported-formula": sheet.Cell("E27").FormulaA1 = "IF(E6>0,E6,0)"; break;
                case "vendor-sum": sheet.Cell("E27").FormulaA1 = "SUM(E6:E7)"; break;
                case "unsafe-template": sheet.Cell("E7").Style.Font.Bold = true; break;
                case "formula-template": sheet.Cell("G7").FormulaA1 = "E7"; break;
                case "bad-subtotal": sheet.Cell("E10").FormulaA1 = "SUM(E6:E7)"; break;
                case "missing-subtotal": sheet.Cell("A10").Clear(); break;
                case "duplicate-heading": sheet.Cell("A4").Value = "2025 PAYABLES (INTERNAL)"; break;
                case "1904": wb.Use1904DateSystem = true; break;
            }
            using var output = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite);
            wb.SaveAs(output, new ClosedXML.Excel.SaveOptions { GenerateCalculationChain = false });
            void Add(int row, string vendor, string id)
            {
                sheet.Cell(row, 1).Value = vendor; sheet.Cell(row, 2).Value = "Original particulars"; sheet.Cell(row, 3).Value = id;
                sheet.Cell(row, 4).Value = "INV-" + id; sheet.Cell(row, 5).Value = 100;
                sheet.Cell(row, 6).Value = new DateTime(2024, 1, 2);
                sheet.Cell(row, 7).Value = "PAID"; sheet.Cell(row, 8).Value = "OLD-VOUCHER";
                sheet.Row(row).Height = 22;
                sheet.Range(row, 1, row, 8).Style.Font.FontName = "Arial";
                sheet.Range(row, 1, row, 8).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet.Range(row, 1, row, 8).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00";
                sheet.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightBlue;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "yyyy-mm-dd";
            }
        }
        public void Dispose() => File.Delete(Path);
    }

    private static WorkbookSession Copy(WorkbookEngine engine, IWorkbookSession main) => (WorkbookSession)main.CreateWorkingCopy(main.Info.Worksheets[0].Id, main.Info.Revision);
    private static CellInfo Cell(IWorkbookSession session, int row, int col) => session.ReadViewport(session.Info.Worksheets[0].Id, row, col, 1, 1).Cells[0];

    [TestMethod]
    public void InsertsActualCellsWithLiteralReferenceExpectationsAndCleanStyles()
    {
        using var f = new Fixture(); var hash = Hash(f.Path); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true };
        using var main = engine.Open(f.Path); using var copy = Copy(engine, main); var request = Request(copy); var plan = WorkflowPlanning.Prepare(copy, request);
        Assert.AreEqual(8, plan.Records[0].BaseInsertionRow);
        var result = copy.ExperimentInsert(request, plan, true, true);
        Assert.IsTrue(result.Report.Accepted); Assert.AreEqual(1, copy.Info.Revision); Assert.IsTrue(copy.Info.IsDirty);
        Assert.AreEqual("Other Tools Ltd", Cell(copy, 8, 1).Value); Assert.AreEqual("NEW-one", Cell(copy, 8, 3).Value);
        Assert.AreEqual("123.45", Cell(copy, 8, 5).Value); Assert.AreEqual("10/29/2026 00:00:00", Cell(copy, 8, 6).Value);
        Assert.AreEqual("", Cell(copy, 8, 4).Value); Assert.AreEqual("", Cell(copy, 8, 7).Value); Assert.AreEqual("", Cell(copy, 8, 8).Value);
        Assert.AreEqual(Cell(copy, 6, 5).Format, Cell(copy, 8, 5).Format);
        Assert.AreEqual("SUM(E6:E10)", Cell(copy, 11, 5).Formula);
        Assert.AreEqual("SUM(E17:E22)", Cell(copy, 23, 5).Formula);
        Assert.AreEqual("E11+E23", Cell(copy, 26, 5).Formula);
        Assert.AreEqual("$E$11+E$23+$E6", Cell(copy, 27, 5).Formula);
        Assert.AreEqual("E6+E7", Cell(copy, 28, 5).Formula);
        Assert.AreEqual("Unrelated lower content", Cell(copy, 29, 1).Value);
        Assert.AreEqual("R2", Cell(copy, 7, 3).Value); Assert.AreEqual(0, main.Info.Revision); Assert.AreEqual(hash, Hash(f.Path));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan, true, true));
        var integration = new WorksheetIntegrationRequest(copy.Info.SessionId, main.Info.Worksheets[0].Id, 1, 0, 0);
        Assert.IsFalse(main.PreviewIntegration(copy, integration).CanApply);
        Assert.ThrowsExactly<WorkbookException>(() => copy.CreateSnapshot());
        Assert.ThrowsExactly<WorkbookException>(() => copy.SaveAsNew(f.Path + ".output.xlsx", 1));
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void BatchesAtSubtotalHaveStableInputOrderAndCanBeRepeated(bool differentVendors)
    {
        using var f = new Fixture(); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true };
        using var main = engine.Open(f.Path); using var copy = Copy(engine, main);
        var request = Request(copy, Record("Novel Company", "one"), Record(differentVendors ? "Distinct Supplier" : "Novel Company", "two"));
        var result = copy.ExperimentInsert(request, WorkflowPlanning.Prepare(copy, request), true, true);
        Assert.AreEqual(22, result.Manifest.Boundary); Assert.AreEqual("NEW-one", Cell(copy, 22, 3).Value); Assert.AreEqual("NEW-two", Cell(copy, 23, 3).Value);
        Assert.AreEqual("SUM(E16:E23)", Cell(copy, 24, 5).Formula); Assert.AreEqual("E10+E24", Cell(copy, 27, 5).Formula);
        var next = Request(copy, Record("Third Entity", "three")); copy.ExperimentInsert(next, WorkflowPlanning.Prepare(copy, next), true, true);
        Assert.AreEqual(2, copy.Info.Revision); Assert.AreEqual("NEW-three", Cell(copy, 24, 3).Value);
        Assert.AreEqual("SUM(E16:E24)", Cell(copy, 25, 5).Formula); Assert.AreEqual("E10+E25", Cell(copy, 28, 5).Formula);
    }

    [TestMethod]
    [DataRow("name")] [DataRow("local-name")] [DataRow("table")] [DataRow("merge")] [DataRow("crossing-merge")]
    [DataRow("filter")] [DataRow("hidden-row")] [DataRow("hidden-column")] [DataRow("validation")] [DataRow("conditional")]
    [DataRow("cross-sheet")] [DataRow("unsupported-formula")] [DataRow("unsafe-template")] [DataRow("formula-template")]
    [DataRow("bad-subtotal")] [DataRow("missing-subtotal")] [DataRow("duplicate-heading")] [DataRow("1904")] [DataRow("vendor-sum")]
    public void UnsupportedStructuresFailClosedAndPreserveSource(string feature)
    {
        using var f = new Fixture(feature); var hash = Hash(f.Path); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true };
        using var main = engine.Open(f.Path); var before = main.Info;
        Assert.ThrowsExactly<WorkbookException>(() =>
        {
            using var copy = Copy(engine, main); var request = Request(copy); var plan = WorkflowPlanning.Prepare(copy, request);
            try { copy.ExperimentInsert(request, plan, true, true); }
            finally { Assert.AreEqual(0, copy.Info.Revision); Assert.IsFalse(copy.Info.IsDirty); }
        });
        Assert.AreEqual(before, main.Info); Assert.AreEqual(hash, Hash(f.Path));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(2)] [DataRow(3)] [DataRow(4)] [DataRow(5)] [DataRow(6)] [DataRow(7)]
    public void EveryStageFaultAndCancellationRollBackWithoutContaminatingRetry(int faultStage)
    {
        foreach (var cancel in new[] { false, true })
        {
            using var f = new Fixture(); var hash = Hash(f.Path); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true };
            using var main = engine.Open(f.Path); using var copy = Copy(engine, main); var before = copy.Info;
            var request = Request(copy); var plan = WorkflowPlanning.Prepare(copy, request); using var cts = new CancellationTokenSource();
            engine.StructuralFaultForTesting = stage => { if ((int)stage != faultStage) return; if (cancel) cts.Cancel(); else throw new IOException("Injected stage failure"); };
            if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => copy.ExperimentInsert(request, plan, true, true, cts.Token));
            else Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan, true, true));
            Assert.AreEqual(before, copy.Info); Assert.AreEqual("SUM(E6:E9)", Cell(copy, 10, 5).Formula); Assert.AreEqual(hash, Hash(f.Path));
            engine.StructuralFaultForTesting = null; Assert.IsTrue(copy.ExperimentInsert(request, plan, true, true).Report.Accepted);
        }
    }

    [TestMethod]
    public void ApprovalProvenanceRevisionAmbiguityAndCapabilityAreEnforced()
    {
        using var f = new Fixture(); var engine = new WorkbookEngine(); using var main = engine.Open(f.Path); using var copy = Copy(engine, main);
        var request = Request(copy); var plan = WorkflowPlanning.Prepare(copy, request);
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan, true, true)); engine.StructuralExperimentsEnabled = true;
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan, false, true));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan with { SourceSha256 = "forged" }, true, true));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan with { Records = [] }, true, true));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request with { ExpectedRevision = 9 }, plan, true, true));
        var ambiguous = Request(copy, Record("Acme Trading Incorporated"));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(ambiguous, WorkflowPlanning.Prepare(copy, ambiguous), true, true));
        var resolved = ambiguous with { Records = [ambiguous.Records[0] with { SelectedSectionId = "internal" }] };
        Assert.IsTrue(copy.ExperimentInsert(resolved, WorkflowPlanning.Prepare(copy, resolved), true, true).Report.Accepted);
    }

    [TestMethod]
    public void DuplicateReviewAndMixedBoundaryBatchAreBlocked()
    {
        using var f = new Fixture(); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true }; using var main = engine.Open(f.Path); using var copy = Copy(engine, main);
        var duplicate = Request(copy, Record() with { Values = new Dictionary<string, string> { ["vendor"] = "Other Tools Ltd", ["rfp"] = "R2", ["amount"] = "5" } });
        var plan = WorkflowPlanning.Prepare(copy, duplicate); Assert.IsTrue(plan.Records[0].Duplicates.Count > 0);
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(duplicate, plan, true, false));
        var batch = Request(copy, Record(), Record("Novel Company", "two"));
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(batch, WorkflowPlanning.Prepare(copy, batch), true, true));
        Assert.AreEqual(0, copy.Info.Revision);
    }

    private static byte[] Rewrite(byte[] bytes, string part, Action<XDocument> edit)
    {
        using var output = new MemoryStream(); output.Write(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry(part)!; XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
            edit(doc); entry.Delete(); using var target = zip.CreateEntry(part).Open(); doc.Save(target);
        }
        return output.ToArray();
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void ActualCorruptOrSemanticallyTamperedCandidateRollsBackAndRetrySucceeds(bool corrupt)
    {
        using var f = new Fixture(); var hash = Hash(f.Path); var engine = new WorkbookEngine { StructuralExperimentsEnabled = true };
        using var main = engine.Open(f.Path); using var copy = Copy(engine, main); var before = copy.Info;
        // Prior recorded edit moves from A25 to A26; cell-only Merge must reject without trying old coordinates.
        copy.Edit(new(copy.Info.Worksheets[0].Id, 25, 1, "text", "Reviewed summary label", 0));
        var request = Request(copy); var plan = WorkflowPlanning.Prepare(copy, request); before = copy.Info;
        engine.CandidateFaultForTesting = bytes => corrupt ? [0, 1, 2] : Rewrite(bytes, "xl/worksheets/sheet1.xml", doc =>
            doc.Descendants(StructuralPreservationOracle.S + "c").Single(c => (string?)c.Attribute("r") == "E6").Element(StructuralPreservationOracle.S + "v")!.Value = "999");
        Assert.ThrowsExactly<WorkbookException>(() => copy.ExperimentInsert(request, plan, true, true));
        Assert.AreEqual(before, copy.Info); Assert.AreEqual("100", Cell(copy, 6, 5).Value); Assert.AreEqual(hash, Hash(f.Path));
        engine.CandidateFaultForTesting = null; copy.ExperimentInsert(request, plan, true, true);
        var integration = new WorksheetIntegrationRequest(copy.Info.SessionId, main.Info.Worksheets[0].Id, 2, 0, 0);
        Assert.IsFalse(main.PreviewIntegration(copy, integration).CanApply);
        Assert.AreEqual("Reviewed summary label", Cell(copy, 26, 1).Value);
    }

    [TestMethod]
    [DataRow("shared")] [DataRow("array")] [DataRow("spill")] [DataRow("broken-reference")]
    [DataRow("duplicate-cell")] [DataRow("blank-template")] [DataRow("extra-part")]
    public void StandaloneOracleRejectsUnsupportedSourceAndUnsafeManifests(string feature)
    {
        using var f = new Fixture(); var before = File.ReadAllBytes(f.Path); var s = StructuralPreservationOracle.S;
        var manifest = new StructuralInsertionManifest("Ledger", 22, feature == "blank-template" ? 21 : 16, 16, 22,
            [new StructuralRecord([new(1, "text", "Novel Company"), new(5, "decimal", "25")])]);
        if (feature == "extra-part")
        {
            using var stream = new MemoryStream(); stream.Write(before);
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, true))
            using (var writer = new StreamWriter(zip.CreateEntry("xl/unknown.xml").Open())) writer.Write("<unknown/>");
            before = stream.ToArray();
        }
        else if (feature != "blank-template") before = Rewrite(before, "xl/worksheets/sheet1.xml", doc =>
        {
            var formula = doc.Descendants(s + "f").First();
            if (feature is "shared" or "array") { formula.SetAttributeValue("t", feature); formula.SetAttributeValue("ref", "E10:E11"); }
            if (feature == "spill") formula.Value = "E6#";
            if (feature == "broken-reference") formula.Value = "#REF!";
            if (feature == "duplicate-cell") { var row = doc.Descendants(s + "row").First(); row.Add(new XElement(row.Elements(s + "c").First())); }
        });
        Assert.IsFalse(StructuralPreservationOracle.Evaluate(before, manifest, before).Accepted);
    }

    [TestMethod]
    public void CapabilityPreflightSeparatesUnsupportedAndExperimentalWithoutEnablingApply()
    {
        foreach (var feature in new[] { "plain", "name", "merge", "table" })
        {
            using var f = new Fixture(feature); using var main = new WorkbookEngine().Open(f.Path);
            var plan = WorkflowPlanning.Prepare(main, Request(main));
            Assert.AreEqual(feature == "plain" ? "experimental-envelope-only" : "unsupported-structure", plan.StructuralStatus);
            Assert.IsFalse(plan.CanApply); Assert.AreEqual(0, main.Info.Revision); Assert.IsFalse(main.Info.IsDirty);
        }
    }

    [TestMethod]
    [DataRow("content")] [DataRow("formula")] [DataRow("style")] [DataRow("height")] [DataRow("other-sheet")]
    [DataRow("name")] [DataRow("merge")] [DataRow("relationship")] [DataRow("corrupt")]
    public void IndependentOracleRejectsUnexplainedCandidateChanges(string mutation)
    {
        using var f = new Fixture(); var before = File.ReadAllBytes(f.Path);
        var manifest = new StructuralInsertionManifest("Ledger", 8, 6, 6, 10,
            [new StructuralRecord([new(1, "text", "Other Tools Ltd"), new(3, "text", "NEW"), new(5, "decimal", "123.45")])]);
        var candidate = StructuralRowCandidate.Build(before, manifest, _ => { }, default); var s = StructuralPreservationOracle.S;
        Assert.IsTrue(StructuralPreservationOracle.Evaluate(before, manifest, candidate).Accepted);
        var corrupt = mutation == "corrupt" ? [0, 1, 2] : Rewrite(candidate, mutation switch { "other-sheet" => "xl/worksheets/sheet2.xml", "name" => "xl/workbook.xml", "relationship" => "xl/_rels/workbook.xml.rels", _ => "xl/worksheets/sheet1.xml" }, doc =>
        {
            switch (mutation)
            {
                case "content": doc.Descendants(s + "c").First(c => (string?)c.Attribute("r") == "E6").Element(s + "v")!.Value = "999"; break;
                case "formula": doc.Descendants(s + "f").First().Value = "SUM(E6:E9)"; break;
                case "style": doc.Descendants(s + "c").First(c => (string?)c.Attribute("r") == "E8").SetAttributeValue("s", "0"); break;
                case "height": doc.Descendants(s + "row").First().SetAttributeValue("ht", "90"); break;
                case "other-sheet": doc.Descendants(s + "c").First().SetAttributeValue("s", "1"); break;
                case "name": doc.Root!.Element(s + "calcPr")!.AddBeforeSelf(new XElement(s + "definedNames", new XElement(s + "definedName", new XAttribute("name", "Injected"), "Ledger!$E$8"))); break;
                case "merge": doc.Root!.Element(s + "sheetData")!.AddAfterSelf(new XElement(s + "mergeCells", new XElement(s + "mergeCell", new XAttribute("ref", "A8:B8")))); break;
                case "relationship": doc.Root!.Elements().First().SetAttributeValue("Target", "worksheets/missing.xml"); break;
            }
        });
        Assert.IsFalse(StructuralPreservationOracle.Evaluate(before, manifest, corrupt).Accepted);
    }

    [TestMethod]
    public void OracleAcceptsHandSpecifiedGoldenRowsWithoutCallingCandidateEditor()
    {
        using var f = new Fixture(); var before = File.ReadAllBytes(f.Path); var s = StructuralPreservationOracle.S;
        var manifest = new StructuralInsertionManifest("Ledger", 22, 16, 16, 22, [new StructuralRecord([new(1, "text", "Novel Company"), new(5, "decimal", "25")])]);
        // Explicit fixture expectations, independent of either translation implementation.
        var golden = Rewrite(before, "xl/worksheets/sheet1.xml", doc =>
        {
            var map = new Dictionary<int, int> { [22] = 23, [25] = 26, [26] = 27, [27] = 28, [28] = 29 };
            var data = doc.Root!.Element(s + "sheetData")!;
            foreach (var row in data.Elements(s + "row"))
                if (map.TryGetValue((int)row.Attribute("r")!, out var final))
                {
                    row.SetAttributeValue("r", final);
                    foreach (var c in row.Elements(s + "c")) c.SetAttributeValue("r", new string(((string)c.Attribute("r")!).TakeWhile(char.IsLetter).ToArray()) + final);
                }
            var formulas = new Dictionary<string, string> { ["E10"] = "SUM(E6:E9)", ["E23"] = "SUM(E16:E22)", ["E26"] = "E10+E23", ["E27"] = "$E$10+E$23+$E6", ["E28"] = "E6+E7" };
            foreach (var cell in data.Descendants(s + "c").Where(c => c.Element(s + "f") != null)) { cell.Element(s + "f")!.Value = formulas[(string)cell.Attribute("r")!]; cell.Elements(s + "v").Remove(); }
            var template = data.Elements(s + "row").Single(r => (int)r.Attribute("r")! == 16); var inserted = new XElement(template); inserted.SetAttributeValue("r", 22); inserted.Attribute("spans")?.Remove();
            foreach (var cell in inserted.Elements(s + "c"))
            {
                var col = new string(((string)cell.Attribute("r")!).TakeWhile(char.IsLetter).ToArray()); cell.SetAttributeValue("r", col + "22"); cell.RemoveNodes(); cell.Attribute("t")?.Remove();
                if (col == "A") { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(s + "is", new XElement(s + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), "Novel Company"))); }
                if (col == "E") cell.Add(new XElement(s + "v", "25"));
            }
            data.Add(inserted); data.ReplaceNodes(data.Elements().OrderBy(r => (int)r.Attribute("r")!).ToArray());
            doc.Root.Element(s + "dimension")?.SetAttributeValue("ref", "A1:H29");
        });
        golden = Rewrite(golden, "xl/workbook.xml", doc => { var calc = doc.Root!.Element(s + "calcPr")!; calc.SetAttributeValue("fullCalcOnLoad", "1"); calc.SetAttributeValue("forceFullCalc", "1"); });
        var report = StructuralPreservationOracle.Evaluate(before, manifest, golden);
        Assert.IsTrue(report.Accepted, string.Join("; ", report.Differences));
    }
}
