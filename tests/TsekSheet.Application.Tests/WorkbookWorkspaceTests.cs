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
        public CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns) => new(Info.SessionId, worksheetId, startRow, startColumn, rows, columns, []);
        public IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() => Disposed = true;
    }
    private sealed class Engine : IWorkbookEngine
    {
        public Session Current = new(); public bool Fail; public int Opens; public Exception? OpenFailure; public int Inspections;
        public IWorkbookSession Open(string path, CancellationToken cancellationToken = default)
        {
            Assert.AreEqual("native-only-path.xlsx", path); Opens++;
            if (OpenFailure != null) throw OpenFailure;
            if (Fail) throw new WorkbookException("INVALID_PACKAGE", "Unsupported workbook.");
            return Current;
        }
        public IWorkbookSession Inspect(string path, CancellationToken cancellationToken = default)
        {
            Inspections++;
            throw new WorkbookException("INSPECTION_UNAVAILABLE", "Read-only package inspection is unavailable.");
        }
    }

    [TestMethod]
    [DataRow("UNSUPPORTED_LOCATION")]
    [DataRow("UNSUPPORTED_FORMAT")]
    [DataRow("FILE_UNAVAILABLE")]
    [DataRow("BUSY")]
    public async Task OpeningDoesNotRetryLocationFormatAccessOrBusyFailures(string code)
    {
        var engine = new Engine { OpenFailure = new WorkbookException(code, "Fixed authoritative rejection.") };
        using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx"));
        var failure = await Assert.ThrowsExactlyAsync<WorkbookException>(() => workspace.OpenAsync());
        Assert.AreEqual(code, failure.Code); Assert.AreEqual(0, engine.Inspections);
    }

    [TestMethod]
    public async Task CancellationIsNeverRetriedAsInspection()
    {
        var engine = new Engine { OpenFailure = new OperationCanceledException() };
        using var workspace = new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx"));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => workspace.OpenAsync());
        Assert.AreEqual(0, engine.Inspections);
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
            workbooks: new WorkbookWorkspace(engine, _ => Task.FromResult<string?>("native-only-path.xlsx")));
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
        foreach (var command in new[] { "workbook.createCopy", "workbook.editCopy", "workbook.discard", "workbook.merge", "workbook.previewMerge", "workbook.generateCandidate", "workbook.approveCandidate", "workbook.cancelCandidate", "workbook.saveAsNew" })
        {
            await Send(command.Replace('.', '-'), command, new { });
            Assert.AreEqual("UNSUPPORTED_COMMAND", messages[^1].GetProperty("error").GetProperty("code").GetString());
        }
        await Send("close", "workbook.close", new { sessionId = engine.Current.Info.SessionId });
        Assert.AreEqual("closed", messages[^1].GetProperty("result").GetProperty("status").GetString());
        bridge.Dispose(); Assert.IsTrue(engine.Current.Disposed);
    }

}
