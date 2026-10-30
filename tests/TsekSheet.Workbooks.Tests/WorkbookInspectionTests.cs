using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

[TestClass, DoNotParallelize]
public sealed partial class WorkbookInspectionTests
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string Office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/";
    public TestContext TestContext { get; set; } = null!;
    private readonly List<string> files = [];
    [TestCleanup] public void Cleanup() { foreach (var path in files) File.Delete(path); }
    private static string Hash(string path) { using var f = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(f)); }
    private string Package(int sheets = 1, string? cells = null, string dimension = "A1", Dictionary<string, string>? extra = null, string extraRels = "", bool malformedOther = false)
    {
        var path = Path.Combine(Path.GetTempPath(), "TsekSheet-inspection-" + Guid.NewGuid().ToString("N") + ".xlsx"); files.Add(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        void Put(string part, string content) { using var writer = new StreamWriter(zip.CreateEntry(part).Open()); writer.Write(content); }
        Put("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>");
        Put("_rels/.rels", $"<Relationships xmlns=\"{Rel}\"><Relationship Id=\"root\" Type=\"{Office}officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        Put("xl/workbook.xml", $"<workbook xmlns=\"{Ns}\" xmlns:r=\"{Office.TrimEnd('/')}\"><sheets>" + string.Concat(Enumerable.Range(1, sheets).Select(i => $"<sheet name=\"Sheet{i}\" sheetId=\"{i}\" r:id=\"r{i}\" state=\"{(i == sheets ? "hidden" : "visible")}\"/>")) + "</sheets><definedNames><definedName name=\"Synthetic\">Sheet1!$A$1</definedName></definedNames></workbook>");
        Put("xl/_rels/workbook.xml.rels", $"<Relationships xmlns=\"{Rel}\">" + string.Concat(Enumerable.Range(1, sheets).Select(i => $"<Relationship Id=\"r{i}\" Type=\"{Office}worksheet\" Target=\"worksheets/s{i}.xml\"/>")) + extraRels + "</Relationships>");
        for (var i = 1; i <= sheets; i++) Put($"xl/worksheets/s{i}.xml", malformedOther && i > 1 ? "<broken" : $"<worksheet xmlns=\"{Ns}\"><dimension ref=\"{dimension}\"/><sheetData>{cells ?? "<row r=\"1\"><c r=\"A1\"><v>42</v></c></row>"}</sheetData></worksheet>");
        foreach (var pair in extra ?? []) Put(pair.Key, pair.Value);
        return path;
    }
    private static string Rows(int count, string mode = "values")
    {
        var text = new StringBuilder();
        for (var i = 1; i <= count; i++) text.Append($"<row r=\"{i}\"><c r=\"A{i}\" s=\"1\">{(mode == "styled" ? "" : mode == "formulas" ? $"<f>{i}+1</f><v>{i + 1}</v>" : $"<v>{i}</v>")}</c></row>");
        return text.ToString();
    }
    [TestMethod, DataRow(10), DataRow(50), DataRow(100), DataRow(250), DataRow(512)]
    public void ManySheetsKeepOrderVisibilityAndOnlyLoadSelected(int sheets)
    {
        var path = Package(sheets, malformedOther: true); var hash = Hash(path);
        using (var session = new WorkbookInspection().Open(path))
        {
            Assert.AreEqual(sheets, session.Info.Worksheets.Count); Assert.AreEqual("Hidden", session.Info.Worksheets[^1].Visibility);
            Assert.AreEqual("Sheet" + sheets, session.Info.Worksheets[^1].Name); Assert.AreEqual(sheets, session.Info.Worksheets[^1].Order);
            Assert.AreEqual("42", session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Value);
            Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(session.Info.Worksheets[1].Id, 1, 1, 1, 1));
            Assert.AreEqual(1, session.Info.Compatibility!.DefinedNames);
        }
        Assert.AreEqual(hash, Hash(path));
    }
    [TestMethod]
    public void OrdinaryFixtureInspectionCannotMutateOrBecomeWorkingCopy()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reimbursement-synthetic.xlsx"); var hash = Hash(path);
        using var session = new WorkbookEngine().Inspect(path); var id = session.Info.Worksheets[0].Id;
        Assert.IsFalse(session.Info.EditingAllowed); Assert.IsFalse(session.Info.IsDirty);
        Assert.AreEqual(3, session.Info.Compatibility!.Capabilities.Count(c => c.Allowed));
        Assert.ThrowsExactly<WorkbookException>(() => session.CreateSnapshot()); Assert.ThrowsExactly<WorkbookException>(() => session.CreateWorkingCopy(id, 0));
        Assert.ThrowsExactly<WorkbookException>(() => session.Edit(new(id, 1, 1, "text", "changed", 0)));
        Assert.ThrowsExactly<WorkbookException>(() => session.SaveAsNew(path, 0));
        Assert.ThrowsExactly<WorkbookException>(() => session.Integrate(session, new(session.Info.SessionId, id, 0, 0, 0)));
        var view = session.ReadViewport(id, 1, 1, 20, 6); Assert.IsNotNull(view.Scan); Assert.IsTrue(view.Scan.StoredCells > 0);
        Assert.AreEqual(hash, Hash(path));
    }
    [TestMethod]
    public void RawTypesSparseCellsInflatedDimensionsAndCountsAreHonest()
    {
        var path = Package(cells: "<row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>literal</t></is></c><c r=\"B1\" t=\"b\"><v>1</v></c><c r=\"C1\" t=\"d\"><v>2026-10-29</v></c><c r=\"D1\"><f>1+2</f><v>3</v></c><c r=\"E1\" s=\"1\"/></row><row r=\"900000\"><c r=\"XFD900000\"><v>9</v></c></row>", dimension: "A1:XFD1048576");
        using var session = new WorkbookInspection().Open(path); var id = session.Info.Worksheets[0].Id;
        var v = session.ReadViewport(id, 1, 1, 1, 5); Assert.AreEqual(6L, v.Scan!.StoredCells); Assert.AreEqual(5L, v.Scan.NonemptyValues); Assert.AreEqual(1L, v.Scan.Formulas); Assert.AreEqual(1L, v.Scan.StyledCells);
        Assert.AreEqual("TRUE", v.Cells[1].Value); Assert.AreEqual("Date", v.Cells[2].Kind); Assert.AreEqual("1+2", v.Cells[3].Formula);
        Assert.AreEqual("9", session.ReadViewport(id, 900000, 16384, 1, 1).Cells[0].Value);
        using var inflated = new WorkbookInspection().Open(Package(dimension: "A1:XFD1048576")); Assert.IsTrue(inflated.ReadViewport(inflated.Info.Worksheets[0].Id, 1, 1, 1, 1).Scan!.PotentialInflation);
    }
    [TestMethod]
    public void SharedStringsExternalLinksAndEmbeddedCategoriesRemainReadOnly()
    {
        var extras = new Dictionary<string, string> { ["xl/sharedStrings.xml"] = $"<sst xmlns=\"{Ns}\"><si><r><t>First </t></r><r><t>item</t></r><rPh sb=\"0\" eb=\"1\"><t>annotation</t></rPh></si><si><t>second</t></si></sst>", ["xl/media/image.png"] = "not executed", ["xl/embeddings/object.bin"] = "not executed", ["xl/activeX/control.bin"] = "not executed", ["xl/vbaProject.bin"] = "not executed", ["xl/unknown.bin"] = "ignored" };
        using var session = new WorkbookInspection().Open(Package(cells: "<row><c r=\"A1\" t=\"s\"><v>0</v></c></row>", extra: extras, extraRels: $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/><Relationship Id=\"external\" Type=\"{Office}externalLink\" Target=\"file:///NEVER-OPEN\" TargetMode=\"External\"/>"));
        Assert.IsTrue(session.Info.Compatibility!.ExternalLinks); Assert.IsTrue(session.Info.Compatibility.Categories.Any(c => c.Category.Contains("OLE"))); Assert.IsTrue(session.Info.Compatibility.Categories.Any(c => c.Category.Contains("Images")));
        Assert.AreEqual("First item", session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Value);
    }
    [TestMethod, DataRow("../escape"), DataRow("/absolute"), DataRow("xl/%2e%2e/evil"), DataRow("xl\\evil"), DataRow("xl/workbook.xml")]
    public void AdversarialZipEntriesAreRejected(string part) => Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(Package(extra: new() { [part] = "bad" })));
    [TestMethod, DataRow("missing.xml"), DataRow("https://remote/file"), DataRow("file:///C:/secret")]
    public void BrokenOrRemoteInternalRelationshipsAreRejected(string target) => Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(Package(extraRels: $"<Relationship Id=\"bad\" Type=\"{Office}image\" Target=\"{target}\"/>")));
    [TestMethod]
    public void MalformedAndDtdXmlRejectOnlyTheSelectedView()
    {
        foreach (var xml in new[] { "<row><c", "<!DOCTYPE a [<!ENTITY x SYSTEM 'file:///secret'>]><row/>", "<row><c r=\"A1\"/><c r=\"A1\"/></row>" })
        {
            using var session = new WorkbookInspection().Open(Package(cells: xml));
            Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1)); Assert.IsNotNull(session.Info.Compatibility);
        }
    }
    [TestMethod]
    public void BudgetsCancellationAndRepeatedDisposalReleaseResources()
    {
        var path = Package(cells: Rows(100));
        foreach (var limits in new[] { new InspectionLimits { FileBytes = 1 }, new InspectionLimits { ExpandedBytes = 1 }, new InspectionLimits { Entries = 1 }, new InspectionLimits { Relationships = 1 }, new InspectionLimits { Milliseconds = -1 } }) Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection(limits).Open(path));
        using (var session = new WorkbookInspection(new() { Cells = 10 }).Open(path)) Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1));
        using (var session = new WorkbookInspection(new() { XmlBytes = 4096 }).Open(path)) Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.ThrowsExactly<OperationCanceledException>(() => new WorkbookInspection().Open(path, cancelled.Token));
        for (var i = 0; i < 20; i++) { var session = new WorkbookInspection().Open(path); session.Dispose(); session.Dispose(); Assert.ThrowsExactly<ObjectDisposedException>(() => _ = session.Info); }
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); // no retained source handle after close
    }
    [TestMethod, DataRow(125000, "values"), DataRow(125000, "styled"), DataRow(60000, "formulas")]
    public void RepeatableLargeSheetProbe(int cells, string mode)
    {
        var path = Package(cells: Rows(cells, mode), dimension: "A1:A1048576"); var hash = Hash(path); var runs = new List<object>();
        for (var repeat = 0; repeat < 3; repeat++)
        {
            GC.Collect(); var baseline = GC.GetTotalMemory(true); var allocation = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew();
            using var session = new WorkbookInspection().Open(path); var metadataMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); var view = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 20, 6); var scanMs = watch.Elapsed.TotalMilliseconds;
            Assert.AreEqual((long)cells, view.Scan!.StoredCells); Assert.AreEqual(mode == "styled" ? 0L : cells, view.Scan.NonemptyValues);
            watch.Restart(); Assert.AreSame(view, session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 20, 6)); var cacheMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); session.ReadViewport(session.Info.Worksheets[0].Id, cells - 19, 1, 20, 6); var secondMs = watch.Elapsed.TotalMilliseconds;
            runs.Add(new { repeat, metadataMs, scanMs, cacheMs, secondMs, allocatedBytes = GC.GetTotalAllocatedBytes() - allocation, managedRetainedDelta = GC.GetTotalMemory(true) - baseline, processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 });
        }
        Assert.AreEqual(hash, Hash(path));
        var json = JsonSerializer.Serialize(new { cells, mode, sourceUnchanged = true, runs }, new JsonSerializerOptions { WriteIndented = true }); TestContext.WriteLine(json);
        if (Environment.GetEnvironmentVariable("TSEKSHEET_PROBE_DIR") is { } dir) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, mode + ".json"), json); }
    }

    [TestMethod]
    public void LargeSharedStringsAreScannedWithoutRetainingTheCollection()
    {
        var strings = $"<sst xmlns=\"{Ns}\">" + string.Concat(Enumerable.Range(0, 200000).Select(i => $"<si><t>record-{i}</t></si>")) + "</sst>";
        var path = Package(cells: "<row><c r=\"A1\" t=\"s\"><v>199999</v></c></row>", extra: new() { ["xl/sharedStrings.xml"] = strings }, extraRels: $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/>");
        var timer = Stopwatch.StartNew(); using var session = new WorkbookInspection().Open(path); var metadataMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart(); Assert.AreEqual("record-199999", session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Value);
        var evidence = JsonSerializer.Serialize(new { sharedStrings = 200000, metadataMs, scanMs = timer.Elapsed.TotalMilliseconds, processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 });
        TestContext.WriteLine(evidence); if (Environment.GetEnvironmentVariable("TSEKSHEET_PROBE_DIR") is { } dir) File.WriteAllText(Path.Combine(dir, "shared-strings.json"), evidence);
        using var bounded = new WorkbookInspection(new() { SharedStrings = 100 }).Open(path);
        Assert.ThrowsExactly<WorkbookException>(() => bounded.ReadViewport(bounded.Info.Worksheets[0].Id, 1, 1, 1, 1));
    }
    [TestMethod]
    public void CompressionBombAndDeepMetadataFailClosed()
    {
        Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(Package(extra: new() { ["xl/unknown.bin"] = new string('0', 4 * 1024 * 1024) })));
        var path = Package();
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            zip.GetEntry("xl/workbook.xml")!.Delete(); using var w = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open());
            w.Write(string.Concat(Enumerable.Repeat("<a>", 100)) + string.Concat(Enumerable.Repeat("</a>", 100)));
        }
        Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(path));
    }
    [TestMethod]
    public async Task ClosingDuringScanCancelsAndReleasesSource()
    {
        var path = Package(cells: Rows(500000)); var session = new WorkbookInspection().Open(path); var id = session.Info.Worksheets[0].Id;
        using var started = new ManualResetEventSlim();
        var read = Task.Run(() => { started.Set(); try { session.ReadViewport(id, 1, 1, 20, 6); } catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException) { return true; } return false; });
        started.Wait(); await Task.Delay(5); session.Dispose(); Assert.IsTrue(await read);
        using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }
    [TestMethod]
    public void ManySheetMetadataPerformanceAndAdmissionAreBounded()
    {
        var path = Package(512, malformedOther: true); var watch = Stopwatch.StartNew(); using var one = new WorkbookInspection().Open(path); var metadataMs = watch.Elapsed.TotalMilliseconds;
        using var two = new WorkbookInspection().Open(path); Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(path));
        var json = JsonSerializer.Serialize(new { sheets = 512, metadataMs, processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64, contentsEagerlyLoaded = false });
        TestContext.WriteLine(json); if (Environment.GetEnvironmentVariable("TSEKSHEET_PROBE_DIR") is { } dir) File.WriteAllText(Path.Combine(dir, "many-sheets.json"), json);
    }

    [TestMethod]
    public void ForgedCentralDirectoryCountsAndTruncatedArchivesAreRejected()
    {
        var path = Package(); var bytes = File.ReadAllBytes(path);
        bytes[^12] = 1; bytes[^11] = 0; File.WriteAllBytes(path, bytes);
        Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(path));
        File.WriteAllBytes(path, bytes[..^22]); Assert.ThrowsExactly<WorkbookException>(() => new WorkbookInspection().Open(path));
    }
    [TestMethod]
    public void ThousandsOfSectionRecordsRemainOrdinaryStoredCells()
    {
        var rows = new StringBuilder();
        for (var i = 1; i <= 6000; i++) rows.Append(i % 2000 == 1 ? $"<row><c r=\"A{i}\" t=\"inlineStr\"><is><t>Section {i}</t></is></c></row>" : $"<row><c r=\"A{i}\"><v>{i}</v></c></row>");
        var path = Package(cells: rows.ToString()); var hash = Hash(path); using var session = new WorkbookInspection().Open(path);
        var view = session.ReadViewport(session.Info.Worksheets[0].Id, 4001, 1, 20, 6);
        Assert.AreEqual("Section 4001", view.Cells[0].Value); Assert.AreEqual(6000L, view.Scan!.StoredCells); Assert.AreEqual(hash, Hash(path));
    }
}
