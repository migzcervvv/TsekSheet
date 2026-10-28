using System.Text;
using System.Text.Json;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Application.Foundation;

public sealed record FileSelection(string Status, string? SelectionId = null, string? DisplayName = null);

public static class BridgePolicy
{
    public const string Origin = "https://tseksheet.local";
    public static bool IsTrusted(string source) => Uri.TryCreate(source, UriKind.Absolute, out var uri)
        && uri.Scheme == "https" && uri.Host.Equals("tseksheet.local", StringComparison.OrdinalIgnoreCase)
        && uri.Port == 443 && uri.UserInfo.Length == 0;
}

// One instance per top-level page. No native APIs, UI types, or file paths cross this boundary.
public sealed class BridgeSession : IDisposable
{
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public const int MaxMessageBytes = 16 * 1024;
    private readonly Action<string> send;
    private readonly Func<CancellationToken, Task<FileSelection>> selectFile;
    private readonly Func<int, CancellationToken, Task> delay;
    private readonly CancellationTokenSource lifetime = new();
    private readonly HashSet<string> seen = new(StringComparer.Ordinal);
    private readonly object gate = new();
    private CancellationTokenSource? operation;
    private string? operationId;
    private bool ready, selecting, disposed;
    private readonly WorkbookWorkspace? workbooks;
    public bool HasPendingWorkbookChanges => workbooks?.HasPendingChanges == true;

    public BridgeSession(Action<string> send, Func<CancellationToken, Task<FileSelection>> selectFile,
        Func<int, CancellationToken, Task>? delay = null, WorkbookWorkspace? workbooks = null)
    {
        this.send = send;
        this.selectFile = selectFile;
        this.delay = delay ?? ((ms, token) => Task.Delay(ms, token));
        this.workbooks = workbooks;
    }

    private void Emit(object message)
    {
        lock (gate) if (!disposed) send(JsonSerializer.Serialize(message, JsonOptions));
    }
    private void Success(string id, object result) => Emit(new { protocolVersion = 1, type = "response", requestId = id, ok = true, result });
    private void Error(string id, string code, string message) => Emit(new { protocolVersion = 1, type = "response", requestId = id, ok = false, error = new { code, message } });
    private static bool ValidId(string? id) => id is { Length: > 0 and <= 64 } && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static bool HasOnly(JsonElement value, params string[] names) => value.ValueKind == JsonValueKind.Object
        && value.EnumerateObject().Select(p => p.Name).Distinct().Count() == value.EnumerateObject().Count()
        && value.EnumerateObject().All(p => names.Contains(p.Name)) && names.All(n => value.TryGetProperty(n, out _));

