using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Foundation;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class WorksheetIntegrationTests
{
    private static string Fixture(string file = "reimbursement") => Path.Combine(AppContext.BaseDirectory, "Fixtures", file + "-synthetic.xlsx");
    private static CellInfo Cell(IWorkbookSession session, Guid sheet, int row = 5, int column = 1) => session.ReadViewport(sheet, row, column, 1, 1).Cells[0];
    private static WorksheetIntegrationRequest Request(IWorkbookSession main, IWorkbookSession copy) => new(copy.Info.SessionId,
        copy.WorkingCopy!.SourceWorksheetId, copy.Info.Revision, main.Info.Revision, copy.WorkingCopy.BaseRevision);

    [TestMethod]
    [DataRow("reimbursement")]
    [DataRow("laboratory")]
    public void MergePreservesAllContextAndExportsWithoutOverwritingSource(string fixture)
    {
        var path = Fixture(fixture); var hash = SHA256.HashData(File.ReadAllBytes(path));
        var engine = new WorkbookEngine(); using var main = engine.Open(path); var sheet = main.Info.Worksheets[0].Id;
        var metadata = main.Info.Worksheets.ToArray();
        var before = main.Info.Worksheets.ToDictionary(s => s.Id, s => main.ReadViewport(s.Id, 1, 1, 20, 6).Cells.ToArray());
        using var copy = main.CreateWorkingCopy(sheet, 0);
        copy.Edit(new(sheet, 5, 1, "text", "Merged", 0));
        copy.Edit(new(sheet, 5, 2, "number", "123.5", 1));
        copy.Edit(new(sheet, 5, 3, "formula", "B5*'Lookup'!$B$2+1", 2));
        copy.Edit(new(sheet, 5, 5, "formula", $"'{metadata[0].Name}'!$B5+B$5", 3));
        copy.Edit(new(sheet, 5, 4, "date", "2024-01-02", 4));
        var request = Request(main, copy); var preview = main.PreviewIntegration(copy, request);
        Assert.IsTrue(preview.CanApply); Assert.AreEqual(5, preview.PendingCells); Assert.IsFalse(main.Info.IsDirty);
        var result = main.Integrate(copy, request);
        Assert.AreEqual(5, result.AppliedCells); Assert.AreEqual(1, main.Info.Revision); Assert.IsTrue(main.Info.IsDirty);
        CollectionAssert.AreEqual(metadata, main.Info.Worksheets.ToArray());
        foreach (var s in main.Info.Worksheets)
        foreach (var original in before[s.Id])
        {
            var expected = s.Id == sheet && original.Row == 5 && original.Column <= 5 ? Cell(copy, sheet, 5, original.Column) : original;
            Assert.AreEqual(expected, Cell(main, s.Id, original.Row, original.Column));
        }
        Assert.AreEqual(5, copy.WorkingCopy!.Changes.Count); Assert.IsTrue(copy.Info.IsDirty);
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-integration-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "merged.xlsx");
        try
        {
            var receipt = main.SaveAsNew(target, 1); Assert.AreEqual(1, receipt.SavedRevision);
            using var reopened = engine.Open(target); Assert.AreEqual("Merged", Cell(reopened, reopened.Info.Worksheets[0].Id).Value);
            Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(target, 1)).Code);
            Assert.IsTrue(main.Info.IsDirty);
        }
        finally { File.Delete(target); Directory.Delete(folder); }
        CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(path)));
    }

    [TestMethod]
    public void RepeatedEditsCollapseAndRevertedValuesAreNoOps()
    {
        using var main = new WorkbookEngine().Open(Fixture()); var sheet = main.Info.Worksheets[0].Id;
        using var copy = main.CreateWorkingCopy(sheet, 0);
        Assert.AreEqual(0, main.Integrate(copy, Request(main, copy)).AppliedCells);
        copy.Edit(new(sheet, 5, 1, "text", "temporary", 0)); copy.Edit(new(sheet, 5, 1, "text", "Sample-1", 1));
        Assert.AreEqual(0, main.PreviewIntegration(copy, Request(main, copy)).PendingCells);
        Assert.AreEqual(0, main.Integrate(copy, Request(main, copy)).AppliedCells);
        Assert.AreEqual(0, main.Info.Revision); Assert.IsFalse(main.Info.IsDirty);
        copy.Edit(new(sheet, 5, 1, "text", "final", 2));
        Assert.AreEqual(1, main.Integrate(copy, Request(main, copy)).AppliedCells); Assert.AreEqual("final", Cell(main, sheet).Value);
    }

    [TestMethod]
    public void UnchangedFormulaDoesNotMergeAChangedCalculationCache()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-cache-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "cached.xlsx"); File.Copy(Fixture(), path);
        try
        {
            using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path, true))
            {
                var cell = document.WorkbookPart!.WorksheetParts.SelectMany(p => p.Worksheet!.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>())
                    .First(c => c.CellFormula != null && c.CellReference?.Value == "C5");
                cell.CellValue = new DocumentFormat.OpenXml.Spreadsheet.CellValue("123");
            }
            var hash = SHA256.HashData(File.ReadAllBytes(path));
            using var main = new WorkbookEngine().Open(path); var sheet = main.Info.Worksheets[0].Id;
            using var copy = main.CreateWorkingCopy(sheet, 0); var before = Cell(main, sheet, 5, 3);
            copy.Edit(new(sheet, 5, 3, "formula", before.Formula!, 0));
            Assert.AreEqual(0, main.PreviewIntegration(copy, Request(main, copy)).PendingCells);
            Assert.AreEqual(0, main.Integrate(copy, Request(main, copy)).AppliedCells);
            Assert.AreEqual(before, Cell(main, sheet, 5, 3)); Assert.IsFalse(main.Info.IsDirty);
            CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(path)));
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void AnyDestinationChangeRejectsTheWholeMerge(bool sameCell)
    {
        using var main = new WorkbookEngine().Open(Fixture()); var sheet = main.Info.Worksheets[0].Id;
        using var copy = main.CreateWorkingCopy(sheet, 0); copy.Edit(new(sheet, 5, 1, "text", "copy", 0));
        main.Edit(new(sheet, sameCell ? 5 : 6, 1, "text", "destination", 0));
        var preview = main.PreviewIntegration(copy, Request(main, copy)); Assert.IsFalse(preview.CanApply);
        Assert.AreEqual("DESTINATION_CONFLICT", Assert.ThrowsExactly<WorkbookException>(() => main.Integrate(copy, Request(main, copy))).Code);
        Assert.AreEqual(1, main.Info.Revision); Assert.AreEqual("copy", Cell(copy, sheet).Value);
        Assert.AreEqual("destination", Cell(main, sheet, sameCell ? 5 : 6).Value);
    }

    [TestMethod]
    [DataRow("copy")] [DataRow("destination")] [DataRow("base")] [DataRow("sheet")] [DataRow("identity")]
    public void InvalidFrozenRequestsAreRejected(string field)
    {
        using var main = new WorkbookEngine().Open(Fixture()); var sheet = main.Info.Worksheets[0].Id;
        using var copy = main.CreateWorkingCopy(sheet, 0); copy.Edit(new(sheet, 5, 1, "text", "copy", 0));
        var r = Request(main, copy);
        r = field switch {
            "copy" => r with { ExpectedCopyRevision = 0 }, "destination" => r with { ExpectedDestinationRevision = 1 },
            "base" => r with { ExpectedBaseRevision = 1 }, "sheet" => r with { DestinationWorksheetId = Guid.NewGuid() },
            _ => r with { WorkingCopyId = Guid.NewGuid() }
        };
        Assert.IsFalse(main.PreviewIntegration(copy, r).CanApply);
        Assert.ThrowsExactly<WorkbookException>(() => main.Integrate(copy, r));
        Assert.AreEqual(0, main.Info.Revision); Assert.IsFalse(main.Info.IsDirty); Assert.AreEqual("Sample-1", Cell(main, sheet).Value);
    }

    [TestMethod]
    public void ForeignCopiesAndSnapshotsCannotBeIntegrated()
    {
        var engine = new WorkbookEngine(); using var main = engine.Open(Fixture()); using var foreign = engine.Open(Fixture());
        using var copy = foreign.CreateWorkingCopy(foreign.Info.Worksheets[0].Id, 0);
        Assert.IsFalse(main.PreviewIntegration(copy, Request(main, copy)).CanApply);
        using var snapshot = main.CreateSnapshot();
        Assert.ThrowsExactly<WorkbookException>(() => main.Integrate(snapshot, Request(main, copy)));
    }

    [TestMethod]
    public void MultipleCopiesRemainIndependentAndEarlierCopiesBecomeStale()
    {
        using var main = new WorkbookEngine().Open(Fixture()); var sheet = main.Info.Worksheets[0].Id;
        using var a = main.CreateWorkingCopy(sheet, 0); using var b = main.CreateWorkingCopy(sheet, 0);
        a.Edit(new(sheet, 5, 1, "text", "a", 0)); b.Edit(new(sheet, 6, 1, "text", "b", 0));
        main.Integrate(a, Request(main, a));
        Assert.IsFalse(main.PreviewIntegration(b, Request(main, b)).CanApply);
        Assert.IsFalse(main.PreviewIntegration(a, Request(main, a)).CanApply);
        Assert.AreEqual("b", Cell(b, sheet, 6).Value); Assert.AreEqual("Sample-2", Cell(main, sheet, 6).Value);
    }

    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public void CandidateFailureAndLateCancellationRollBackMultipleEdits(bool cancel)
    {
        var engine = new WorkbookEngine(); using var main = engine.Open(Fixture()); var sheet = main.Info.Worksheets[0].Id;
        using var copy = main.CreateWorkingCopy(sheet, 0);
        copy.Edit(new(sheet, 5, 1, "text", "one", 0)); copy.Edit(new(sheet, 6, 1, "text", "two", 1));
        using var cts = new CancellationTokenSource(); var calls = 0;
        engine.CandidateFaultForTesting = bytes => {
            if (++calls < 2) return bytes;
            if (cancel) { cts.Cancel(); return bytes; }
            using var memory = new MemoryStream(); memory.Write(bytes); memory.Position = 0;
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true)) {
                var entry = zip.GetEntry("customXml/item.xml")!; entry.Delete();
                using var writer = new StreamWriter(zip.CreateEntry("customXml/item.xml").Open()); writer.Write("<tampered/>");
            }
            return memory.ToArray();
        };
        if (cancel) Assert.ThrowsExactly<OperationCanceledException>(() => main.Integrate(copy, Request(main, copy), cts.Token));
        else Assert.ThrowsExactly<WorkbookException>(() => main.Integrate(copy, Request(main, copy)));
        Assert.AreEqual(0, main.Info.Revision); Assert.IsFalse(main.Info.IsDirty);
        Assert.AreEqual("Sample-1", Cell(main, sheet).Value); Assert.AreEqual("Sample-2", Cell(main, sheet, 6).Value);
        Assert.AreEqual(2, copy.Info.Revision); Assert.AreEqual("two", Cell(copy, sheet, 6).Value);
        engine.CandidateFaultForTesting = null;
        Assert.AreEqual(2, main.Integrate(copy, Request(main, copy)).AppliedCells);
    }

    [TestMethod]
    [DataRow(WorksheetIntegrationOperation.Replace)] [DataRow(WorksheetIntegrationOperation.CreateNew)]
    public void UnsafeOperationsRemainBlocked(WorksheetIntegrationOperation operation)
    {
        using var main = new WorkbookEngine().Open(Fixture()); using var copy = main.CreateWorkingCopy(main.Info.Worksheets[0].Id, 0);
        var r = Request(main, copy) with { Operation = operation };
        Assert.IsFalse(main.PreviewIntegration(copy, r).CanApply);
        Assert.AreEqual("UNSUPPORTED_OPERATION", Assert.ThrowsExactly<WorkbookException>(() => main.Integrate(copy, r)).Code);
        Assert.AreEqual(4, main.Info.Worksheets.Count); Assert.IsFalse(main.Info.IsDirty);
    }

    [TestMethod]
    public async Task RealWorkspaceBridgeValidatesMergePayloadAndRetainsCopy()
    {
        var messages = new List<JsonElement>();
        using var workspace = new WorkbookWorkspace(new WorkbookEngine(), _ => Task.FromResult<string?>(Fixture()));
        using var bridge = new BridgeSession(json => messages.Add(JsonDocument.Parse(json).RootElement.Clone()),
            _ => Task.FromResult(new FileSelection("cancelled")), workbooks: workspace);
        var sequence = 0;
        async Task<JsonElement> Send(string command, object payload)
        {
            await bridge.HandleAsync(BridgePolicy.Origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = "integration-" + sequence++, command, payload }));
            return messages[^1];
        }
        await Send("foundation.handshake", new { supportedVersions = new[] { 1 } });
        var main = (await Send("workbook.open", new { })).GetProperty("result").GetProperty("workbook");
        var mainId = main.GetProperty("sessionId").GetGuid(); var sheet = main.GetProperty("worksheets")[0].GetProperty("id").GetGuid();
        var copyId = (await Send("workbook.createCopy", new { sessionId = mainId, worksheetId = sheet, expectedRevision = 0 })).GetProperty("result").GetProperty("sessionId").GetGuid();
        await Send("workbook.editCopy", new { sessionId = copyId, worksheetId = sheet, expectedRevision = 0, row = 5, column = 1, kind = "text", value = "bridge merge" });
        var payload = new { sessionId = mainId, workingCopyId = copyId, worksheetId = sheet, expectedCopyRevision = 1, expectedRevision = 0, expectedBaseRevision = 0 };
        var preview = await Send("workbook.previewMerge", payload);
        Assert.IsTrue(preview.GetProperty("result").GetProperty("canApply").GetBoolean());
        Assert.AreEqual(1, preview.GetProperty("result").GetProperty("pendingCells").GetInt32());
        var bad = await Send("workbook.merge", new { sessionId = mainId, workingCopyId = copyId, worksheetId = sheet, expectedCopyRevision = 1, expectedRevision = 0, expectedBaseRevision = 0, path = "forbidden.xlsx" });
        Assert.AreEqual("INVALID_REQUEST", bad.GetProperty("error").GetProperty("code").GetString());
        var result = await Send("workbook.merge", payload);
        Assert.AreEqual(1, result.GetProperty("result").GetProperty("workbook").GetProperty("revision").GetInt32());
        Assert.IsTrue(bridge.HasPendingWorkbookChanges);
        Assert.AreEqual("bridge merge", (await workspace.ReadAsync(copyId, sheet, 5, 1, 1, 1)).Cells[0].Value);
        Assert.AreEqual("bridge merge", (await workspace.ReadAsync(mainId, sheet, 5, 1, 1, 1)).Cells[0].Value);
        Assert.IsFalse(result.GetRawText().Contains(Path.GetDirectoryName(Fixture())!, StringComparison.Ordinal));
    }
}
