using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

public sealed partial class WorkbookInspectionTests
{
    private string InterpretedPackage(string cells, string format = "yyyy-mm-dd", bool date1904 = false, int formatId = 164, string? stylesheet = null)
    {
        var style = stylesheet ?? $"<styleSheet xmlns=\"{Ns}\"><numFmts>{(formatId >= 164 ? new XElement("numFmt", new XAttribute("numFmtId", formatId), new XAttribute("formatCode", format)).ToString() : "")}</numFmts><cellXfs><xf numFmtId=\"{formatId}\"/></cellXfs></styleSheet>";
        // XElement above inherits the default namespace in the serialized enclosing stylesheet.
        var path = Package(cells: cells, extra: new() { ["xl/styles.xml"] = style }, extraRels: $"<Relationship Id=\"styles\" Type=\"{Office}styles\" Target=\"styles.xml\"/>");
        if (date1904)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Update); var entry = zip.GetEntry("xl/workbook.xml")!; string text;
            using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd(); entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open()); writer.Write(text.Replace("<sheets>", "<workbookPr date1904=\"1\"/><sheets>"));
        }
        return path;
    }
    [TestMethod]
    [DataRow("1", false, "yyyy-mm-dd", "1900-01-01")]
    [DataRow("59", false, "yyyy-mm-dd", "1900-02-28")]
    [DataRow("60", false, "yyyy-mm-dd", "1900-02-29")]
    [DataRow("61", false, "dd/mm/yyyy", "1900-03-01")]
    [DataRow("0", true, "yyyy-mm-dd", "1904-01-01")]
    [DataRow("1", true, "yyyy-mm-dd", "1904-01-02")]
    [DataRow("1462", false, "yyyy-mm-dd", "1904-01-01")]
    [DataRow("61.5", false, "yyyy-mm-dd hh:mm:ss", "1900-03-01 12:00:00")]
    [DataRow("0.25", false, "hh:mm:ss", "06:00:00")]
    [DataRow("1.75", false, "h:mm", "18:00:00")]
    [DataRow("0.000001", false, "hh:mm:ss", "00:00:00.086")]
    [DataRow("2958465", false, "yyyy-mm-dd", "9999-12-31")]
    [DataRow("61.999999999", false, "yyyy-mm-dd", "1900-03-01")]
    public void DateSerialLiteralExpectations(string serial, bool date1904, string format, string expected)
    {
        var path = InterpretedPackage($"<row r=\"1\"><c r=\"A1\"><v>{serial}</v></c></row>", format, date1904); var hash = Hash(path);
        using var session = new WorkbookInspection().Open(path); var cell = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0];
        Assert.AreEqual(serial, cell.Value); Assert.AreEqual(serial, cell.Interpretation!.RawValue); Assert.AreEqual(expected, cell.Interpretation.DisplayValue); Assert.AreEqual("Supported subset", cell.Interpretation.Confidence);
        if (serial == "60") StringAssert.Contains(cell.Interpretation.Warning, "Fictitious"); Assert.AreEqual(hash, Hash(path));
    }
    [TestMethod]
    [DataRow("-1", "yyyy-mm-dd")][DataRow("2958466", "yyyy-mm-dd")][DataRow("0", "yyyy-mm-dd")][DataRow("NaN", "yyyy-mm-dd")]
    [DataRow("1234", "mm")][DataRow("1234", "[Red]yyyy-mm-dd")][DataRow("1234", "[$-409]m/d/yy")][DataRow("1.5", "[h]:mm:ss")][DataRow("1234", "yyyy\"")]
    public void UnsupportedDateFormatsAndSerialsRetainRaw(string serial, string format)
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage($"<row><c r=\"A1\"><v>{serial}</v></c></row>", format));
        var cell = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0]; Assert.AreEqual(serial, cell.Interpretation!.DisplayValue); Assert.AreEqual("Unsupported", cell.Interpretation.Confidence);
    }
    [TestMethod, DataRow(14, "61", "1900-03-01"), DataRow(22, "61.25", "1900-03-01 06:00:00"), DataRow(20, "0.5", "12:00:00"), DataRow(4, "1234.5", "1,234.50"), DataRow(0, "61", "61")]
    public void BuiltInFormatsDoNotTurnGeneralNumbersIntoDates(int id, string raw, string expected)
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage($"<row><c r=\"A1\"><v>{raw}</v></c></row>", formatId: id));
        Assert.AreEqual(expected, session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Interpretation!.DisplayValue);
    }
    [TestMethod]
    public void SharedFormulasTranslateMixedReferencesWithIndependentLiterals()
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage("<row r=\"2\"><c r=\"B2\"><f t=\"shared\" si=\"0\" ref=\"B2:C3\">SUM(A1:$A$2)+A$1+$A1*2</f><v>8</v></c><c r=\"C2\"><f t=\"shared\" si=\"0\"/><v>9</v></c></row><row r=\"3\"><c r=\"B3\"><f t=\"shared\" si=\"0\"/><v>10</v></c><c r=\"C3\"><f t=\"shared\" si=\"0\"/><v>11</v></c></row>", "General"));
        var id = session.Info.Worksheets[0].Id; var c = session.ReadViewport(id, 3, 3, 1, 1).Cells[0];
        Assert.AreEqual("SUM(B2:$A$2)+B$1+$A2*2", c.Formula); Assert.AreEqual("11", c.Value); StringAssert.StartsWith(c.Interpretation!.FormulaStatus, "Reconstructed");
        Assert.AreEqual("SUM(A2:$A$2)+A$1+$A2*2", session.ReadViewport(id, 3, 2, 1, 1).Cells[0].Formula);
    }
    [TestMethod, DataRow("'Other sheet'!A1"), DataRow("Sheet2!A1"), DataRow("IF(A1,1,0)"), DataRow("A1#"), DataRow("Table1[Amount]"), DataRow("SUM(A1:B2,C3)"), DataRow("XFD1+1")]
    public void UnsupportedSharedFormulaSyntaxNeverFabricatesFollower(string formula)
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage($"<row><c r=\"B1\"><f t=\"shared\" si=\"0\" ref=\"B1:C1\">{formula}</f><v>1</v></c><c r=\"C1\"><f t=\"shared\" si=\"0\"/><v>2</v></c></row>", "General"));
        var cell = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 3, 1, 1).Cells[0]; Assert.AreEqual("", cell.Formula); StringAssert.StartsWith(cell.Interpretation!.FormulaStatus, "Unsupported");
    }
    [TestMethod]
    public void MalformedSharedGroupsAndArrayFollowersAreExplicit()
    {
        foreach (var cells in new[] {
            "<row><c r=\"A1\"><f t=\"shared\" si=\"3\"/></c></row>",
            "<row><c r=\"A1\"><f t=\"shared\" si=\"0\" ref=\"A1:A3\">B1</f></c></row><row><c r=\"A2\"><f t=\"shared\" si=\"0\"/></c></row>",
            "<row><c r=\"A1\"><f t=\"shared\" si=\"0\" ref=\"A1:B1\">C1</f></c><c r=\"B1\"><f t=\"shared\" si=\"1\" ref=\"B1:C1\">D1</f></c><c r=\"C1\"><f t=\"shared\" si=\"1\"/></c></row>" })
        { using var session = new WorkbookInspection().Open(InterpretedPackage(cells, "General")); StringAssert.StartsWith(session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Interpretation!.FormulaStatus, "Unsupported"); }
        using var array = new WorkbookInspection().Open(InterpretedPackage("<row><c r=\"A1\"><f t=\"array\" ref=\"A1:A2\">SUM(B1:B2)</f><v>4</v></c></row>", "General"));
        StringAssert.Contains(array.ReadViewport(array.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0].Interpretation!.FormulaStatus, "Unsupported array");
    }
    [TestMethod]
    public void StyleBudgetsRetainCellsAndExplicitWarnings()
    {
        foreach (var styles in new[] { "<broken", $"<styleSheet xmlns=\"{Ns}\"><cellXfs>" + string.Concat(Enumerable.Repeat("<xf numFmtId=\"14\"/>", 4097)) + "</cellXfs></styleSheet>" })
        { using var session = new WorkbookInspection().Open(InterpretedPackage("<row><c r=\"A1\"><v>61</v></c></row>", stylesheet: styles)); var c = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0]; Assert.AreEqual("61", c.Interpretation!.DisplayValue); Assert.AreEqual("Unsupported", c.Interpretation.Confidence); }
    }
    [TestMethod, DataRow("<row r=\"2\"/><row r=\"1\"/>"), DataRow("<row r=\"1\"/><row r=\"1\"/>"), DataRow("<row r=\"x\"/>"), DataRow("<row r=\"2\"><c r=\"A1\"/></row>")]
    public void InvalidRowLocationsRejectTheView(string rows)
    { using var session = new WorkbookInspection().Open(Package(cells: rows)); Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1)); }
    [TestMethod]
    public void WriterExclusionCacheEvictionAndReopenBindToSourceIdentity()
    {
        var path = Package(cells: Rows(200)); var first = new WorkbookInspection().Open(path); var id = first.Info.Worksheets[0].Id; var hash = first.Info.SourceSha256;
        Assert.ThrowsExactly<IOException>(() => { using var writer = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite); });
        var old = first.ReadViewport(id, 1, 1, 20, 6); first.ReadViewport(id, 21, 1, 20, 6); Assert.AreSame(old, first.ReadViewport(id, 1, 1, 20, 6));
        for (var i = 1; i <= 9; i++) first.ReadViewport(id, i * 20 + 1, 1, 20, 6);
        Assert.AreNotSame(old, first.ReadViewport(id, 1, 1, 20, 6)); first.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => first.ReadViewport(id, 1, 1, 20, 6));
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Update)) { using var w = new StreamWriter(zip.CreateEntry("unrelated.xml").Open()); w.Write("<data/>"); }
        using var reopened = new WorkbookInspection().Open(path); Assert.AreNotEqual(hash, reopened.Info.SourceSha256); Assert.ThrowsExactly<WorkbookException>(() => reopened.ReadViewport(id, 1, 1, 20, 6));
    }
    [TestMethod]
    public void NavigationProbeUsesBoundedRecentWindowsRatherThanZipOffsets()
    {
        var path = Package(cells: Rows(125000)); var hash = Hash(path); var results = new List<object>();
        for (var repeat = 0; repeat < 3; repeat++)
        {
            var watch = Stopwatch.StartNew(); var allocated = GC.GetTotalAllocatedBytes(); using var session = new WorkbookInspection().Open(path); var metadataMs = watch.Elapsed.TotalMilliseconds; var id = session.Info.Worksheets[0].Id;
            double Read(int row) { watch.Restart(); session.ReadViewport(id, row, 1, 20, 6); return watch.Elapsed.TotalMilliseconds; }
            var firstMs = Read(1); var sequentialMs = Read(21); var distantMs = Read(124980); var recentMs = Read(1);
            watch.Restart(); session.Dispose(); var disposeMs = watch.Elapsed.TotalMilliseconds;
            results.Add(new { repeat, metadataMs, firstMs, sequentialMs, distantMs, recentMs, disposeMs, allocatedBytes = GC.GetTotalAllocatedBytes() - allocated, processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 });
        }
        Assert.AreEqual(hash, Hash(path)); var json = JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }); TestContext.WriteLine(json);
        if (Environment.GetEnvironmentVariable("TSEKSHEET_PROBE_DIR") is { } dir) { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "step47-navigation.json"), json); }
    }
    [TestMethod]
    public void OversizedSharedTextRemainsBoundedAndMarkedTruncated()
    {
        var path = Package(cells: "<row><c r=\"A1\" t=\"s\"><v>0</v></c></row>", extra: new() { ["xl/sharedStrings.xml"] = $"<sst xmlns=\"{Ns}\"><si><t>{new string('x', 5000)}</t></si></sst>" }, extraRels: $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/>");
        using var session = new WorkbookInspection().Open(path); var c = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0];
        Assert.IsTrue(c.Truncated); Assert.AreEqual(1024, c.Value.Length); StringAssert.Contains(c.Interpretation!.Warning, "truncated");
    }
    [TestMethod]
    public void ManySheetNavigationProbeRetainsOnlyRecentWindows()
    {
        var path = Package(100, Rows(1000)); var hash = Hash(path); var timer = Stopwatch.StartNew(); using var session = new WorkbookInspection().Open(path); var metadataMs = timer.Elapsed.TotalMilliseconds;
        var times = new List<double>(); foreach (var sheet in session.Info.Worksheets.Take(10)) { timer.Restart(); session.ReadViewport(sheet.Id, 1, 1, 20, 6); times.Add(timer.Elapsed.TotalMilliseconds); }
        Assert.AreEqual(hash, Hash(path)); var json = JsonSerializer.Serialize(new { sheets = 100, cellsPerSheet = 1000, selectedSheets = 10, metadataMs, selectedScanMs = times, processPeakWorkingSet = Process.GetCurrentProcess().PeakWorkingSet64 });
        TestContext.WriteLine(json); if (Environment.GetEnvironmentVariable("TSEKSHEET_PROBE_DIR") is { } dir) File.WriteAllText(Path.Combine(dir, "step47-many-sheet-navigation.json"), json);
    }
}
