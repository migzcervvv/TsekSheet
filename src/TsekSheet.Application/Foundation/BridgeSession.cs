using System.Text;
using System.Text.Json;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Excel;

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
    private readonly Func<CancellationToken, Task<string?>>? selectDocument;
    private readonly ReceiptQueue receiptQueue;
    private DesktopReceiptReview receipts => receiptQueue.Active;
    private readonly Func<CancellationToken, Task<IReadOnlyList<string>>>? selectDocuments;
    private readonly IReadOnlyList<WorkflowProfile>? profiles;
    private readonly ILocalDocumentRenderer? renderer;
    private readonly ExcelReceiptInsertion? excel;
    private readonly Func<CancellationToken, Task<string?>>? selectExcelWorkbook;
    private CancellationTokenSource? renderOperation;
    private TaskCompletionSource? renderFinished;
    private readonly IProductOcr? ocr;
    public bool HasPendingWorkbookChanges => workbooks?.HasPendingChanges == true;
    public int ReviewDecisionCount => workbooks?.ReviewDecisionCount ?? 0;

    public BridgeSession(Action<string> send, Func<CancellationToken, Task<FileSelection>> selectFile,
        Func<int, CancellationToken, Task>? delay = null, WorkbookWorkspace? workbooks = null,
        Func<CancellationToken, Task<string?>>? selectDocument = null, ILocalDocumentRenderer? renderer = null,
        ExcelReceiptInsertion? excel = null, Func<CancellationToken, Task<string?>>? selectExcelWorkbook = null,
        IReadOnlyList<WorkflowProfile>? profiles = null, Func<CancellationToken, Task<IReadOnlyList<string>>>? selectDocuments = null, IProductOcr? ocr = null)
    {
        this.ocr = ocr; this.send = send;
        this.selectFile = selectFile;
        this.delay = delay ?? ((ms, token) => Task.Delay(ms, token));
        this.workbooks = workbooks;
        this.selectDocument = selectDocument;
        this.renderer = renderer;
        this.excel = excel;
        this.selectExcelWorkbook = selectExcelWorkbook;
        this.profiles = profiles;
        this.selectDocuments = selectDocuments;
        receiptQueue = new(profiles is null ? null : id => profiles.Single(p => p.Id == id));
    }

    private void Emit(object message)
    {
        lock (gate) if (!disposed) send(JsonSerializer.Serialize(message, JsonOptions));
    }
    private void Success(string id, object result) => Emit(new { protocolVersion = 1, type = "response", requestId = id, ok = true, result });
    private void Error(string id, string code, string message, IReadOnlyList<CompatibilityFinding>? details = null) => Emit(new { protocolVersion = 1, type = "response", requestId = id, ok = false, error = new { code, message, details } });
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
                Success(id!, new { selectedVersion = 1, applicationName = new ApplicationMetadataService().ApplicationName, version = "0.1.0", capabilities = new { sourceRendering = renderer != null, receipts = selectDocument != null, diagnostics = true, fileSelection = true, workbooks = workbooks != null, templates = false, ocr = ocr != null } });
                return;
            }
            if (command is not ("receipt.changeProfile" or "receipt.queue" or "receipt.enqueue" or "receipt.navigate" or "receipt.prepare" or "excel.previewDraft" or "excel.addRow" or "excel.listWorkbooks" or "excel.previewInsert" or "excel.insert" or "receipt.ocr" or "receipt.render" or "receipt.cancelRender" or "receipt.select" or "receipt.state" or "receipt.start" or "receipt.correct" or "receipt.verify" or "receipt.retract" or "receipt.close" or "receipt.preview" or "sectionReview.start" or "sectionReview.decide" or "sectionReview.retract" or "sectionReview.cancel" or "foundation.getInfo" or "foundation.selectFile" or "diagnostics.run" or "diagnostics.cancel" or "workbook.open" or "workbook.inspect" or "workbook.readViewport" or "workbook.close" or "workflow.profiles" or "workbook.planRecords"))
            { Error(id!, "UNSUPPORTED_COMMAND", "Command is unavailable."); return; }
            if (!ready) { Error(id!, "NOT_READY", "Handshake is required."); return; }
            if (command == "receipt.queue")
            {
                if (!HasOnly(payload)) throw new JsonException();
                Success(id!, receiptQueue.State); return;
            }
            if (command == "receipt.enqueue")
            {
                if (!HasOnly(payload) || selectDocuments == null) throw new JsonException();
                lock (gate) { if (selecting) throw new InvalidOperationException(); selecting = true; }
                try
                {
                    var paths = await selectDocuments(lifetime.Token);
                    Success(id!, paths.Count == 0 ? receiptQueue.State : await Task.Run(() => receiptQueue.Add(paths, lifetime.Token)));
                }
                finally { lock (gate) selecting = false; }
                return;
            }
            if (command == "receipt.navigate")
            {
                if (!HasOnly(payload, "expectedId", "targetId", "skip")) throw new JsonException();
                lock (gate) renderOperation?.Cancel();
                Success(id!, receiptQueue.Navigate(payload.GetProperty("expectedId").GetString()!, payload.GetProperty("targetId").GetString()!, payload.GetProperty("skip").GetBoolean())); return;
            }
            if (command!.StartsWith("excel.", StringComparison.Ordinal))
            {
                if (excel is null) { Error(id!, "UNSUPPORTED_COMMAND", "Excel insertion is unavailable in this host."); return; }
                if (command == "excel.listWorkbooks")
                {
                    if (!HasOnly(payload, "open")) throw new JsonException();
                    string? path = null;
                    if (payload.GetProperty("open").GetBoolean())
                    {
                        if (selectExcelWorkbook is null) throw new InvalidOperationException();
                        lock (gate) { if (selecting) throw new InvalidOperationException(); selecting = true; }
                        try { path = await selectExcelWorkbook(lifetime.Token); }
                        finally { lock (gate) selecting = false; }
                    }
                    var profile = receipts.State.Version?.ProfileId;
                    var openedWorkbookId = path is null ? null : await excel.OpenAsync(path);
                    Success(id!, new { openedWorkbookId, workbooks = await excel.ListAsync(profileId: profile), sections = profile is null ? [] : excel.Sections(profile),
                        changes = excel.Changes.Select(c => new { c.Workbook, c.Sheet, c.Range, manualReview = c.ManualReview.Select(w => new { w.Cell, w.Reason }) }) });
                    return;
                }
                if (!(command is "excel.previewInsert" or "excel.previewDraft" ? HasOnly(payload, "expected", "workbookId", "sectionId") || HasOnly(payload, "expected", "workbookId", "sectionId", "sheet") : command == "excel.addRow" ? HasOnly(payload, "expected", "previewToken", "queueId") : HasOnly(payload, "expected", "previewToken"))
                    || !HasOnly(payload.GetProperty("expected"), "reviewId", "revision", "profileId", "profileHash")) throw new JsonException();
                var expected = JsonSerializer.Deserialize<ReviewVersion>(payload.GetProperty("expected"), JsonOptions)!;
                receiptQueue.RequireEditable();
                if (command is "excel.previewInsert" or "excel.previewDraft")
                {
                    var preview = await excel.PreviewAsync(receipts, expected, payload.GetProperty("workbookId").GetString()!, payload.GetProperty("sectionId").GetString()!, lifetime.Token,
                        payload.TryGetProperty("sheet", out var sheetValue) ? sheetValue.GetString() : null, draft: command == "excel.previewDraft");
                    Success(id!, new { preview.Token, preview.WorkbookId, preview.SectionId, preview.Sheet, preview.Range, preview.InsertRow, preview.Values, preview.AdvisoryWarnings,
                        manualReview = preview.ManualReview.Select(w => new { w.Cell, w.Reason }) });
                }
                else
                {
                    var queueId = command == "excel.addRow" ? payload.GetProperty("queueId").GetString() : null;
                    if (command == "excel.addRow" && (queueId is not { Length: 32 } || !queueId.All(char.IsAsciiHexDigit))) throw new JsonException();
                    if (queueId != null && receiptQueue.State.ActiveId != queueId) throw new FieldReviewException("STALE_QUEUE", "The active receipt changed. Request a fresh preview.");
                    var result = await excel.InsertAsync(receipts, expected, payload.GetProperty("previewToken").GetString()!, lifetime.Token, confirm: command == "excel.addRow");
                    if (queueId != null)
                    {
                        Success(id!, new { change = new { result.Workbook, result.Sheet, result.Range, manualReview = result.ManualReview.Select(w => new { w.Cell, w.Reason }) }, queue = receiptQueue.Complete(queueId) }); return;
                    }
                    Success(id!, new { result.Workbook, result.Sheet, result.Range, manualReview = result.ManualReview.Select(w => new { w.Cell, w.Reason }) });
                }
                return;
            }
            if (command!.StartsWith("receipt.", StringComparison.Ordinal))
            {
                if (selectDocument == null) { Error(id!, "UNSUPPORTED_COMMAND", "Native receipt selection is unavailable."); return; }
                if(command=="receipt.cancelRender")
                {
                    if(!HasOnly(payload))throw new JsonException();
                    Task? finished;lock(gate){renderOperation?.Cancel();finished=renderFinished?.Task;}
                    if(finished!=null)await finished;
                    Success(id!,new{status="requested"});return;
                }
                if(command is "receipt.render" or "receipt.ocr")
                {
                    if(renderer==null){Error(id!,"RENDER_UNAVAILABLE","Local rendering is unavailable in this host.");return;}
                    if(!(command=="receipt.ocr"?HasOnly(payload,"source","pageNumber","region"):HasOnly(payload,"source","pageNumber")) || !HasOnly(payload.GetProperty("source"),"documentId","revision","sha256"))throw new JsonException();
                    var expectedSource=JsonSerializer.Deserialize<LocalDocumentVersion>(payload.GetProperty("source"),JsonOptions)!;
                    var requestedPage=payload.GetProperty("pageNumber").GetInt32();
                    CancellationTokenSource render;
                    lock(gate)
                    {
                        if(renderOperation!=null){Error(id!,"RENDER_BUSY","Wait for the current render to finish or cancel it.");return;}
                        render=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);renderOperation=render;renderFinished=new(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                    try
                    {
                        var owner=receipts;
                        var before=owner.State;
                        if(command=="receipt.ocr")
                        {
                            receiptQueue.RequireEditable();
                            if(ocr==null)throw new InvalidOperationException("Configure local OCR assets before recognition.");
                            if(before.Version==null)throw new InvalidOperationException("Start receipt review first.");
                            var regionElement=payload.GetProperty("region");
                            if(regionElement.ValueKind!=JsonValueKind.Null && !HasOnly(regionElement,"x","y","width","height"))throw new JsonException();
                            OcrRegion? region=regionElement.ValueKind==JsonValueKind.Null?null:JsonSerializer.Deserialize<OcrRegion>(regionElement,JsonOptions);
                            region?.Validate();
                            var page=await owner.RenderAsync(ocr.Renderer,expectedSource,requestedPage,render.Token);
                            var proposals=await Task.Run(()=>ocr.RecognizeAsync(page.Page,region,render.Token),render.Token);
                            // Source and review must still match after the asynchronous worker finishes.
                            await owner.RenderAsync(new OcrIdentityRenderer(page.Page),expectedSource,requestedPage,render.Token);
                            if(!ReferenceEquals(owner,receipts)||owner.State.Version!=before.Version)throw new InvalidOperationException("Receipt changed during OCR. Run recognition again.");
                            Success(id!,proposals);
                        }
                        else Success(id!,await owner.RenderAsync(renderer,expectedSource,requestedPage,render.Token));
                    }
                    finally{lock(gate){if(ReferenceEquals(renderOperation,render)){renderOperation=null;renderFinished?.TrySetResult();renderFinished=null;}}render.Dispose();}
                    return;
                }
                if (command is "receipt.state" or "receipt.close" or "receipt.select")
                {
                    if (!HasOnly(payload)) throw new JsonException();
                    if (command != "receipt.state" && receiptQueue.State.Items.Count > 0) throw new FieldReviewException("QUEUE_ACTIVE", "Navigate the queue; queued reviews are kept until this session closes.");
                    if (command == "receipt.select")
                    {
                        lock (gate) { if (selecting) { Error(id!, "BUSY", "A file dialog is already open."); return; } selecting = true; }
                        try
                        {
                            if (receipts.State.Source != null) throw new FieldReviewException("RECEIPT_ALREADY_OPEN", "Close the current receipt before selecting another source.");
                            var path = await selectDocument(lifetime.Token);
                            var state = path == null ? receipts.State : await Task.Run(() => receipts.Register(path, lifetime.Token));
                            Success(id!, state);
                        }
                        finally { lock (gate) selecting = false; }
                        return;
                    }
                    if (command == "receipt.close") { lock(gate){renderOperation?.Cancel();} receipts.Close(); }
                    Success(id!, receipts.State); return;
                }
                if (command == "receipt.start")
                {
                    if (!HasOnly(payload, "profileId", "pageNumber")) throw new JsonException();
                    receiptQueue.RequireEditable();
                    Success(id!, await Task.Run(() => receipts.Start(payload.GetProperty("profileId").GetString()!, payload.GetProperty("pageNumber").GetInt32(), lifetime.Token))); return;
                }
                var names = command switch {
                    "receipt.changeProfile" => new[] { "expected", "profileId", "values" },
                    "receipt.prepare" => new[] { "expected", "values" },
                    "receipt.correct" => new[] { "expected", "key", "valueRevision", "value" },
                    "receipt.preview" => new[] { "expected", "destination", "sectionId", "confirmNewEntity" },
                    _ => new[] { "expected", "key", "valueRevision" }
                };
                if (!HasOnly(payload, names) || !HasOnly(payload.GetProperty("expected"), "reviewId", "revision", "profileId", "profileHash")) throw new JsonException();
                var expected = JsonSerializer.Deserialize<ReviewVersion>(payload.GetProperty("expected"), JsonOptions)!;
                if (command == "receipt.changeProfile")
                {
                    receiptQueue.RequireEditable();
                    var values = payload.GetProperty("values");
                    if (values.ValueKind != JsonValueKind.Object || values.EnumerateObject().Count() > 16 || values.EnumerateObject().Any(p => p.Value.ValueKind != JsonValueKind.String)) throw new JsonException();
                    Success(id!, await Task.Run(() => receipts.ChangeProfile(expected, payload.GetProperty("profileId").GetString()!, values.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!), lifetime.Token))); return;
                }
                if (command == "receipt.prepare")
                {
                    receiptQueue.RequireEditable();
                    var values = payload.GetProperty("values");
                    if (values.ValueKind != JsonValueKind.Object || values.EnumerateObject().Count() > 16 || values.EnumerateObject().Any(p => p.Value.ValueKind != JsonValueKind.String)) throw new JsonException();
                    Success(id!, await Task.Run(() => receipts.Prepare(expected, values.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!), lifetime.Token))); return;
                }
                if (command == "receipt.preview")
                {
                    if (workbooks == null || !HasOnly(payload.GetProperty("destination"), "sessionId", "worksheetId", "revision", "sourceSha256", "profileId", "profileHash")) throw new JsonException();
                    var destination = JsonSerializer.Deserialize<MappingDestination>(payload.GetProperty("destination"), JsonOptions)!;
                    var choices = new PlacementChoices(payload.GetProperty("sectionId").GetString(), payload.GetProperty("confirmNewEntity").GetBoolean());
                    Success(id!, await workbooks.PreviewReceiptAsync(receipts, expected, destination, choices)); return;
                }
                receiptQueue.RequireEditable();
                Success(id!, await Task.Run(() => receipts.Change(command[8..], expected, payload.GetProperty("key").GetString()!, payload.GetProperty("valueRevision").GetInt32(),
                    command == "receipt.correct" ? payload.GetProperty("value").GetString() : null, lifetime.Token))); return;
            }
            if (command!.StartsWith("sectionReview.", StringComparison.Ordinal))
            {
                if (workbooks == null) { Error(id!, "UNSUPPORTED_COMMAND", "Workbook host required."); return; }
                if (command is "sectionReview.start" or "sectionReview.cancel")
                {
                    var names = command == "sectionReview.start" ? new[] { "sessionId", "worksheetId", "expectedRevision" } : new[] { "sessionId", "worksheetId" };
                    if (!HasOnly(payload, names) || !Guid.TryParseExact(payload.GetProperty("sessionId").GetString(), "D", out var sid)
                        || !Guid.TryParseExact(payload.GetProperty("worksheetId").GetString(), "D", out var wid)) throw new JsonException();
                    if (command == "sectionReview.cancel") { Success(id!, new { status = workbooks.CancelSectionReview(sid, wid) }); return; }
                    if (!payload.GetProperty("expectedRevision").TryGetInt32(out var revision) || revision < 0) throw new JsonException();
                    var review = await workbooks.StartSectionReviewAsync(sid, wid, revision);
                    Success(id!, new { status = review == null ? "cancelled" : "started", review }); return;
                }
                if (!HasOnly(payload, command == "sectionReview.decide" ? ["binding", "decision"] : ["binding", "decisionId"])) throw new JsonException();
                var b = payload.GetProperty("binding");
                if (!HasOnly(b, "reviewId", "sessionId", "worksheetId", "worksheetOrder", "captureRevision", "sourceSha256", "profileVersion", "profileSha256", "reviewRevision")) throw new JsonException();
                if (command == "sectionReview.decide")
                {
                    if (!HasOnly(payload.GetProperty("decision"), "id", "kind", "row", "sectionId", "value", "evidenceRow", "evidenceColumn", "reason")) throw new JsonException();
                    Success(id!, await workbooks.DecideSectionAsync(JsonSerializer.Deserialize<SectionReviewRequest>(payload.GetRawText(), JsonOptions)!));
                }
                else Success(id!, await workbooks.RetractSectionAsync(JsonSerializer.Deserialize<SectionReviewRetraction>(payload.GetRawText(), JsonOptions)!));
                return;
            }
            if (command == "workflow.profiles")
            {
                if (!HasOnly(payload) || workbooks == null) { Error(id!, "INVALID_REQUEST", "Workflow catalog accepts no paths or arguments and requires a workbook host."); return; }
                Success(id!, new { profiles = profiles is null ? WorkflowProfiles.Catalog : profiles.Select(WorkflowProfiles.Summary).ToArray() }); return;
            }
            if (command!.StartsWith("workbook.", StringComparison.Ordinal))
            {
                if (workbooks == null) { Error(id!, "UNSUPPORTED_COMMAND", "Read-only workbooks are unavailable in this host."); return; }
                if (command is "workbook.open" or "workbook.inspect")
                {
                    if (!HasOnly(payload)) { Error(id!, "INVALID_REQUEST", "Workbook opening accepts no paths or other arguments."); return; }
                    Success(id!, await workbooks.OpenAsync(command == "workbook.inspect")); return;
                }
                var names = command switch
                {
                    "workbook.close" => new[] { "sessionId" },
                    "workbook.planRecords" => new[] { "sessionId", "worksheetId", "expectedRevision", "profileId", "profileHash", "records" },
                    _ => new[] { "sessionId", "worksheetId", "startRow", "startColumn", "rows", "columns" }
                };
                if (!HasOnly(payload, names) || payload.GetProperty("sessionId").ValueKind != JsonValueKind.String || !Guid.TryParseExact(payload.GetProperty("sessionId").GetString(), "D", out var sessionId))
                { Error(id!, "INVALID_REQUEST", "Invalid workbook session payload."); return; }
                if (command is "workbook.close") { Success(id!, new { status = await workbooks.CloseAsync(sessionId) }); return; }
                if (payload.GetProperty("worksheetId").ValueKind != JsonValueKind.String || !Guid.TryParseExact(payload.GetProperty("worksheetId").GetString(), "D", out var worksheetId)
                    ) { Error(id!, "INVALID_REQUEST", "Invalid worksheet identity."); return; }
                if (command == "workbook.planRecords")
                {
                    var request = WorkflowProfiles.ParseRequest(payload.GetRawText());
                    Success(id!, await workbooks.PlanRecordsAsync(request)); return;
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
        catch (ExcelInsertRejectedException ex) { if (ValidId(id)) Error(id!, "EXCEL_INSERT_REJECTED", ex.Message); }
        catch (ExcelBusyException ex) { if (ValidId(id)) Error(id!, "EXCEL_BUSY", ex.Message); }
        catch (PreservationException ex) { if (ValidId(id)) Error(id!, "EXCEL_PRESERVATION_FAILED", ex.Message); }
        catch (FieldReviewException ex) { if (ValidId(id)) Error(id!, ex.Code, ex.Message); }
        catch (DocumentSourceException ex) { if (ValidId(id)) Error(id!, ex.Code, ex.Message); }
        catch (WorkbookException ex) { if (ValidId(id)) Error(id!, ex.Code, ex.Message, ex.Details); }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        { if (ValidId(id)) Error(id!, "INVALID_REQUEST", "Malformed request."); }
        catch (OperationCanceledException) { if (ValidId(id)) Error(id!, "CANCELLED", "Request cancelled."); }
        catch (Exception) { if (ValidId(id)) Error(id!, "OPERATION_FAILED", "The operation could not be completed."); }
    }

    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; lifetime.Cancel(); lifetime.Dispose(); seen.Clear(); }
        receiptQueue.Dispose();
        if(ocr is IAsyncDisposable ownedOcr) _ = ownedOcr.DisposeAsync().AsTask();
        workbooks?.Dispose();
        // Active handlers own their linked tokens; cancellation prevents any further sends.
    }
}
