using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class WorkbookEngineTests
{
    private static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", file + "-synthetic.xlsx");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [TestMethod]
    [DataRow("reimbursement", "Expense Draft", 10)] [DataRow("laboratory", "Lab Runs", 16)]
    public void OpenInspectSnapshotAndCloseNeverWriteOriginal(string file, string main, int summary)
    {
        var path = Fixture(file); var hash = Hash(path); var engine = new WorkbookEngine();
        Guid id;
        using (var session = engine.Open(path))
        {
            var info = session.Info; id = info.SessionId;
            Assert.AreEqual(hash, info.SourceSha256); Assert.IsFalse(info.IsDirty); Assert.AreEqual(0, info.Revision); Assert.IsTrue(info.EditingAllowed);
            Assert.AreEqual(4, info.Worksheets.Count); Assert.AreEqual("SyntheticFixtureGenerator", info.Creator);
            var hidden = info.Worksheets.Single(s => s.Name == "Lookup"); Assert.AreEqual("Hidden", hidden.Visibility);
            var sheet = info.Worksheets.Single(s => s.Name == main); Assert.AreEqual(summary + 5, sheet.LastRow); Assert.AreEqual(5, sheet.LastColumn);
            var view = session.ReadViewport(sheet.Id, 4, 1, 2, 5);
            Assert.IsTrue(view.Cells.Single(c => c.Row == 4 && c.Column == 1).Format.Bold);
            Assert.AreEqual(2, view.Cells.Single(c => c.Row == 5 && c.Column == 2).Format.NumberFormatId);
            Assert.AreEqual("B5*'Lookup'!$B$2", view.Cells.Single(c => c.Row == 5 && c.Column == 3).Formula);
            var overview = info.Worksheets.Single(s => s.Name == "Overview");
            Assert.AreEqual($"'{main}'!C{summary}", session.ReadViewport(overview.Id, 2, 2, 1, 1).Cells[0].Formula);
            using var snapshot = session.CreateSnapshot();
            using var secondSnapshot = session.CreateSnapshot();
            Assert.AreNotEqual(id, snapshot.Info.SessionId); Assert.AreEqual(id, snapshot.Info.ParentSessionId);
            Assert.AreNotEqual(snapshot.Info.SessionId, secondSnapshot.Info.SessionId); Assert.AreEqual(id, secondSnapshot.Info.ParentSessionId);
            Assert.AreEqual(sheet.Id, snapshot.Info.Worksheets.Single(s => s.Name == main).Id);
            // White-box probe only: production exposes no mutable workbook/edit API.
            var field = session.GetType().GetField("workbook", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var sourceModel = (XLWorkbook)field.GetValue(session)!; var copyModel = (XLWorkbook)field.GetValue(snapshot)!;
            Assert.AreNotSame(sourceModel, copyModel);
            Assert.AreNotSame(copyModel, field.GetValue(secondSnapshot));
            copyModel.Worksheet(main).Cell("A5").Value = "Snapshot-only probe";
            copyModel.Worksheet(main).Cell("B5").Style.Font.Bold = true;
            Assert.AreEqual("Sample-1", session.ReadViewport(sheet.Id, 5, 1, 1, 1).Cells[0].Value);
            Assert.IsFalse(session.ReadViewport(sheet.Id, 5, 2, 1, 1).Cells[0].Format.Bold);
            Assert.IsFalse(sourceModel.Worksheet(main).Cell("B5").Style.Font.Bold);
            Assert.AreEqual("Sample-1", sourceModel.Worksheet(main).Cell("A5").GetString());
            Assert.AreEqual("Sample-1", ((XLWorkbook)field.GetValue(secondSnapshot)!).Worksheet(main).Cell("A5").GetString());
            Assert.IsFalse(((XLWorkbook)field.GetValue(secondSnapshot)!).Worksheet(main).Cell("B5").Style.Font.Bold);
            Assert.AreEqual(hash, Hash(path));
        }
        Assert.AreEqual(hash, Hash(path));
        using var second = engine.Open(path); Assert.AreNotEqual(id, second.Info.SessionId);
    }

    [TestMethod]
    public void DisposalReleasesAdmissionBudgetAndSnapshotLivesIndependently()
    {
        var engine = new WorkbookEngine(new() { MaxSessions = 2 }); var session = engine.Open(Fixture("reimbursement"));
        var snapshot = session.CreateSnapshot();
        Assert.AreEqual("LIMIT_EXCEEDED", Assert.ThrowsExactly<WorkbookException>(() => engine.Open(Fixture("laboratory"))).Code);
        session.Dispose(); session.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => _ = session.Info);
        Assert.ThrowsExactly<ObjectDisposedException>(() => session.CreateSnapshot());
        Assert.ThrowsExactly<ObjectDisposedException>(() => session.ReadViewport(Guid.NewGuid(), 1, 1, 1, 1));
        Assert.AreEqual(4, snapshot.Info.Worksheets.Count);
        using var reopened = engine.Open(Fixture("laboratory")); snapshot.Dispose();
    }

    [TestMethod]
    public void SnapshotPreparationAndFailedAdmissionReleaseTheirMemoryReservations()
    {
        var engine = new WorkbookEngine(new() { MaxEstimatedSessionBytes = 10 * 1024 * 1024 });
        using (var session = engine.Open(Fixture("reimbursement")))
        {
            Assert.AreEqual("LIMIT_EXCEEDED", Assert.ThrowsExactly<WorkbookException>(() => session.CreateSnapshot()).Code);
            Assert.AreEqual(4, session.Info.Worksheets.Count);
        }
        using var reopened = engine.Open(Fixture("reimbursement")); Assert.AreEqual(4, reopened.Info.Worksheets.Count);
    }

    [TestMethod]
    public void ViewportsAndIdentitiesAreBounded()
    {
        using var session = new WorkbookEngine().Open(Fixture("reimbursement")); var sheet = session.Info.Worksheets[0].Id;
        foreach (var args in new[] { (0, 1, 1, 1), (1, 1, 21, 1), (1, 1, 1, 11), (1048576, 1, 2, 1), (1, 16384, 1, 2), (int.MaxValue, 1, 1, 1) })
            Assert.AreEqual("INVALID_VIEWPORT", Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(sheet, args.Item1, args.Item2, args.Item3, args.Item4)).Code);
        Assert.AreEqual("UNKNOWN_WORKSHEET", Assert.ThrowsExactly<WorkbookException>(() => session.ReadViewport(Guid.NewGuid(), 1, 1, 1, 1)).Code);
    }

    [TestMethod]
    public void EmptyViewportReadsDoNotGrowTheMutableWorkbookModel()
    {
        using var session = new WorkbookEngine().Open(Fixture("reimbursement")); var sheet = session.Info.Worksheets[0];
        var field = session.GetType().GetField("workbook", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var model = (XLWorkbook)field.GetValue(session)!; var before = model.Worksheet(sheet.Name).CellsUsed(XLCellsUsedOptions.All).Count();
        for (var i = 0; i < 100; i++) Assert.AreEqual(200, session.ReadViewport(sheet.Id, 1000 + i * 20, 100, 20, 10).Cells.Count);
        Assert.AreEqual(before, model.Worksheet(sheet.Name).CellsUsed(XLCellsUsedOptions.All).Count());
    }

    [TestMethod]
    public void AdvancedContentRemainsInspectableButBlocksSnapshots()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-core-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "advanced.xlsx");
        try
        {
            File.Copy(Fixture("laboratory"), path);
            using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; string xml;
                using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                var name = entry.FullName; entry.Delete(); var document = System.Xml.Linq.XDocument.Parse(xml); var ns = document.Root!.Name.Namespace;
                document.Root.Element(ns + "pageMargins")!.AddBeforeSelf(new System.Xml.Linq.XElement(ns + "conditionalFormatting", new System.Xml.Linq.XAttribute("sqref", "B5"),
                    new System.Xml.Linq.XElement(ns + "cfRule", new System.Xml.Linq.XAttribute("type", "expression"), new System.Xml.Linq.XAttribute("priority", "1"), new System.Xml.Linq.XElement(ns + "formula", "B5>0"))));
                using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(document.ToString());
            }
            var hash = Hash(path);
            using (var session = new WorkbookEngine().Open(path))
            {
                Assert.IsTrue(session.Info.Findings.Any(f => f.Code == "ADVANCED_CONTENT"));
                Assert.IsFalse(session.Info.EditingAllowed);
                Assert.AreEqual("SNAPSHOT_UNSUPPORTED", Assert.ThrowsExactly<WorkbookException>(() => session.CreateSnapshot()).Code);
                Assert.AreEqual("EDIT_UNSUPPORTED", Assert.ThrowsExactly<WorkbookException>(() => session.Edit(new(session.Info.Worksheets[0].Id, 5, 1, "text", "blocked", 0))).Code);
                Assert.AreEqual("Sample-1", session.ReadViewport(session.Info.Worksheets[0].Id, 5, 1, 1, 1).Cells[0].Value);
            }
            Assert.AreEqual(hash, Hash(path));
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }

    [TestMethod]
    public void FilePackageAndSessionLimitsAreEnforcedBeforeLoading()
    {
        var path = Fixture("reimbursement");
        foreach (var limits in new[] { new WorkbookLimits { MaxFileBytes = 10 }, new() { MaxExpandedBytes = 100 }, new() { MaxEntries = 1 }, new() { MaxCells = 1 }, new() { MaxWorksheets = 1 }, new() { MaxEstimatedSessionBytes = 1024 } })
            Assert.AreEqual("LIMIT_EXCEEDED", Assert.ThrowsExactly<WorkbookException>(() => new WorkbookEngine(limits).Open(path)).Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new WorkbookEngine().Open(path, cancelled.Token));
    }

    [TestMethod]
    [DataRow("corrupt", "INVALID_PACKAGE")] [DataRow("encrypted", "PROTECTED_WORKBOOK")]
    [DataRow("protected", "PROTECTED_WORKBOOK")] [DataRow("external", "EXTERNAL_RESOURCE")]
    [DataRow("macro", "UNSUPPORTED_FEATURE")] [DataRow("dtd", "INVALID_PACKAGE")]
    [DataRow("depth", "LIMIT_EXCEEDED")]
    [DataRow("duplicate", "INVALID_PACKAGE")] [DataRow("dangling", "INVALID_PACKAGE")]
    public void UnsafePackagesFailWithoutChangingTheirBytes(string mode, string code)
    {
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-core-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "unsafe.xlsx");
        try
        {
            File.Copy(Fixture("reimbursement"), path);
            if (mode == "corrupt") File.WriteAllText(path, "not a ZIP");
            else if (mode == "encrypted") File.WriteAllBytes(path, [0xD0, 0xCF, 0, 0]);
            else
            {
                using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
                if (mode == "macro") { using var writer = new StreamWriter(zip.CreateEntry("xl/vbaProject.bin").Open()); writer.Write("synthetic"); }
                else if (mode == "duplicate") { using var writer = new StreamWriter(zip.CreateEntry("xl/workbook.xml").Open()); writer.Write("duplicate"); }
                else
                {
                    var entry = zip.GetEntry(mode == "protected" ? "xl/worksheets/sheet1.xml" : mode == "depth" ? "customXml/item.xml" : "_rels/.rels")!;
                    string xml; using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                    var name = entry.FullName; entry.Delete();
                    if (mode == "protected")
                    {
                        var document = System.Xml.Linq.XDocument.Parse(xml);
                        document.Root!.Add(new System.Xml.Linq.XElement(document.Root.Name.Namespace + "sheetProtection", new System.Xml.Linq.XAttribute("sheet", "1")));
                        xml = document.ToString();
                    }
                    xml = mode switch
                    {
                        "protected" => xml.Replace("</worksheet>", "<sheetProtection sheet=\"1\"/></worksheet>"),
                        "external" => xml.Replace("</Relationships>", "<Relationship Id=\"evil\" Type=\"http://test\" TargetMode=\"External\" Target=\"https://example.com/a\"/></Relationships>"),
                        "dangling" => xml.Replace("</Relationships>", "<Relationship Id=\"missing\" Type=\"http://test\" Target=\"missing.xml\"/></Relationships>"),
                        "dtd" => "<!DOCTYPE Relationships [<!ENTITY x SYSTEM 'file:///test'>]>" + xml[(xml.IndexOf("?>", StringComparison.Ordinal) + 2)..],
                        "depth" => string.Concat(Enumerable.Repeat("<n>", 66)) + string.Concat(Enumerable.Repeat("</n>", 66)),
                        _ => xml
                    };
                    using var writer = new StreamWriter(zip.CreateEntry(name).Open()); writer.Write(xml);
                }
            }
            var hash = Hash(path); var error = Assert.ThrowsExactly<WorkbookException>(() => new WorkbookEngine().Open(path));
            Assert.AreEqual(code, error.Code, error.Message); Assert.AreEqual(hash, Hash(path)); Assert.IsFalse(error.Message.Contains(folder, StringComparison.Ordinal));
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }

    [TestMethod]
    public void UnsupportedExtensionsAndNetworkLocationsAreRejected()
    {
        Assert.AreEqual("UNSUPPORTED_FORMAT", Assert.ThrowsExactly<WorkbookException>(() => new WorkbookEngine().Open("anything.xlsm")).Code);
        Assert.AreEqual("UNSUPPORTED_LOCATION", Assert.ThrowsExactly<WorkbookException>(() => new WorkbookEngine().Open("\\\\server\\share\\book.xlsx")).Code);
    }

    [TestMethod]
    public void WorkbookAndApplicationAssembliesHaveNoUiDependencyAndContractsHaveNoLibraryDependency()
    {
        foreach (var assembly in new[] { typeof(WorkbookEngine).Assembly, typeof(IWorkbookEngine).Assembly })
            Assert.IsFalse(assembly.GetReferencedAssemblies().Any(a => new[] { "PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "TsekSheet.Desktop" }.Contains(a.Name) || a.Name!.StartsWith("Microsoft.Web.WebView2", StringComparison.Ordinal)));
        Assert.IsFalse(typeof(IWorkbookEngine).Assembly.GetReferencedAssemblies().Any(a => a.Name!.StartsWith("ClosedXML", StringComparison.Ordinal) || a.Name.StartsWith("DocumentFormat.OpenXml", StringComparison.Ordinal)));
    }
}
