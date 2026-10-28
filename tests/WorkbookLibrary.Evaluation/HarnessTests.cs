using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace WorkbookLibrary.Evaluation;

[TestClass]
public sealed class HarnessTests
{
    private static string FixturePath(FixtureSpec spec) => Path.Combine(Program.FindRoot(), "tests", "WorkbookLibrary.Evaluation", "Fixtures", spec.FileName);
    private static string FreshOutput() { var folder = Path.Combine(Program.FindRoot(), "artifacts", "workbook-evaluation", "test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder); return Path.Combine(folder, "candidate.xlsx"); }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void FixturesAreValidArtificialPackages(int index)
    {
        var spec = FixtureFactory.Specs[index]; var snapshot = PackageInspector.Read(FixturePath(spec));
        Assert.IsEmpty(snapshot.SchemaErrors);
        Assert.AreEqual("hidden", snapshot.Values["sheet/Lookup/state"]);
        Assert.AreEqual("artificial-only", snapshot.Values["custom/FixtureClassification"]);
        Assert.AreEqual($"formula:'{spec.MainSheet}'!C{spec.SummaryRow}", snapshot.Values["cell/Overview/B2/content"]);
        Assert.AreEqual($"A4:D{spec.AlternateSummary - 1}", snapshot.Values[$"table/{spec.AlternateSheet}/SyntheticTable2"]);
        Assert.AreEqual("True", snapshot.Values["opaque/present"]);
    }

    [TestMethod]
    public void FixtureGenerationRefusesToOverwriteExistingFile()
    {
        var path = FreshOutput(); File.WriteAllText(path, "sentinel");
        Assert.ThrowsExactly<IOException>(() => FixtureFactory.Create(path, FixtureFactory.Specs[0]));
        Assert.AreEqual("sentinel", File.ReadAllText(path));
    }

    [TestMethod]
    [DataRow(0, "ClosedXML")] [DataRow(1, "ClosedXML")]
    [DataRow(0, "OpenXmlSdk")] [DataRow(1, "OpenXmlSdk")]
    public void SerializedCloneEditsSurviveReopenWithoutChangingSource(int index, string library)
    {
        var spec = FixtureFactory.Specs[index]; var path = FixturePath(spec); var original = PackageInspector.Hash(path);
        var output = FreshOutput(); File.WriteAllBytes(output, LibraryProbes.Run(library, "deep-copy", File.ReadAllBytes(path), spec));
        Assert.AreEqual(original, PackageInspector.Hash(path));
        Assert.IsEmpty(IntegrityOracle.Check(PackageInspector.Read(path), PackageInspector.Read(output), spec, "deep-copy", library));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void RepeatedInsertionWithExplicitFixtureRangePolicyPreservesDeclaredContent(int index)
    {
        var spec = FixtureFactory.Specs[index]; var path = FixturePath(spec); var hash = PackageInspector.Hash(path); var output = FreshOutput();
        File.WriteAllBytes(output, LibraryProbes.Run("ClosedXML", "insert-declared-ranges", File.ReadAllBytes(path), spec));
        Assert.IsEmpty(IntegrityOracle.Check(PackageInspector.Read(path), PackageInspector.Read(output), spec, "insert-declared-ranges", "ClosedXML"));
        Assert.AreEqual(hash, PackageInspector.Hash(path));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void SchemaValidationAloneDoesNotCatchBrokenStructuralReferences(int index)
    {
        var spec = FixtureFactory.Specs[index]; var path = FixturePath(spec); var output = FreshOutput();
        File.WriteAllBytes(output, LibraryProbes.Run("OpenXmlSdk", "insert-repeated", File.ReadAllBytes(path), spec));
        var actual = PackageInspector.Read(output);
        Assert.IsEmpty(actual.SchemaErrors);
        var differences = IntegrityOracle.Check(PackageInspector.Read(path), actual, spec, "insert-repeated", "OpenXmlSdk");
        Assert.IsTrue(differences.Any(d => d.Contains("cell/Overview/B2/content", StringComparison.Ordinal)));
        Assert.IsTrue(differences.Any(d => d.Contains("name/SummaryCell", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow(0)] [DataRow(1)]
    public void RawBoundaryInsertionDoesNotMeetDeclaredRegionExpansionContract(int index)
    {
        var spec = FixtureFactory.Specs[index]; var path = FixturePath(spec); var output = FreshOutput();
        File.WriteAllBytes(output, LibraryProbes.Run("ClosedXML", "insert-repeated", File.ReadAllBytes(path), spec));
        var differences = IntegrityOracle.Check(PackageInspector.Read(path), PackageInspector.Read(output), spec, "insert-repeated", "ClosedXML");
        Assert.AreEqual(5, differences.Length);
        Assert.IsTrue(differences.Any(d => d.StartsWith("table/", StringComparison.Ordinal)));
    }
}
