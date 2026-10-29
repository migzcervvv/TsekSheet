using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

internal sealed record FormattingStep(int Boundary, int Count, int Template, int FirstData, int Subtotal);
internal sealed record FormattingCase(string Id, string Height, string Mode, string Feature, FormattingStep[] Steps, Dictionary<string,string> ExpectedFormulas);
internal sealed record FormattingSpecification(int SchemaVersion, string Provenance, FormattingCase[] Cases);
internal sealed record FormattingArtifact(FormattingCase Spec, string InputFile, string InputSha256, string CandidateFile, string CandidateSha256, StructuralInsertionManifest[] Operations);
internal sealed record FormattingIndex(int SchemaVersion, string FixtureKind, string SpecificationSha256, string ScriptSha256, FormattingArtifact[] Cases);

[TestClass]
public sealed class ExcelFormattingReferenceTests
{
    internal static string DirectoryPath => Path.Combine(ReferenceCorpusTests.Root, "artifacts/step45-reference/formatting-v1");
    internal static string Retained => Path.Combine(ReferenceCorpusTests.Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedFormatting");
    private static XNamespace S => StructuralPreservationOracle.S;
    internal static byte[] Rewrite(byte[] bytes, string part, Action<XDocument> edit)
    {
        using var output = new MemoryStream(); output.Write(bytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry(part)!; XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
            edit(doc); entry.Delete(); using var target = zip.CreateEntry(part).Open(); doc.Save(target, System.Xml.Linq.SaveOptions.DisableFormatting);
        }
        return output.ToArray();
    }
    private static object RowFacts(string path)
    {
        var parts = StructuralPreservationOracle.Read(File.ReadAllBytes(path), default);
        var doc = StructuralPreservationOracle.Xml(parts["xl/worksheets/sheet1.xml"]);
        return new { defaults = StructuralPreservationOracle.Canonical(doc.Root!.Element(S + "sheetFormatPr")!),
            rows = doc.Descendants(S + "row").Where(r => r.Attribute("ht") != null).Select(r => new { row = (int)r.Attribute("r")!, height = (string?)r.Attribute("ht"), customHeight = (string?)r.Attribute("customHeight"), attributes = string.Join(";",r.Attributes().OrderBy(a=>a.Name.ToString()).Select(a=>a.Name+"="+a.Value)) }).ToArray() };
    }
    [TestMethod]
    public void GenerateExpandedFormattingCorpusWithLiteralExpectations()
    {
        var specPath = Path.Combine(ReferenceCorpusTests.Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/formatting-v1.json");
        var spec = JsonSerializer.Deserialize<FormattingSpecification>(File.ReadAllText(specPath), ReferenceCorpusTests.Json)!;
        Directory.CreateDirectory(DirectoryPath);
        if (!File.Exists(Path.Combine(DirectoryPath, "formatting.index.json")) && File.Exists(Path.Combine(Retained, "formatting.index.json")))
            foreach (var file in Directory.GetFiles(Retained).Where(f => Path.GetExtension(f) is ".json" or ".xlsx")) File.Copy(file, Path.Combine(DirectoryPath, Path.GetFileName(file)), false);
        var indexPath = Path.Combine(DirectoryPath, "formatting.index.json");
        var prior = File.Exists(indexPath) ? JsonSerializer.Deserialize<FormattingIndex>(File.ReadAllText(indexPath), ReferenceCorpusTests.Json) : null;
        var artifacts = new List<FormattingArtifact>();
        foreach (var item in spec.Cases)
        {
            var inputFile = item.Id + ".input.xlsx"; var inputPath = Path.Combine(DirectoryPath, inputFile);
            if (!File.Exists(inputPath))
            {
                var source = File.ReadAllBytes(Path.Combine(ReferenceCorpusTests.Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel/before-subtotal.input.xlsx"));
                source = Rewrite(source, "xl/worksheets/sheet1.xml", doc =>
                {
                    foreach (var row in doc.Descendants(S + "row").Where(r => r.Attribute("ht") != null)) row.SetAttributeValue("ht", item.Height);
                    if (item.Feature == "blank-template")
                    {
                        var blank = new XElement(doc.Descendants(S + "row").Single(r => (int)r.Attribute("r")! == 16)); blank.SetAttributeValue("r", 21);
                        foreach (var cell in blank.Elements(S + "c")) { cell.SetAttributeValue("r", new string(((string)cell.Attribute("r")!).TakeWhile(char.IsLetter).ToArray()) + "21"); cell.RemoveNodes(); cell.Attribute("t")?.Remove(); }
                        var data = doc.Root!.Element(S + "sheetData")!; data.Add(blank); data.ReplaceNodes(data.Elements().OrderBy(r => (int)r.Attribute("r")!).ToArray());
                    }
                });
                if (item.Feature is "currency" or "decorated") source = Rewrite(source, "xl/styles.xml", doc =>
                {
                    if (item.Feature == "currency") foreach (var format in doc.Descendants(S + "numFmt").Where(f => (string?)f.Attribute("formatCode") == "#,##0.00")) format.SetAttributeValue("formatCode", "\"USD \"#,##0.0000");
                    else
                    {
                        foreach (var font in doc.Descendants(S + "font").Where(f => (string?)f.Element(S + "name")?.Attribute("val") == "Arial")) { font.Element(S + "sz")!.SetAttributeValue("val", "12"); font.AddFirst(new XElement(S + "b")); }
                        foreach (var fill in doc.Descendants(S + "fgColor").Where(f => (string?)f.Attribute("rgb") == "FFADD8E6")) fill.SetAttributeValue("rgb", "FFFFE699");
                        foreach (var border in doc.Descendants(S + "bottom").Where(b => (string?)b.Attribute("style") == "thin")) border.SetAttributeValue("style", "medium");
                        foreach (var alignment in doc.Descendants(S + "alignment")) alignment.SetAttributeValue("horizontal", "right");
                    }
                });
                using var file = new FileStream(inputPath, FileMode.CreateNew); file.Write(source);
            }
            var bytes = File.ReadAllBytes(inputPath); var operations = new List<StructuralInsertionManifest>(); var offset = 0;
            foreach (var step in item.Steps)
            {
                var records = Enumerable.Range(offset + 1, step.Count).Select(i => new StructuralRecord([new(1,"text","Artificial Vendor " + i), new(2,"text","New artificial record"),new(3,"text","NEW-" + i),new(4,"text",""),new(5,"decimal","25"),new(6,"date","2026-10-29")])).ToArray();
                var manifest = new StructuralInsertionManifest("Ledger", step.Boundary, step.Template, step.FirstData, step.Subtotal, records);
                var after = StructuralRowCandidate.Build(bytes, manifest, _ => { }, default);
                Assert.IsTrue(StructuralPreservationOracle.Evaluate(bytes, manifest, after).Accepted, item.Id); bytes = after; operations.Add(manifest); offset += step.Count;
            }
            var outputFile = item.Id + ".tseksheet.xlsx"; var outputPath = Path.Combine(DirectoryPath, outputFile);
            if (!File.Exists(outputPath)) { using var output = new FileStream(outputPath, FileMode.CreateNew); output.Write(bytes); }
            using (var workbook = new XLWorkbook(new MemoryStream(bytes, false))) foreach (var (address, formula) in item.ExpectedFormulas) Assert.AreEqual(formula, workbook.Worksheet("Ledger").Cell(address).FormulaA1, item.Id);
            var artifact = new FormattingArtifact(item, inputFile, ReferenceCorpusFixture.Hash(inputPath), outputFile, ReferenceCorpusFixture.Hash(outputPath), operations.ToArray());
            if (prior != null) Assert.AreEqual(JsonSerializer.Serialize(prior.Cases.Single(c => c.Spec.Id == item.Id), ReferenceCorpusTests.Json), JsonSerializer.Serialize(artifact, ReferenceCorpusTests.Json));
            artifacts.Add(artifact);
        }
        var index = new FormattingIndex(1, "generated-artificial-formatting-only", ReferenceCorpusFixture.Hash(specPath), ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root, "scripts/Verify-ExcelFormattingReferences.ps1")), artifacts.ToArray());
        if (prior == null) { using var writer = new StreamWriter(new FileStream(indexPath, FileMode.CreateNew)); writer.Write(JsonSerializer.Serialize(index, ReferenceCorpusTests.Json)); }
        else Assert.AreEqual(JsonSerializer.Serialize(prior, ReferenceCorpusTests.Json), JsonSerializer.Serialize(index, ReferenceCorpusTests.Json));
        Assert.AreEqual(17, artifacts.Count); Console.WriteLine("FORMATTING_CORPUS=" + DirectoryPath);
    }
    [TestMethod]
    public void NativeFormattingEvidenceUsesExactHeightsAndReportsEveryDifference()
    {
        var evidencePath = Path.Combine(DirectoryPath, "excel-formatting-evidence.json");
        if (!File.Exists(evidencePath)) Assert.Inconclusive("Expanded native Excel formatting references pending.");
        var index = JsonSerializer.Deserialize<FormattingIndex>(File.ReadAllText(Path.Combine(DirectoryPath, "formatting.index.json")), ReferenceCorpusTests.Json)!;
        using var evidence = JsonDocument.Parse(File.ReadAllText(evidencePath)); var root = evidence.RootElement;
        Assert.AreEqual(ReferenceCorpusFixture.Hash(Path.Combine(DirectoryPath, "formatting.index.json")), root.GetProperty("indexSha256").GetString());
        Assert.AreEqual(index.ScriptSha256, root.GetProperty("scriptSha256").GetString());
        var results = new List<object>();
        foreach (var item in root.GetProperty("cases").EnumerateArray())
        {
            var entry = index.Cases.Single(c => c.Spec.Id == item.GetProperty("id").GetString());
            var excel = Path.Combine(DirectoryPath, entry.Spec.Id + ".excel.xlsx");
            Assert.AreEqual(entry.InputSha256, ReferenceCorpusFixture.Hash(Path.Combine(DirectoryPath, entry.InputFile)));
            Assert.AreEqual(item.GetProperty("outputSha256").GetString(), ReferenceCorpusFixture.Hash(excel));
            var expected = ReferenceCorpusFixture.Semantics(Path.Combine(DirectoryPath, entry.CandidateFile)); var actual = ReferenceCorpusFixture.Semantics(excel);
            var differences = expected.Keys.Union(actual.Keys).Where(k => expected.GetValueOrDefault(k) != actual.GetValueOrDefault(k)).Select(k => k + ": " + expected.GetValueOrDefault(k) + " <> " + actual.GetValueOrDefault(k)).ToArray();
            var core = differences.Where(d => !d.StartsWith("height/", StringComparison.Ordinal)).ToArray(); Assert.IsEmpty(core, entry.Spec.Id + ": " + string.Join(";", core.Take(8)));
            var heights=differences.Where(d=>d.StartsWith("height/",StringComparison.Ordinal)).ToArray();
            if(entry.Spec.Height is "20" or "22")
            {
                Assert.IsNotEmpty(heights);
                foreach(var key in expected.Keys.Where(k=>k.StartsWith("height/",StringComparison.Ordinal)&&expected.GetValueOrDefault(k)!=actual.GetValueOrDefault(k)))
                { Assert.AreEqual(entry.Spec.Height,expected[key]);Assert.AreEqual(entry.Spec.Height=="20"?"20.1":"21.95",actual[key]); }
            }
            else Assert.IsEmpty(heights,"Exact native height preservation failed: "+entry.Spec.Id);
            var candidateFacts=JsonSerializer.SerializeToElement(RowFacts(Path.Combine(DirectoryPath,entry.CandidateFile)),ReferenceCorpusTests.Json);
            var excelFacts=JsonSerializer.SerializeToElement(RowFacts(excel),ReferenceCorpusTests.Json);
            if(entry.Spec.Height is "20" or "22")foreach(var rawRow in excelFacts.GetProperty("rows").EnumerateArray())
            {Assert.AreEqual(entry.Spec.Height=="20"?"20.100000000000001":"21.95",rawRow.GetProperty("height").GetString());Assert.AreEqual("1",rawRow.GetProperty("customHeight").GetString());}
            if(entry.Spec.Height=="21")foreach(var row in candidateFacts.GetProperty("rows").EnumerateArray())
            {
                var actualRow=excelFacts.GetProperty("rows").EnumerateArray().Single(r=>r.GetProperty("row").GetInt32()==row.GetProperty("row").GetInt32());
                Assert.AreEqual(row.GetProperty("height").GetString(),actualRow.GetProperty("height").GetString());
                Assert.AreEqual(row.GetProperty("customHeight").GetString(),actualRow.GetProperty("customHeight").GetString());
            }
            using (var workbook = new XLWorkbook(excel)) foreach (var (address, formula) in entry.Spec.ExpectedFormulas) Assert.AreEqual(formula, workbook.Worksheet("Ledger").Cell(address).FormulaA1);
            results.Add(new { entry.Spec.Id, entry.Spec.Height, entry.Spec.Mode, exactHeightMatch = !differences.Any(d => d.StartsWith("height/", StringComparison.Ordinal)), differences, source = RowFacts(Path.Combine(DirectoryPath, entry.InputFile)), candidate = RowFacts(Path.Combine(DirectoryPath, entry.CandidateFile)), excel = RowFacts(excel), qualification = differences.Length == 0 ? "compared projection exact" : "native saved height differs; blocked, no tolerance" });
        }
        Assert.AreEqual(17, root.GetProperty("cases").GetArrayLength());
        var verifier=ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root,"tests/TsekSheet.Workbooks.Tests/ExcelFormattingReferenceTests.cs"));
        var reportPath = Path.Combine(DirectoryPath, "formatting-comparison-"+verifier[..12]+".json"); var report = JsonSerializer.Serialize(new { verifierSha256=verifier, editorSha256=ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root,"src/TsekSheet.Workbooks/StructuralRowCandidate.cs")), oracleSha256=ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root,"src/TsekSheet.Workbooks/StructuralPreservationOracle.cs")), provenance = "actual Microsoft Excel, direct saved OOXML and independent read-only projection", results }, ReferenceCorpusTests.Json);
        if (!File.Exists(reportPath)) { using var writer = new StreamWriter(new FileStream(reportPath, FileMode.CreateNew)); writer.Write(report); } else Assert.AreEqual(File.ReadAllText(reportPath), report);
    }
}
