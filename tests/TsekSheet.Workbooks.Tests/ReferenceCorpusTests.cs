using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

internal sealed record ReferenceCase(string Id, string Feature, int Boundary, int Count, int Template, int FirstData, int Subtotal,
    bool Supported, string ExcelReference, Dictionary<string, string> ExpectedFormulas);
internal sealed record ReferenceSpecification(int SchemaVersion, string FixtureKind, string ExpectationProvenance, string Operation,
    string[] Invariants, ReferenceCase[] Cases);
internal sealed record ReferenceArtifact(ReferenceCase Spec, string InputFile, string InputSha256, string? CandidateFile,
    string? CandidateSha256, StructuralInsertionManifest Manifest, string CandidateProvenance);
internal sealed record ReferenceIndex(int SchemaVersion, string FixtureKind, string SpecificationSha256, string FixtureGeneratorSha256,
    string FixtureGenerator, string EditorSha256, string OracleSha256, string ExcelProcedureSha256, string DotnetVersion, ReferenceArtifact[] Cases);

[TestClass]
public sealed class ReferenceCorpusTests
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true, WriteIndented = true };
    internal static string Root
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "TsekSheet.sln"))) dir = dir.Parent;
            return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
        }
    }
    internal static string SpecPath => Path.Combine(Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/corpus-v1.json");
    internal static string CorpusDirectory
    {
        get
        {
            var inputs = new[] { SpecPath, Path.Combine(Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs"),
                Path.Combine(Root, "src/TsekSheet.Workbooks/StructuralRowCandidate.cs"), Path.Combine(Root, "src/TsekSheet.Workbooks/StructuralPreservationOracle.cs"),
                Path.Combine(Root, "scripts/Verify-ExcelReferenceCorpus.ps1") };
            var fingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Join("|", inputs.Select(ReferenceCorpusFixture.Hash)))));
            return Path.Combine(Root, "artifacts/step44-reference/corpus-v1-" + fingerprint[..12]);
        }
    }
    internal static StructuralInsertionManifest Manifest(ReferenceCase spec) => new("Ledger", spec.Boundary, spec.Template, spec.FirstData, spec.Subtotal,
        Enumerable.Range(1, spec.Count).Select(i => new StructuralRecord([new(1, "text", "Artificial Vendor " + i), new(2, "text", "New artificial record"),
            new(3, "text", "NEW-" + i), new(4, "text", ""), new(5, "decimal", "25"), new(6, "date", "2026-10-29")])).ToArray());

    [TestMethod]
    public void GenerateAndValidateProvenanceTrackedCorpus()
    {
        var specification = JsonSerializer.Deserialize<ReferenceSpecification>(File.ReadAllText(SpecPath), Json)!;
        Assert.AreEqual(1, specification.SchemaVersion); Assert.AreEqual("generated-artificial-ledger-only", specification.FixtureKind);
        Assert.AreEqual("hand-authored-OOXML-semantics-not-Excel", specification.ExpectationProvenance);
        Assert.AreEqual(11, specification.Cases.Length);
        Directory.CreateDirectory(CorpusDirectory);
        var indexPath = Path.Combine(CorpusDirectory, "corpus.index.json");
        var retained = Path.Combine(Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpus/VerifiedExcel");
        if (!File.Exists(indexPath) && File.Exists(Path.Combine(retained, "corpus.index.json")))
        {
            var seed = JsonSerializer.Deserialize<ReferenceIndex>(File.ReadAllText(Path.Combine(retained, "corpus.index.json")), Json)!;
            if (seed.SpecificationSha256 == ReferenceCorpusFixture.Hash(SpecPath)
                && seed.FixtureGeneratorSha256 == ReferenceCorpusFixture.Hash(Path.Combine(Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs"))
                && seed.ExcelProcedureSha256 == ReferenceCorpusFixture.Hash(Path.Combine(Root, "scripts/Verify-ExcelReferenceCorpus.ps1")))
                foreach (var file in Directory.GetFiles(retained).Where(f => Path.GetExtension(f) is ".xlsx" or ".json"))
                    File.Copy(file, Path.Combine(CorpusDirectory, Path.GetFileName(file)), overwrite: false);
        }
        var previous = File.Exists(indexPath) ? JsonSerializer.Deserialize<ReferenceIndex>(File.ReadAllText(indexPath), Json) : null;
        var artifacts = new List<ReferenceArtifact>();
        foreach (var spec in specification.Cases)
        {
            Assert.IsTrue(spec.ExcelReference.StartsWith("pending", StringComparison.Ordinal), "Specification must not invent Excel provenance.");
            var input = spec.Id + ".input.xlsx"; var inputPath = Path.Combine(CorpusDirectory, input);
            if (!File.Exists(inputPath)) { using var fixture = new ReferenceCorpusFixture(spec.Feature, inputPath); }
            var inputHash = ReferenceCorpusFixture.Hash(inputPath); var prior = previous?.Cases.Single(c => c.Spec.Id == spec.Id);
            if (prior != null) Assert.AreEqual(prior.InputSha256, inputHash, "Never regenerate a changed corpus input.");
            var bytes = File.ReadAllBytes(inputPath); var manifest = Manifest(spec);
            string? output = null; string? outputHash = null;
            if (spec.Supported)
            {
                var freshCandidate = StructuralRowCandidate.Build(bytes, manifest, _ => { }, default);
                output = spec.Id + ".tseksheet.xlsx"; var outputPath = Path.Combine(CorpusDirectory, output);
                if (!File.Exists(outputPath))
                {
                    using var file = new FileStream(outputPath, FileMode.CreateNew); file.Write(freshCandidate);
                }
                outputHash = ReferenceCorpusFixture.Hash(outputPath);
                if (prior != null) Assert.AreEqual(prior.CandidateSha256, outputHash);
                var report = StructuralPreservationOracle.Evaluate(bytes, manifest, freshCandidate);
                Assert.IsTrue(report.Accepted, string.Join("; ", report.Differences));
                using var workbook = new XLWorkbook(new MemoryStream(freshCandidate, false)); var sheet = workbook.Worksheet("Ledger");
                foreach (var (address, formula) in spec.ExpectedFormulas) Assert.AreEqual(formula, sheet.Cell(address).FormulaA1, spec.Id + ":" + address);
                Assert.AreEqual("Artificial constant-only context", workbook.Worksheet("Context").Cell("A1").GetString());
                for (var i = 0; i < spec.Count; i++)
                {
                    Assert.AreEqual("NEW-" + (i + 1), sheet.Cell(spec.Boundary + i, 3).GetString());
                    Assert.IsTrue(sheet.Cell(spec.Boundary + i, 7).IsEmpty()); Assert.IsTrue(sheet.Cell(spec.Boundary + i, 8).IsEmpty());
                }
            }
            else Assert.IsFalse(StructuralPreservationOracle.Evaluate(bytes, manifest, bytes).Accepted, spec.Id);
            Assert.AreEqual(inputHash, ReferenceCorpusFixture.Hash(inputPath));
            artifacts.Add(new(spec, input, inputHash, output, outputHash, manifest, spec.Supported ? "TsekSheet editor; NOT Microsoft Excel" : "negative control; editor rejected; no accepted candidate"));
        }
        var index = new ReferenceIndex(1, specification.FixtureKind, ReferenceCorpusFixture.Hash(SpecPath),
            ReferenceCorpusFixture.Hash(Path.Combine(Root, "tests/TsekSheet.Workbooks.Tests/ReferenceCorpusFixture.cs")), "ClosedXML 0.105.1; NOT Microsoft Excel",
            previous?.EditorSha256 ?? ReferenceCorpusFixture.Hash(Path.Combine(Root, "src/TsekSheet.Workbooks/StructuralRowCandidate.cs")),
            previous?.OracleSha256 ?? ReferenceCorpusFixture.Hash(Path.Combine(Root, "src/TsekSheet.Workbooks/StructuralPreservationOracle.cs")),
            ReferenceCorpusFixture.Hash(Path.Combine(Root, "scripts/Verify-ExcelReferenceCorpus.ps1")), previous?.DotnetVersion ?? Environment.Version.ToString(), artifacts.ToArray());
        if (previous == null) { using var writer = new StreamWriter(new FileStream(indexPath, FileMode.CreateNew)); writer.Write(JsonSerializer.Serialize(index, Json)); }
        else Assert.AreEqual(JsonSerializer.Serialize(previous, Json), JsonSerializer.Serialize(index, Json), "Corpus implementation/spec changed; retain old evidence and generate a new version.");
        Console.WriteLine("REFERENCE_CORPUS=" + CorpusDirectory);
    }

    [TestMethod]
    public void ExcelReferencesRequireActualProvenanceAndIndependentSemanticAgreement()
    {
        var path = Path.Combine(CorpusDirectory, "excel-evidence.json");
        if (!File.Exists(path)) Assert.Inconclusive("Microsoft Excel references are pending; OOXML fixtures are not Excel evidence.");
        using var evidence = JsonDocument.Parse(File.ReadAllText(path));
        var root = evidence.RootElement;
        Assert.AreEqual("Microsoft Excel COM automation on generated isolated fixtures", root.GetProperty("provenance").GetString());
        Assert.IsFalse(string.IsNullOrWhiteSpace(root.GetProperty("excelVersion").GetString()));
        Assert.AreEqual(ReferenceCorpusFixture.Hash(Path.Combine(CorpusDirectory, "corpus.index.json")), root.GetProperty("indexSha256").GetString());
        var index = JsonSerializer.Deserialize<ReferenceIndex>(File.ReadAllText(Path.Combine(CorpusDirectory, "corpus.index.json")), Json)!;
        Assert.AreEqual(index.ExcelProcedureSha256, root.GetProperty("scriptSha256").GetString());
        Assert.AreEqual(index.ExcelProcedureSha256, ReferenceCorpusFixture.Hash(Path.Combine(Root, "scripts/Verify-ExcelReferenceCorpus.ps1")));
        var comparisons = new List<object>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in root.GetProperty("cases").EnumerateArray())
        {
            var spec = index.Cases.Single(c => c.Spec.Id == item.GetProperty("id").GetString());
            Assert.IsTrue(seen.Add(spec.Spec.Id), "Duplicate Excel reference case.");
            Assert.IsTrue(spec.Spec.Supported);
            Assert.AreEqual(spec.Spec.Id + ".excel.xlsx", item.GetProperty("outputFile").GetString());
            Assert.AreEqual(spec.InputSha256, item.GetProperty("inputSha256").GetString());
            Assert.AreEqual(200d + 25d * spec.Spec.Count, item.GetProperty("calculatedTotals").GetProperty("selectedSection").GetDouble());
            Assert.AreEqual(400d + 25d * spec.Spec.Count, item.GetProperty("calculatedTotals").GetProperty("grandSummary").GetDouble());
            var excelPath = Path.Combine(CorpusDirectory, item.GetProperty("outputFile").GetString()!);
            Assert.AreEqual(spec.InputSha256, ReferenceCorpusFixture.Hash(Path.Combine(CorpusDirectory, spec.InputFile)));
            Assert.AreEqual(item.GetProperty("outputSha256").GetString(), ReferenceCorpusFixture.Hash(excelPath));
            var fresh = StructuralRowCandidate.Build(File.ReadAllBytes(Path.Combine(CorpusDirectory,spec.InputFile)),spec.Manifest,_=>{},default);
            Assert.IsTrue(StructuralPreservationOracle.Evaluate(File.ReadAllBytes(Path.Combine(CorpusDirectory,spec.InputFile)),spec.Manifest,fresh).Accepted);
            var temporary=Path.Combine(Path.GetTempPath(),"TsekSheet-reference-current-"+Guid.NewGuid().ToString("N")+".xlsx");
            SortedDictionary<string,string> expected;
            try { using(var file=new FileStream(temporary,FileMode.CreateNew))file.Write(fresh); expected=ReferenceCorpusFixture.Semantics(temporary); }
            finally { if(File.Exists(temporary))File.Delete(temporary); }
            var actual = ReferenceCorpusFixture.Semantics(excelPath);
            var differences = expected.Keys.Union(actual.Keys).Where(k => expected.GetValueOrDefault(k) != actual.GetValueOrDefault(k)).Select(k => k + ": " + expected.GetValueOrDefault(k) + " <> " + actual.GetValueOrDefault(k)).ToArray();
            var coreDifferences = differences.Where(d => !d.StartsWith("height/", StringComparison.Ordinal)).ToArray();
            Assert.IsEmpty(coreDifferences, spec.Spec.Id + ": " + string.Join("\n", coreDifferences.Take(12)));
            var heights = expected.Keys.Union(actual.Keys).Where(k => k.StartsWith("height/", StringComparison.Ordinal) && expected.GetValueOrDefault(k) != actual.GetValueOrDefault(k)).ToArray();
            foreach (var key in heights)
            {
                Assert.IsTrue(expected.ContainsKey(key) && actual.ContainsKey(key));
                Assert.AreEqual("22",expected[key],"Historical source height changed.");
                Assert.AreEqual("21.95",actual[key],"Historical Excel height changed; no tolerance is permitted.");
            }
            var candidateParts = StructuralPreservationOracle.Read(File.ReadAllBytes(Path.Combine(CorpusDirectory, spec.CandidateFile!)), default);
            var excelParts = StructuralPreservationOracle.Read(File.ReadAllBytes(excelPath), default);
            var changedParts = candidateParts.Keys.Intersect(excelParts.Keys).Where(k => !candidateParts[k].AsSpan().SequenceEqual(excelParts[k])).Order().ToArray();
            comparisons.Add(new { id = spec.Spec.Id, contentFormulaAndComparedStyleDifferences = coreDifferences, rowHeightDifferences = differences.Where(d => d.StartsWith("height/", StringComparison.Ordinal)).ToArray(),
                addedExcelParts = excelParts.Keys.Except(candidateParts.Keys).Order().ToArray(), removedExcelParts = candidateParts.Keys.Except(excelParts.Keys).Order().ToArray(),
                changedPackageParts = changedParts, changedRelationshipParts = changedParts.Where(k => k.EndsWith(".rels", StringComparison.Ordinal)).ToArray(),
                qualification = heights.Length == 0 ? "compared semantic projection matches" : "content/formula/compared styles match; measured Excel row-height rounding differs; not full structural equivalence" });
        }
        Assert.AreEqual(4, root.GetProperty("cases").GetArrayLength());
        var verifierHash=ReferenceCorpusFixture.Hash(Path.Combine(Root,"tests/TsekSheet.Workbooks.Tests/ReferenceCorpusTests.cs"));
        var comparisonPath = Path.Combine(CorpusDirectory, "semantic-comparison-v4-"+verifierHash[..12]+".json");
        var comparisonJson = JsonSerializer.Serialize(new { verifierSha256 = verifierHash, currentEditorSha256=ReferenceCorpusFixture.Hash(Path.Combine(Root,"src/TsekSheet.Workbooks/StructuralRowCandidate.cs")), currentOracleSha256=ReferenceCorpusFixture.Hash(Path.Combine(Root,"src/TsekSheet.Workbooks/StructuralPreservationOracle.cs")), excelEvidenceSha256 = ReferenceCorpusFixture.Hash(path), comparisons }, Json);
        if (!File.Exists(comparisonPath)) { using var writer = new StreamWriter(new FileStream(comparisonPath, FileMode.CreateNew)); writer.Write(comparisonJson); }
        else Assert.AreEqual(File.ReadAllText(comparisonPath), comparisonJson);
    }
}
