using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

[TestClass, DoNotParallelize]
public sealed class NativeFormatResolutionTests
{
    [TestMethod]
    public void ArtificialResolutionCasesKeepIndependentDisplayOrRawExpectations()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TsekSheet-format-resolution-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        foreach (var spec in FormatResolutionFixture.Cases)
        {
            var path = Path.Combine(directory, spec.Id + ".xlsx"); FormatResolutionFixture.Create(path, spec); var hash = ReferenceCorpusFixture.Hash(path);
            using (var session = new WorkbookInspection().Open(path))
            {
                var c = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0];
                Assert.AreEqual(spec.Raw, c.Value, spec.Id); Assert.AreEqual(spec.FormatId, c.Format.NumberFormatId, spec.Id);
                Assert.AreEqual(spec.ExpectedDisplay ?? spec.Raw, c.Interpretation!.DisplayValue, spec.Id);
                Assert.AreEqual(spec.ExpectedDisplay == null ? "Unsupported" : "Supported subset", c.Interpretation.Confidence, spec.Id);
            }
            Assert.AreEqual(hash, ReferenceCorpusFixture.Hash(path));
        }
        // Generation is explicit and create-new; normal tests never rewrite accepted reference files.
        if (Environment.GetEnvironmentVariable("TSEKSHEET_FORMAT_REFERENCE_DIR") is { } requested)
        {
            var allowed = Path.GetFullPath(Path.Combine(ReferenceCorpusTests.Root, "artifacts/step54-style-reference")) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(requested); Assert.IsTrue((target + Path.DirectorySeparatorChar).StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
            Directory.CreateDirectory(target);
            for (var walk = new DirectoryInfo(target); walk != null; walk = walk.Parent) Assert.IsFalse(walk.Attributes.HasFlag(FileAttributes.ReparsePoint));
            var artifacts = new List<object>();
            foreach (var spec in FormatResolutionFixture.Cases)
            {
                var name = spec.Id + ".input.xlsx"; var path = Path.Combine(target, name); File.Copy(Path.Combine(directory, spec.Id + ".xlsx"), path, false);
                artifacts.Add(new { spec, inputFile = name, inputSha256 = ReferenceCorpusFixture.Hash(path) });
            }
            var index = new { schemaVersion = 1, fixtureKind = "step54-artificial-format-resolution", generatorSha256 = ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root, "tests/TsekSheet.Workbooks.Tests/FormatResolutionFixture.cs")),
                procedureSha256 = ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root, "scripts/Capture-FormatResolution.ps1")), cases = artifacts };
            using var file = new FileStream(Path.Combine(target, "resolution.index.json"), FileMode.CreateNew); JsonSerializer.Serialize(file, index, ReferenceCorpusTests.Json);
        }
    }

    [TestMethod]
    public void RetainedNativeResolutionObservationsDoNotBroadenRejectionPolicy()
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "ReferenceCorpus/FormatResolution");
        // Absence is a failure: this reference gate must never silently become a skip.
        using var evidence = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "capture.json")));
        using var index = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "resolution.index.json")));
        var root = evidence.RootElement;
        Assert.AreEqual("Actual Microsoft Excel; isolated generated format-resolution inputs only", root.GetProperty("provenance").GetString());
        Assert.AreEqual(ReferenceCorpusFixture.Hash(Path.Combine(directory, "resolution.index.json")), root.GetProperty("indexSha256").GetString());
        Assert.AreEqual(index.RootElement.GetProperty("procedureSha256").GetString(), root.GetProperty("scriptSha256").GetString());
        Assert.AreEqual(index.RootElement.GetProperty("generatorSha256").GetString(), ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root, "tests/TsekSheet.Workbooks.Tests/FormatResolutionFixture.cs")));
        Assert.AreEqual(root.GetProperty("scriptSha256").GetString(), ReferenceCorpusFixture.Hash(Path.Combine(ReferenceCorpusTests.Root, "scripts/Capture-FormatResolution.ps1")));
        int qualified = 0, unsupported = 0, nativeRejected = 0;
        foreach (var item in root.GetProperty("cases").EnumerateArray())
        {
            var spec = FormatResolutionFixture.Cases.Single(s => s.Id == item.GetProperty("id").GetString());
            var input = Path.Combine(directory, spec.Id + ".input.xlsx"); var entry = index.RootElement.GetProperty("cases").EnumerateArray().Single(c => c.GetProperty("spec").GetProperty("id").GetString() == spec.Id);
            Assert.AreEqual(entry.GetProperty("inputSha256").GetString(), ReferenceCorpusFixture.Hash(input));
            if (item.GetProperty("status").GetString() != "observed") { nativeRejected++; Assert.IsNull(spec.ExpectedDisplay, "Qualified cases require actual native observations."); }
            else
            {
                Assert.AreEqual(ReferenceCorpusFixture.Hash(Path.Combine(directory, item.GetProperty("outputFile").GetString()!)), item.GetProperty("outputSha256").GetString());
                if (spec.ExpectedDisplay != null) Assert.AreEqual(spec.ExpectedDisplay, item.GetProperty("text").GetString(), spec.Id);
            }
            using var session = new WorkbookInspection().Open(input); var c = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0];
            Assert.AreEqual(spec.ExpectedDisplay ?? spec.Raw, c.Interpretation!.DisplayValue, spec.Id);
            if (spec.ExpectedDisplay != null) qualified++; else { unsupported++; Assert.AreEqual("Unsupported", c.Interpretation.Confidence); }
        }
        Assert.AreEqual(19, root.GetProperty("cases").GetArrayLength()); Assert.AreEqual(10, qualified); Assert.AreEqual(9, unsupported);
        Assert.IsTrue(nativeRejected <= 9); // Explicit native rejections are retained, never counted as display matches.
    }
}
