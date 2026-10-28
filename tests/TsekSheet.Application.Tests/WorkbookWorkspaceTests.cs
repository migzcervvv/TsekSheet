using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class WorkbookWorkspaceTests
{
    private sealed class Session : IWorkbookSession
    {
        public bool Disposed { get; private set; }
        public WorkbookInfo Info { get; private set; } = new(Guid.NewGuid(), null, "artificial.xlsx", "ABC", 0, false, true, "", "", [new(Guid.NewGuid(), "Data", 1, "Visible", 2, 2)], []);
        private Guid? selected;
        private readonly List<CellChange> changes = [];
        public CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns) => new(Info.SessionId, worksheetId, startRow, startColumn, rows, columns, []);
        public IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public IWorkbookSession CreateWorkingCopy(Guid worksheetId, int expectedRevision, CancellationToken cancellationToken = default) => new Session { selected = worksheetId, Info = Info with { SessionId = Guid.NewGuid(), ParentSessionId = Info.SessionId } };
        public WorkbookInfo Edit(CellEdit edit, CancellationToken cancellationToken = default) { Info = Info with { Revision = Info.Revision + 1, IsDirty = true }; changes.Add(new(edit.WorksheetId, edit.Row, edit.Column, edit.Kind, edit.Value, Info.Revision)); return Info; }
        public WorkingCopyInfo? WorkingCopy => selected is Guid id ? new(Info.SessionId, id, 0, Info.Revision, Info.IsDirty, changes.AsReadOnly()) : null;
        public int Saves; public string? LastDestination;
        public SaveReceipt SaveAsNew(string destination, int expectedRevision, CancellationToken cancellationToken = default)
        {
            Saves++; LastDestination = destination;
            return new("saved", Info.SessionId, Info.Revision, Info.IsDirty, false, false, new string('A', 64), "export.xlsx");
        }
        public void Dispose() => Disposed = true;
    }
    private sealed class Engine : IWorkbookEngine
    {
        public Session Current = new(); public bool Fail; public int Opens;
        public IWorkbookSession Open(string path, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual("native-only-path.xlsx", path); Opens++;
            if (Fail) throw new WorkbookException("INVALID_PACKAGE", "Unsupported workbook.");
            return Current;
        }
    }

    [TestMethod]
    public async Task FailedOrCancelledOpenRetainsPreviousSessionAndCloseDisposes()
    {
        var engine = new Engine(); var cancel = false;
        using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult(cancel ? null : "native-only-path.xlsx"));
        var opened = await workspace.OpenAsync(); Assert.AreEqual("opened", opened.Status);
        var info = opened.Workbook!;
        engine.Fail = true; Assert.AreEqual("INVALID_PACKAGE", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.OpenAsync())).Code);
        Assert.IsFalse(engine.Current.Disposed);
        cancel = true; Assert.AreEqual("cancelled", (await workspace.OpenAsync()).Status);
        Assert.AreEqual(info.SessionId, (await workspace.ReadAsync(info.SessionId, info.Worksheets[0].Id, 1, 1, 1, 1)).SessionId);
        Assert.AreEqual("SESSION_NOT_FOUND", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.CloseAsync(Guid.NewGuid()))).Code);
        await workspace.CloseAsync(info.SessionId); Assert.IsTrue(engine.Current.Disposed);
        Assert.AreEqual("SESSION_NOT_FOUND", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.ReadAsync(info.SessionId, info.Worksheets[0].Id, 1, 1, 1, 1))).Code);
    }

    [TestMethod]
    public async Task ConcurrentOpenIsRejectedAndDisposalPreventsLateAdoption()
    {
        var selection = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously); var engine = new Engine();
        using var workspace = new WorkbookWorkspace(engine, _ => selection.Task);
        var pending = workspace.OpenAsync();
        Assert.AreEqual("BUSY", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.OpenAsync())).Code);
        workspace.Dispose(); selection.SetResult("native-only-path.xlsx");
        await Assert.ThrowsAsync<OperationCanceledException>(() => pending); Assert.AreEqual(0, engine.Opens);
    }

    [TestMethod]
    public async Task BridgeValidatesWorkbookCommandsAndNeverAcceptsFrontendPaths()
    {
        var messages = new List<JsonElement>(); var engine = new Engine();
        using var bridge = new BridgeSession(json => messages.Add(JsonDocument.Parse(json).RootElement.Clone()), _ => Task.FromResult(new FileSelection("cancelled")),
            workbooks: new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx"), _ => Task.FromResult<string?>("native-only-destination.xlsx")));
        Task Send(string id, string command, object payload, string origin = BridgePolicy.Origin) => bridge.HandleAsync(origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = id, command, payload }));
        await Send("early", "workbook.open", new { }); Assert.AreEqual("NOT_READY", messages[^1].GetProperty("error").GetProperty("code").GetString());
        await Send("hello", "foundation.handshake", new { supportedVersions = new[] { 1 } });
        Assert.IsTrue(messages[^1].GetProperty("result").GetProperty("capabilities").GetProperty("workbooks").GetBoolean());
        await Send("path", "workbook.open", new { path = "forbidden.xlsx" }); Assert.AreEqual(0, engine.Opens);
        await Send("untrusted", "workbook.open", new { }, "https://evil.test"); Assert.AreEqual(0, engine.Opens);
        await Send("open", "workbook.open", new { });
        Assert.AreEqual("open", messages[^1].GetProperty("requestId").GetString()); Assert.AreEqual(1, engine.Opens);
        Assert.IsFalse(messages[^1].GetRawText().Contains("native-only-path", StringComparison.Ordinal));
        await Send("view", "workbook.readViewport", new { sessionId = engine.Current.Info.SessionId, worksheetId = engine.Current.Info.Worksheets[0].Id, startRow = 1, startColumn = 1, rows = 10, columns = 5 });
        Assert.IsTrue(messages[^1].GetProperty("ok").GetBoolean());
        await Send("copy", "workbook.createCopy", new { sessionId = engine.Current.Info.SessionId, worksheetId = engine.Current.Info.Worksheets[0].Id, expectedRevision = 0 });
        var copyId = messages[^1].GetProperty("result").GetProperty("sessionId").GetGuid();
        await Send("edit", "workbook.editCopy", new { sessionId = copyId, worksheetId = engine.Current.Info.Worksheets[0].Id, row = 1, column = 1, kind = "text", value = "changed", expectedRevision = 0 });
        Assert.IsTrue(messages[^1].GetProperty("result").GetProperty("isDirty").GetBoolean());
        await Send("info-after-edit", "foundation.getInfo", new { }); Assert.IsTrue(messages[^1].GetProperty("result").GetProperty("hasUnsavedWork").GetBoolean());
        await Send("close-dirty", "workbook.close", new { sessionId = engine.Current.Info.SessionId }); Assert.AreEqual("UNSAVED_CHANGES", messages[^1].GetProperty("error").GetProperty("code").GetString());
        await Send("bad", "workbook.readViewport", new { sessionId = "wrong" }); Assert.AreEqual("INVALID_REQUEST", messages[^1].GetProperty("error").GetProperty("code").GetString());
        await Send("save", "workbook.save", new { }); Assert.AreEqual("UNSUPPORTED_COMMAND", messages[^1].GetProperty("error").GetProperty("code").GetString());
        await Send("save-path", "workbook.saveAsNew", new { sessionId = engine.Current.Info.SessionId, expectedRevision = 0, path = "forbidden.xlsx" });
        Assert.AreEqual("INVALID_REQUEST", messages[^1].GetProperty("error").GetProperty("code").GetString()); Assert.AreEqual(0, engine.Current.Saves);
        await Send("save-copy", "workbook.saveAsNew", new { sessionId = copyId, expectedRevision = 1 }); Assert.AreEqual("WORKING_COPY_SAVE_UNSUPPORTED", messages[^1].GetProperty("error").GetProperty("code").GetString());
        await Send("save-new", "workbook.saveAsNew", new { sessionId = engine.Current.Info.SessionId, expectedRevision = 0 });
        Assert.IsTrue(messages[^1].GetProperty("ok").GetBoolean()); Assert.AreEqual(1, engine.Current.Saves);
        Assert.IsFalse(messages[^1].GetRawText().Contains("native-only-destination", StringComparison.Ordinal)); Assert.IsTrue(bridge.HasPendingWorkbookChanges);
        await Send("discard", "workbook.discard", new { sessionId = engine.Current.Info.SessionId }); Assert.AreEqual("closed", messages[^1].GetProperty("result").GetProperty("status").GetString());
        bridge.Dispose(); Assert.IsTrue(engine.Current.Disposed);
    }

    [TestMethod]
    public async Task PendingWorkingCopyRequiresExplicitDiscardAndNeverEditsMainSession()
    {
        var engine = new Engine(); using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx"));
        var main = (await workspace.OpenAsync()).Workbook!; var worksheet = main.Worksheets[0].Id;
        var copy = await workspace.CreateWorkingCopyAsync(main.SessionId, worksheet, 0);
        Assert.AreEqual("READ_ONLY_WORKBOOK", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.EditWorkingCopyAsync(main.SessionId, new(worksheet, 1, 1, "text", "x", 0)))).Code);
        copy = await workspace.EditWorkingCopyAsync(copy.SessionId, new(worksheet, 1, 1, "text", "x", 0));
        Assert.IsTrue(copy.IsDirty); Assert.AreEqual(1, copy.Revision); Assert.IsTrue(workspace.HasPendingChanges); Assert.IsFalse(engine.Current.Info.IsDirty);
        Assert.AreEqual("UNSAVED_CHANGES", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.CloseAsync(main.SessionId))).Code);
        Assert.AreEqual("UNSAVED_CHANGES", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.OpenAsync())).Code);
        Assert.AreEqual("closed", await workspace.CloseAsync(main.SessionId, true)); Assert.IsTrue(engine.Current.Disposed); Assert.IsFalse(workspace.HasPendingChanges);
    }

    [TestMethod]
    public async Task SaveRequiresFreshNativeSelectionAndRejectsStaleRevision()
    {
        var engine = new Engine(); var selections = 0; var cancel = true;
        using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx"), _ => { selections++; return Task.FromResult(cancel ? null : "native-destination.xlsx"); });
        var main = (await workspace.OpenAsync()).Workbook!;
        Assert.AreEqual("STALE_REVISION", (await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.SaveAsNewAsync(main.SessionId, 1))).Code); Assert.AreEqual(0, selections);
        Assert.AreEqual("cancelled", (await workspace.SaveAsNewAsync(main.SessionId, 0)).Status); Assert.AreEqual(0, engine.Current.Saves);
        cancel = false;
        Assert.AreEqual("saved", (await workspace.SaveAsNewAsync(main.SessionId, 0)).Status);
        Assert.AreEqual("saved", (await workspace.SaveAsNewAsync(main.SessionId, 0)).Status);
        Assert.AreEqual(3, selections); Assert.AreEqual(2, engine.Current.Saves);
    }
}