    public async Task HandleAsync(string source, string json)
    {
        if (!BridgePolicy.IsTrusted(source) || Encoding.UTF8.GetByteCount(json) > MaxMessageBytes) return;
        string? id = null;
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return;
            if (root.TryGetProperty("requestId", out var idValue) && idValue.ValueKind == JsonValueKind.String) id = idValue.GetString();
            if (!ValidId(id)) return;
            if (!HasOnly(root, "protocolVersion", "requestId", "command", "payload") || !root.GetProperty("protocolVersion").TryGetInt32(out var version))
            { Error(id!, "INVALID_REQUEST", "Invalid request envelope."); return; }
            if (version != 1) { Error(id!, "UNSUPPORTED_VERSION", "Protocol version 1 is required."); return; }
            var commandValue = root.GetProperty("command");
            if (commandValue.ValueKind != JsonValueKind.String) { Error(id!, "INVALID_REQUEST", "Invalid command."); return; }
            var command = commandValue.GetString();
            var payload = root.GetProperty("payload");
            lock (gate)
            {
                if (disposed) return;
                if (seen.Contains(id!)) { Error(id!, "DUPLICATE_REQUEST", "Request ID has already been used."); return; }
                if (seen.Count >= 4096) { Error(id!, "LIMIT_EXCEEDED", "Reload the page to begin a new session."); return; }
                seen.Add(id!);
            }
            if (command == "foundation.handshake")
            {
                if (!HasOnly(payload, "supportedVersions") || payload.GetProperty("supportedVersions") is not { ValueKind: JsonValueKind.Array } versions
                    || versions.GetArrayLength() is < 1 or > 4 || versions.EnumerateArray().Any(v => !v.TryGetInt32(out _)))
                { Error(id!, "INVALID_REQUEST", "Invalid supported versions."); return; }
                if (!versions.EnumerateArray().Any(v => v.GetInt32() == 1)) { Error(id!, "UNSUPPORTED_VERSION", "No compatible version."); return; }
                ready = true;
                Success(id!, new { selectedVersion = 1, applicationName = new ApplicationMetadataService().ApplicationName, version = "0.1.0", capabilities = new { diagnostics = true, fileSelection = true, workbooks = workbooks != null, templates = false, ocr = false } });
                return;
            }
            if (command is not ("foundation.getInfo" or "foundation.selectFile" or "diagnostics.run" or "diagnostics.cancel" or "workbook.open" or "workbook.readViewport" or "workbook.close" or "workbook.discard" or "workbook.createCopy" or "workbook.editCopy" or "workbook.saveAsNew"))
            { Error(id!, "UNSUPPORTED_COMMAND", "Command is unavailable."); return; }
            if (!ready) { Error(id!, "NOT_READY", "Handshake is required."); return; }
            if (command!.StartsWith("workbook.", StringComparison.Ordinal))
            {
                if (workbooks == null) { Error(id!, "UNSUPPORTED_COMMAND", "Read-only workbooks are unavailable in this host."); return; }
                if (command == "workbook.open")
                {
                    if (!HasOnly(payload)) { Error(id!, "INVALID_REQUEST", "Workbook opening accepts no paths or other arguments."); return; }
                    Success(id!, await workbooks.OpenAsync()); return;
                }
                var names = command switch
                {
                    "workbook.close" or "workbook.discard" => new[] { "sessionId" },
                    "workbook.saveAsNew" => new[] { "sessionId", "expectedRevision" },
                    "workbook.createCopy" => new[] { "sessionId", "worksheetId", "expectedRevision" },
                    "workbook.editCopy" => new[] { "sessionId", "worksheetId", "row", "column", "kind", "value", "expectedRevision" },
                    _ => new[] { "sessionId", "worksheetId", "startRow", "startColumn", "rows", "columns" }
                };
                if (!HasOnly(payload, names) || payload.GetProperty("sessionId").ValueKind != JsonValueKind.String || !Guid.TryParseExact(payload.GetProperty("sessionId").GetString(), "D", out var sessionId))
                { Error(id!, "INVALID_REQUEST", "Invalid workbook session payload."); return; }
                if (command is "workbook.close" or "workbook.discard") { Success(id!, new { status = await workbooks.CloseAsync(sessionId, command == "workbook.discard") }); return; }
                if (command == "workbook.saveAsNew")
                {
                    if (!payload.GetProperty("expectedRevision").TryGetInt32(out var revision) || revision < 0) { Error(id!, "INVALID_REQUEST", "Invalid revision."); return; }
                    Success(id!, await workbooks.SaveAsNewAsync(sessionId, revision)); return;
                }
                if (payload.GetProperty("worksheetId").ValueKind != JsonValueKind.String || !Guid.TryParseExact(payload.GetProperty("worksheetId").GetString(), "D", out var worksheetId)
                    ) { Error(id!, "INVALID_REQUEST", "Invalid worksheet identity."); return; }
                if (command == "workbook.createCopy")
                {
                    if (!payload.GetProperty("expectedRevision").TryGetInt32(out var revision) || revision < 0) { Error(id!, "INVALID_REQUEST", "Invalid revision."); return; }
                    Success(id!, await workbooks.CreateWorkingCopyAsync(sessionId, worksheetId, revision)); return;
                }
                if (command == "workbook.editCopy")
                {
                    if (!payload.GetProperty("row").TryGetInt32(out var editRow) || !payload.GetProperty("column").TryGetInt32(out var editColumn)
                        || !payload.GetProperty("expectedRevision").TryGetInt32(out var revision) || revision < 0
                        || payload.GetProperty("kind").ValueKind != JsonValueKind.String || payload.GetProperty("value").ValueKind != JsonValueKind.String)
                    { Error(id!, "INVALID_REQUEST", "Invalid cell edit."); return; }
                    Success(id!, await workbooks.EditWorkingCopyAsync(sessionId, new CellEdit(worksheetId, editRow, editColumn, payload.GetProperty("kind").GetString()!, payload.GetProperty("value").GetString()!, revision))); return;
                }
                if (!payload.GetProperty("startRow").TryGetInt32(out var row) || !payload.GetProperty("startColumn").TryGetInt32(out var column)
                    || !payload.GetProperty("rows").TryGetInt32(out var rows) || !payload.GetProperty("columns").TryGetInt32(out var columns))
                { Error(id!, "INVALID_REQUEST", "Invalid worksheet viewport payload."); return; }
                Success(id!, await workbooks.ReadAsync(sessionId, worksheetId, row, column, rows, columns)); return;
            }
            switch (command)
            {
                case "foundation.getInfo":
                    if (!HasOnly(payload)) { Error(id!, "INVALID_REQUEST", "Expected an empty payload."); return; }
                    Success(id!, new { applicationName = new ApplicationMetadataService().ApplicationName, version = "0.1.0", platform = "Windows x64", hasUnsavedWork = workbooks?.HasPendingChanges == true });
                    break;
                case "foundation.selectFile":
                    if (!HasOnly(payload)) { Error(id!, "INVALID_REQUEST", "Expected an empty payload."); return; }
                    lock (gate) { if (selecting) { Error(id!, "BUSY", "A file dialog is already open."); return; } selecting = true; }
                    try { Success(id!, await selectFile(lifetime.Token)); }
                    finally { lock (gate) selecting = false; }
                    break;
                case "diagnostics.cancel":
                    if (!HasOnly(payload, "operationId") || payload.GetProperty("operationId").ValueKind != JsonValueKind.String || !ValidId(payload.GetProperty("operationId").GetString()))
                    { Error(id!, "INVALID_REQUEST", "Invalid operation ID."); return; }
                    lock (gate)
                    {
                        var active = operationId == payload.GetProperty("operationId").GetString() && operation != null;
                        if (active) operation!.Cancel();
                        Success(id!, new { status = active ? "requested" : "notRunning" });
                    }
                    break;
                case "diagnostics.run":
                    if (!HasOnly(payload, "steps", "delayMs") || !payload.GetProperty("steps").TryGetInt32(out var steps) || steps is < 1 or > 100
                        || !payload.GetProperty("delayMs").TryGetInt32(out var ms) || ms is < 10 or > 200 || steps * ms > 20000)
                    { Error(id!, "INVALID_REQUEST", "Diagnostics allow 1–100 steps, 10–200 ms per step, up to 20 seconds."); return; }
                    CancellationTokenSource cts;
                    lock (gate)
                    {
                        if (operation != null) { Error(id!, "BUSY", "A diagnostic is already running."); return; }
                        cts = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); operation = cts; operationId = id;
                    }
                    try
                    {
                        for (var step = 1; step <= steps; step++)
                        {
                            await delay(ms, cts.Token);
                            cts.Token.ThrowIfCancellationRequested();
                            Emit(new { protocolVersion = 1, type = "event", name = "diagnostics.progress", operationId = id, progress = new { completed = step, total = steps } });
                        }
                        Success(id!, new { status = "completed" });
                    }
                    catch (OperationCanceledException) { Success(id!, new { status = "cancelled" }); }
                    finally { lock (gate) { operation = null; operationId = null; cts.Dispose(); } }
                    break;
            }
        }
        catch (WorkbookException ex) { if (ValidId(id)) Error(id!, ex.Code, ex.Message); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { if (ValidId(id)) Error(id!, "INVALID_REQUEST", "Malformed request."); }
        catch (OperationCanceledException) { if (ValidId(id)) Error(id!, "CANCELLED", "Request cancelled."); }
        catch (Exception) { if (ValidId(id)) Error(id!, "OPERATION_FAILED", "The operation could not be completed."); }
    }

    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); seen.Clear(); }
        workbooks?.Dispose();
        // Active handlers own their linked tokens; cancellation prevents any further sends.
    }
}
