using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;
using TsekSheet.TestFixtures;

namespace TsekSheet.Workbooks.Tests;

[TestClass, DoNotParallelize]
public sealed class WorkbookProjectOpeningTests
{
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    [TestMethod]
    [DataRow("cells", "LIMIT_EXCEEDED", "50001")]
    [DataRow("sheets", "LIMIT_EXCEEDED", "33")]
    [DataRow("external", "EXTERNAL_RESOURCE", "external relationship")]
    [DataRow("externalLink", "UNSUPPORTED_FEATURE", "external workbook link")]
    [DataRow("vml", "UNSUPPORTED_FEATURE", "VML")]
    [DataRow("image", "UNSUPPORTED_FEATURE", "image/media")]
    [DataRow("printer", "UNSUPPORTED_FEATURE", "printer settings")]
    [DataRow("protection", "PROTECTED_WORKBOOK", "sheetProtection")]
    public async Task RejectedEditablePackagesOpenAsInspectionProjectsWithoutNewAuthority(string feature, string code, string explanation)
    {
        using var fixture = new AccountingCompatibilityFixture(feature);
        var hash = Hash(fixture.Path); var engine = new WorkbookEngine(); var saveSelections = 0;
        var rejected = Assert.ThrowsExactly<WorkbookException>(() => engine.Open(fixture.Path));
        Assert.AreEqual(code, rejected.Code); StringAssert.Contains(rejected.Message, explanation);
        using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult<string?>(fixture.Path), _ => { saveSelections++; return Task.FromResult<string?>(fixture.Path + ".output.xlsx"); });
        var info = (await workspace.OpenAsync()).Workbook!;
        Assert.IsFalse(info.EditingAllowed); Assert.IsFalse(info.IsDirty); Assert.AreEqual(0, info.Revision);
        Assert.IsNotNull(info.Compatibility);
        Assert.IsTrue(info.Findings.Any(f => f.Code == "EDITABLE_ADMISSION_" + code && f.Message == rejected.Message));
        Assert.IsTrue(info.Compatibility.Capabilities.Where(c => c.Operation is not ("Metadata inspection" or "Worksheet selection" or "Read-only browsing")).All(c => !c.Allowed));
        foreach (var sheet in new[] { info.Worksheets[0], info.Worksheets[^1] })
            Assert.AreEqual("1", (await workspace.ReadAsync(info.SessionId, sheet.Id, 1, 1, 1, 1)).Cells[0].Value);
        Assert.AreEqual("Hidden", info.Worksheets[^1].Visibility);
        Assert.AreEqual("EDIT_UNSUPPORTED", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.CreateWorkingCopyAsync(info.SessionId, info.Worksheets[0].Id, 0))).Code);
        Assert.AreEqual("EDIT_UNSUPPORTED", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.SaveAsNewAsync(info.SessionId, 0))).Code);
        Assert.AreEqual("READ_ONLY_INSPECTION", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.PlanRecordsAsync(new(info.SessionId, info.Worksheets[0].Id, 0, "synthetic-payables", "", [])))).Code);
        Assert.AreEqual("READ_ONLY_WORKBOOK", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.EditWorkingCopyAsync(info.SessionId, new(info.Worksheets[0].Id, 1, 1, "text", "blocked", 0)))).Code);
        Assert.AreEqual(0, saveSelections); Assert.IsFalse(workspace.HasPendingChanges);
        await workspace.CloseAsync(info.SessionId);
        Assert.AreEqual(hash, Hash(fixture.Path)); Assert.IsFalse(File.Exists(fixture.Path + ".output.xlsx"));
    }

    [TestMethod]
    public async Task SwitchingBetweenInspectionAndEditableProjectsKeepsDirtyAndSessionGuards()
    {
        using var complex = new AccountingCompatibilityFixture("cells");
        using var ordinary = new AccountingCompatibilityFixture("plain");
        var complexHash = Hash(complex.Path); var ordinaryHash = Hash(ordinary.Path);
        var selected = complex.Path;
        using var workspace = new WorkbookWorkspace(new WorkbookEngine(), _ => Task.FromResult<string?>(selected));
        var inspection = (await workspace.OpenAsync()).Workbook!;
        selected = ordinary.Path; var editable = (await workspace.OpenAsync()).Workbook!;
        Assert.IsTrue(editable.EditingAllowed); Assert.IsNull(editable.Compatibility);
        Assert.AreEqual("SESSION_NOT_FOUND", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.ReadAsync(inspection.SessionId, inspection.Worksheets[0].Id, 1, 1, 1, 1))).Code);
        var copy = await workspace.CreateWorkingCopyAsync(editable.SessionId, editable.Worksheets[0].Id, 0);
        await workspace.EditWorkingCopyAsync(copy.SessionId, new(editable.Worksheets[0].Id, 1, 1, "number", "2", 0));
        selected = complex.Path;
        Assert.AreEqual("UNSAVED_CHANGES", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.OpenAsync())).Code);
        await workspace.CloseAsync(editable.SessionId, true);
        var reopened = (await workspace.OpenAsync()).Workbook!;
        Assert.IsFalse(reopened.EditingAllowed); Assert.AreNotEqual(inspection.SessionId, reopened.SessionId);
        Assert.AreEqual(complexHash, Hash(complex.Path)); Assert.AreEqual(ordinaryHash, Hash(ordinary.Path));
    }

    [TestMethod]
    public async Task FailedInspectionReportsBothReasonsAndRetainsPreviousProjectThroughBridge()
    {
        using var ordinary = new AccountingCompatibilityFixture("plain"); using var invalid = new AccountingCompatibilityFixture("broken");
        var hash = Hash(invalid.Path); var selected = ordinary.Path; var messages = new List<JsonElement>();
        using var bridge = new BridgeSession(json => messages.Add(JsonDocument.Parse(json).RootElement.Clone()), _ => Task.FromResult(new FileSelection("cancelled")),
            workbooks: new WorkbookWorkspace(new WorkbookEngine(), _ => Task.FromResult<string?>(selected)));
        Task Send(string id, string command, object payload) => bridge.HandleAsync(BridgePolicy.Origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = id, command, payload }));
        await Send("hello", "foundation.handshake", new { supportedVersions = new[] { 1 } });
        await Send("open", "workbook.open", new { });
        var original = messages[^1].GetProperty("result").GetProperty("workbook");
        selected = invalid.Path; await Send("invalid", "workbook.open", new { });
        Assert.IsFalse(messages[^1].GetProperty("ok").GetBoolean());
        var error = messages[^1].GetProperty("error"); Assert.AreEqual("INSPECTION_INVALID_XML", error.GetProperty("code").GetString());
        var details = error.GetProperty("details").EnumerateArray().ToArray(); Assert.AreEqual(2, details.Length);
        Assert.AreEqual("EDITABLE_ADMISSION_INVALID_PACKAGE", details[0].GetProperty("code").GetString());
        Assert.AreEqual("INSPECTION_INVALID_XML", details[1].GetProperty("code").GetString());
        await Send("read", "workbook.readViewport", new { sessionId = original.GetProperty("sessionId").GetString(), worksheetId = original.GetProperty("worksheets")[0].GetProperty("id").GetString(), startRow = 1, startColumn = 1, rows = 1, columns = 1 });
        Assert.IsTrue(messages[^1].GetProperty("ok").GetBoolean()); Assert.AreEqual(hash, Hash(invalid.Path));
    }

    [TestMethod]
    public async Task MetadataSuccessDoesNotConcealAnUnavailableWorksheetOrEnableAdvancedEditing()
    {
        using var malformedSheet = new AccountingCompatibilityFixture("badSheet");
        using var advanced = new AccountingCompatibilityFixture("advanced");
        using var workspace = new WorkbookWorkspace(new WorkbookEngine(), _ => Task.FromResult<string?>(malformedSheet.Path));
        var info = (await workspace.OpenAsync()).Workbook!;
        Assert.IsFalse(info.EditingAllowed); Assert.IsNotNull(info.Compatibility);
        Assert.AreEqual("1", (await workspace.ReadAsync(info.SessionId, info.Worksheets[0].Id, 1, 1, 1, 1)).Cells[0].Value);
        await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.ReadAsync(info.SessionId, info.Worksheets[1].Id, 1, 1, 1, 1));
        using var session = new WorkbookEngine().Open(advanced.Path);
        Assert.IsFalse(session.Info.EditingAllowed); Assert.IsTrue(session.Info.Findings.Any(f => f.Code == "ADVANCED_CONTENT"));
        Assert.ThrowsExactly<WorkbookException>(() => session.CreateWorkingCopy(session.Info.Worksheets[0].Id, 0));
        Assert.ThrowsExactly<WorkbookException>(() => session.SaveAsNew(advanced.Path + ".output.xlsx", 0));
    }
}
