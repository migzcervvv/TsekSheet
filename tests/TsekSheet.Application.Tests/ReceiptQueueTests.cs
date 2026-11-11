using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Verification;
using TsekSheet.TestFixtures;
using System.Text.Json;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ReceiptQueueTests
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "TsekSheet-loop-" + Guid.NewGuid().ToString("N"));
    [TestInitialize] public void Initialize() => Directory.CreateDirectory(root);
    [TestCleanup] public void Cleanup() { foreach (var path in Directory.GetFiles(root)) File.Delete(path); Directory.Delete(root); }
    private string Source(int i) { var path = Path.Combine(root, $"receipt-{i}.pdf"); File.WriteAllText(path, "%PDF-1.7\n" + new string('x', 100) + i); return path; }
    private static Dictionary<string, string> Values(string item = "LOOP-1") => LiquidationProfile.Create().Fields.ToDictionary(f => f.Key,
        f => f.Key switch { "itemNo" => item, "particulars" => "=literal expense", "account" => "Q", "amount" => "123.50", _ => "" });

    [TestMethod]
    public void QueueRetainsDraftsSkipsAndReviewAndAdvancesOnlyAfterCompletion()
    {
        using var queue = new ReceiptQueue(_ => LiquidationProfile.Create());
        var first = queue.Add([Source(1), Source(2), Source(3)], default);
        var state = queue.Active.Start("synthetic-liquidation", 1, default);
        state = queue.Active.Prepare(state.Version!, Values(), default);
        Assert.IsTrue(state.Fields.Where(f => f.Value.Length > 0).All(f => f.Status == "Corrected"));
        var second = queue.Navigate(first.ActiveId!, first.Items[1].Id, true);
        Assert.AreEqual("Skipped", second.Items[0].Status);
        queue.Navigate(second.ActiveId!, first.ActiveId!, false);
        Assert.AreEqual("123.50", queue.Active.State.Fields.Single(f => f.Key == "amount").Value);
        Assert.AreEqual(state.Version, queue.Active.State.Version);
        var after = queue.Complete(first.ActiveId!);
        Assert.AreEqual(first.Items[1].Id, after.ActiveId);
        Assert.AreEqual("Inserted", after.Items[0].Status);
        queue.Navigate(after.ActiveId!, first.ActiveId!, false);
        Assert.ThrowsExactly<FieldReviewException>(queue.RequireEditable);
        Assert.ThrowsExactly<FieldReviewException>(() => queue.Navigate("stale", after.ActiveId!, false));
    }

    [TestMethod]
    public void DuplicateSourcesAreRejectedWithoutPartialEnqueue()
    {
        using var queue = new ReceiptQueue(_ => LiquidationProfile.Create());
        var source = Source(1);
        Assert.ThrowsExactly<FieldReviewException>(() => queue.Add([source, source], default));
        Assert.AreEqual(0, queue.State.Items.Count);
        queue.Add([source], default);
        Assert.ThrowsExactly<FieldReviewException>(() => queue.Add([Source(2), source], default));
        Assert.AreEqual(1, queue.State.Items.Count);
    }

    [TestMethod]
    public void ExplicitProfileChangeKeepsMatchingTypedDraftsAndInvalidatesPreviousVerification()
    {
        var profile = LiquidationProfile.Create();
        var other = profile with { Id = "other-liquidation", Layout = profile.Layout! with { Id = "other-liquidation" } };
        using var receipt = new DesktopReceiptReview(id => id == other.Id ? other : profile);
        receipt.Register(Source(1), default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, Values(), default); state = receipt.ConfirmAll(state.Version!, default);
        var values = Values(); values["particulars"] = "Unsaved matching draft";
        var changed = receipt.ChangeProfile(state.Version!, other.Id, values, default);
        Assert.AreEqual(other.Id, changed.Version!.ProfileId);
        Assert.AreEqual("Unsaved matching draft", changed.Fields.Single(f => f.Key == "particulars").Value);
        Assert.IsTrue(changed.Fields.All(f => f.Status == "Draft"));
        Assert.ThrowsExactly<FieldReviewException>(() => receipt.ConfirmAll(state.Version!, default));
    }

    [TestMethod]
    public async Task BridgeBindsConfirmationToTheActiveQueueReceiptAndNeverAcceptsNativePaths()
    {
        var profile = LiquidationProfile.Create(); var excel = new FakeExcel(); var messages = new List<string>(); var n = 0;
        using var bridge = new BridgeSession(messages.Add, _ => Task.FromResult(new FileSelection("cancelled")),
            selectDocument: _ => Task.FromResult<string?>(null), selectDocuments: _ => Task.FromResult<IReadOnlyList<string>>([Source(1), Source(2)]),
            profiles: [profile], excel: new(excel, excel, _ => profile));
        async Task<JsonElement> Send(string command, object payload, bool ok = true)
        {
            await bridge.HandleAsync(BridgePolicy.Origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = "loop-" + n++, command, payload }, BridgeSession.JsonOptions));
            var result = JsonDocument.Parse(messages[^1]).RootElement.Clone(); Assert.AreEqual(ok, result.GetProperty("ok").GetBoolean(), result.ToString());
            Assert.IsFalse(messages[^1].Contains(root.Replace("\\", "\\\\")), "Native source paths must not cross the bridge.");
            return result.GetProperty(ok ? "result" : "error");
        }
        await Send("receipt.enqueue", new { }, false);
        await Send("foundation.handshake", new { supportedVersions = new[] { 1 } });
        await Send("receipt.enqueue", new { paths = new[] { "untrusted.pdf" } }, false);
        var q = await Send("receipt.enqueue", new { }); var queueId = q.GetProperty("activeId").GetString();
        var state = (await Send("receipt.start", new { profileId = profile.Id, pageNumber = 1 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
        state = (await Send("receipt.prepare", new { expected = state.Version, values = Values() })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
        var preview = await Send("excel.previewDraft", new { expected = state.Version, workbookId = excel.Id, sectionId = "expenses", sheet = "Person 1" });
        await Send("excel.addRow", new { expected = state.Version, previewToken = preview.GetProperty("token").GetString(), queueId = new string('f', 32) }, false);
        Assert.AreEqual(0, excel.Inserts);
        q = (await Send("excel.addRow", new { expected = state.Version, previewToken = preview.GetProperty("token").GetString(), queueId })).GetProperty("queue");
        Assert.AreNotEqual(queueId, q.GetProperty("activeId").GetString());
        Assert.AreEqual("Inserted", q.GetProperty("items")[0].GetProperty("status").GetString());
        await Send("excel.addRow", new { expected = state.Version, previewToken = preview.GetProperty("token").GetString(), queueId }, false);
        Assert.AreEqual(1, excel.Inserts);
    }

    [TestMethod]
    public async Task DraftPreviewDoesNotVerifyAndExplicitConfirmationCannotInsertTheReceiptTwice()
    {
        var profile = LiquidationProfile.Create(); using var receipt = new DesktopReceiptReview(_ => profile);
        receipt.Register(Source(1), default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, Values(), default);
        var excel = new FakeExcel(); var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        Assert.AreEqual("Corrected", receipt.State.Fields.Single(f => f.Key == "amount").Status);
        Assert.AreEqual(123.50, preview.Values.Single(v => v.Key == "amount").Value);
        Assert.AreEqual(17, preview.Values.Single(v => v.Key == "amount").Column);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default));
        Assert.AreEqual(0, excel.Inserts);
        preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        await insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true);
        Assert.AreEqual(1, excel.Inserts);
        state = receipt.State;
        Assert.AreEqual("Verified", state.Fields.Single(f => f.Key == "amount").Status);
        var changed = receipt.Prepare(state.Version!, Values("LOOP-2"), default);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(receipt, changed.Version!, excel.Id, "expenses", default, "Person 1", draft: true));
    }

    [TestMethod]
    public async Task MissingAmbiguousAndChangedSourceAreRejectedBeforeWriting()
    {
        var profile = LiquidationProfile.Create(); using var receipt = new DesktopReceiptReview(_ => profile);
        var path = Source(1); receipt.Register(path, default); var state = receipt.Start(profile.Id, 1, default);
        var values = Values(); values["amount"] = "";
        Assert.ThrowsExactly<TsekSheet.Application.Workbooks.WorkbookException>(() => receipt.Prepare(state.Version!, values, default));
        values["amount"] = "12 or 123.50";
        Assert.ThrowsExactly<TsekSheet.Application.Workbooks.WorkbookException>(() => receipt.Prepare(state.Version!, values, default));
        Assert.AreEqual(state.Version, receipt.State.Version, "Invalid drafts must not partially change fields.");
        state = receipt.Prepare(state.Version!, Values(), default);
        var excel = new FakeExcel { Ambiguous = true }; var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var error = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true));
        Assert.AreEqual("AMBIGUOUS_TOTAL", error.Code);
        excel.Ambiguous = false;
        var preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        File.AppendAllText(path, "changed");
        await Assert.ThrowsExactlyAsync<DocumentSourceException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true));
        Assert.AreEqual(0, excel.Inserts);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public async Task LayoutDoesNotInferUniquenessFromItemNumberOrVendorInvoiceAndDoesNotReadTwice(bool invoice)
    {
        var profile = LiquidationProfile.Create(); using var receipt = new DesktopReceiptReview(_ => profile);
        receipt.Register(Source(1), default); var state = receipt.Start(profile.Id, 1, default);
        var values = Values(); if (invoice) { values["invoice"] = "INV-1"; values["vendor"] = "Synthetic vendor"; }
        state = receipt.Prepare(state.Version!, values, default);
        var excel = new FakeExcel { Duplicate = true, DuplicateInvoice = invoice };
        var timing = new List<PreviewStageTiming>();
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile) { PreviewTiming = timing.Add };
        var preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        Assert.AreEqual("LOOP-1", preview.Values.Single(v => v.Key == "itemNo").Value);
        Assert.AreEqual(0, excel.Reads); Assert.AreEqual(0, excel.UsedRanges);
        Assert.AreEqual(1, excel.Previews, "The writer's safety preview must still run.");
        Assert.IsTrue(timing.Any(t => t.Stage == "duplicate.notConfigured"));
        Assert.IsTrue(timing.Any(t => t.Stage == "application.total"));
        Assert.AreEqual("Corrected", receipt.State.Fields.Single(f => f.Key == "amount").Status);
    }

    [TestMethod, DataRow("Existing vendor", true), DataRow("Different vendor", false)]
    public async Task ExplicitEntityAndIdentifierPolicyRejectsOnlyTheMatchingEntity(string vendor, bool duplicate)
    {
        var profile = WorkflowProfiles.BuiltInProfiles.Single(p => p.Id == "synthetic-payables");
        using var receipt = new DesktopReceiptReview(_ => profile);
        receipt.Register(Source(1), default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, profile.Fields.ToDictionary(f => f.Key, f => f.Key switch { "vendor" => vendor, "rfp" => "SHARED-1", "amount" => "123.50", _ => "" }), default);
        var excel = new FakeExcel { Legacy = true };
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        if (duplicate)
        {
            var error = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(receipt, state.Version!, excel.Id, "columns", default, "Columns", draft: true));
            Assert.AreEqual("DUPLICATE_RECORD", error.Code);
        }
        else await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "columns", default, "Columns", draft: true);
        Assert.AreEqual(1, excel.Reads); Assert.AreEqual(1, excel.UsedRanges);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public async Task SubmittedSourceCannotBeResubmittedByRestartingItsProfileOrReview(bool unknownOutcome)
    {
        var profile = LiquidationProfile.Create();
        var other = profile with { Id = "other-liquidation", Layout = profile.Layout! with { Id = "other-liquidation" } };
        WorkflowProfile Profile(string id) => id == other.Id ? other : profile;
        using var receipt = new DesktopReceiptReview(Profile);
        var path = Source(1); receipt.Register(path, default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, Values(), default);
        var excel = new FakeExcel { UnknownOutcome = unknownOutcome }; var insertion = new ExcelReceiptInsertion(excel, excel, Profile);
        var preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        if (unknownOutcome) await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true));
        else await insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true);
        state = receipt.ChangeProfile(receipt.State.Version!, other.Id, Values(), default);
        state = receipt.Prepare(state.Version!, Values(), default);
        var error = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true));
        Assert.AreEqual("ALREADY_INSERTED", error.Code);
        using var replacement = new DesktopReceiptReview(Profile); replacement.Register(path, default);
        var next = replacement.Start(profile.Id, 1, default); next = replacement.Prepare(next.Version!, Values(), default);
        error = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(replacement, next.Version!, excel.Id, "expenses", default, "Person 1", draft: true));
        Assert.AreEqual("ALREADY_INSERTED", error.Code);
        Assert.AreEqual(1, excel.Inserts);
    }

    [TestMethod]
    public async Task KnownPrewriteRejectionAllowsFreshPreviewOfTheSameSource()
    {
        var profile = LiquidationProfile.Create(); using var receipt = new DesktopReceiptReview(_ => profile);
        receipt.Register(Source(1), default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, Values(), default);
        var excel = new FakeExcel { RejectInsert = true }; var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        var error = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true));
        Assert.AreEqual("STALE_INSERT_PREVIEW", error.Code);
        state = receipt.State; excel.RejectInsert = false;
        preview = await insertion.PreviewAsync(receipt, state.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        await insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true);
        Assert.AreEqual(2, excel.Inserts);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public async Task HeldCheckAllowsRenderingAndObservationsButGuardsEditsAndNavigation(bool editAfterCheck)
    {
        var profile = LiquidationProfile.Create();
        using var queue = new ReceiptQueue(_ => profile);
        var initial = queue.Add([Source(1), Source(2)], default);
        var receipt = queue.Active;
        var started = receipt.Start(profile.Id, 1, default);
        var draft = receipt.Prepare(started.Version!, Values(), default);
        var barrier = new PreviewBarrier();
        var excel = new FakeExcel { BeforePreview = barrier.Hold };
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var check = insertion.PreviewAsync(receipt, draft.Version!, excel.Id, "expenses", default, "Person 1", draft: true, queue: queue);
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            var source = draft.Source!.Document.Version;
            var render = await receipt.RenderAsync(new TestRenderer(), source, 2, default);
            Assert.AreEqual(source, render.Source);
            Assert.AreEqual(2, render.Page.PageNumber);
            var wrongSource = source with { Revision = source.Revision + 1 };
            await Assert.ThrowsExactlyAsync<DocumentSourceException>(() => receipt.RenderAsync(new TestRenderer(), wrongSource, 1, default));
            Assert.AreEqual(draft.Version, queue.State.Receipt.Version);
            CollectionAssert.AreEqual(draft.Fields.ToArray(), receipt.State.Fields.ToArray());
            var stale = Assert.ThrowsExactly<FieldReviewException>(() => receipt.Prepare(started.Version!, Values("STALE"), default));
            Assert.AreEqual("STALE_RECORD_REVIEW", stale.Code);
            var locked = Assert.ThrowsExactly<FieldReviewException>(() => receipt.Prepare(draft.Version!, Values("BLOCKED"), default));
            Assert.AreEqual("RECEIPT_REVIEW_LOCKED", locked.Code);
            locked = Assert.ThrowsExactly<FieldReviewException>(() => queue.Navigate(initial.ActiveId!, initial.Items[1].Id, false));
            Assert.AreEqual("RECEIPT_REVIEW_LOCKED", locked.Code);
            Assert.ThrowsExactly<FieldReviewException>(receipt.Close);
        }
        finally { barrier.Release.TrySetResult(); }

        var preview = await check;
        Assert.AreEqual("LOOP-1", preview.Values.Single(v => v.Key == "itemNo").Value);
        if (editAfterCheck)
        {
            var changed = receipt.Prepare(draft.Version!, Values("AFTER-CHECK"), default);
            Assert.AreNotEqual(draft.Version, changed.Version);
            await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, draft.Version!, preview.Token, default, confirm: true));
        }
        else await insertion.InsertAsync(receipt, draft.Version!, preview.Token, default, confirm: true);
        Assert.AreEqual(editAfterCheck ? 0 : 1, excel.Inserts);
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public async Task HeldCheckRejectsSourceChangesAndSessionDisposal(bool dispose)
    {
        var profile = LiquidationProfile.Create();
        using var receipt = new DesktopReceiptReview(_ => profile);
        var path = Source(1);
        receipt.Register(path, default);
        var started = receipt.Start(profile.Id, 1, default);
        var draft = receipt.Prepare(started.Version!, Values(), default);
        var barrier = new PreviewBarrier();
        var excel = new FakeExcel { BeforePreview = barrier.Hold };
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var check = insertion.PreviewAsync(receipt, draft.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        try
        {
            await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (dispose) receipt.Dispose();
            else File.AppendAllText(path, "changed during Check");
        }
        finally { barrier.Release.TrySetResult(); }
        if (dispose)
        {
            var closed = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => check);
            Assert.AreEqual("SOURCE_SESSION_CLOSED", closed.Code);
        }
        else await Assert.ThrowsExactlyAsync<DocumentSourceException>(() => check);
        Assert.AreEqual(0, excel.Inserts);
    }

    [TestMethod]
    public async Task ConsecutiveChecksSerializeAndOnlyNewestTokenCanBeConfirmed()
    {
        var profile = LiquidationProfile.Create();
        using var receipt = new DesktopReceiptReview(_ => profile);
        var draft = PrepareReceipt(receipt, profile, 1);
        var barrier = new PreviewBarrier();
        var excel = new FakeExcel { BeforePreview = barrier.Hold };
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        Task<SectionInsertPreview> Check() => insertion.PreviewAsync(receipt, draft.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        var first = Check();
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var second = Check();
        try
        {
            Assert.IsFalse(second.IsCompleted);
            Assert.AreEqual(1, excel.Previews);
        }
        finally { barrier.Release.TrySetResult(); }
        var older = await first;
        var latest = await second;
        Assert.AreEqual(2, excel.Previews);
        Assert.AreNotEqual(older.Token, latest.Token);
        var stale = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, draft.Version!, older.Token, default, confirm: true));
        Assert.AreEqual("STALE_INSERT_PREVIEW", stale.Code);
        Assert.AreEqual(0, excel.Inserts);
        await insertion.InsertAsync(receipt, draft.Version!, latest.Token, default, confirm: true);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, draft.Version!, latest.Token, default, confirm: true));
        Assert.AreEqual(1, excel.Inserts);
    }

    [TestMethod, DataRow("unchanged"), DataRow("edit"), DataRow("source"), DataRow("navigate")]
    public async Task InsertWaitsBehindAnotherCheckAndRevalidatesReceipt(string change)
    {
        var profile = LiquidationProfile.Create();
        using var queue = new ReceiptQueue(_ => profile);
        using var other = new DesktopReceiptReview(_ => profile);
        var path = Source(1);
        var initial = queue.Add([path, Source(2)], default);
        var target = queue.Active;
        var started = target.Start(profile.Id, 1, default);
        var draft = target.Prepare(started.Version!, Values(), default);
        var otherDraft = PrepareReceipt(other, profile, 3);
        var excel = new FakeExcel();
        var insertion = new ExcelReceiptInsertion(excel, excel, _ => profile);
        var preview = await insertion.PreviewAsync(target, draft.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        var barrier = new PreviewBarrier();
        excel.BeforePreview = barrier.Hold;
        var check = insertion.PreviewAsync(other, otherDraft.Version!, excel.Id, "expenses", default, "Person 1", draft: true);
        await barrier.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var write = insertion.InsertFromQueueAsync(target, draft.Version!, preview.Token, default, queue, initial.ActiveId);
        try
        {
            Assert.IsFalse(write.IsCompleted);
            Assert.AreEqual(0, excel.Inserts);
            if (change == "edit") target.Prepare(draft.Version!, Values("NEW-DRAFT"), default);
            if (change == "source") File.AppendAllText(path, "changed");
            if (change == "navigate") queue.Navigate(initial.ActiveId!, initial.Items[1].Id, false);
        }
        finally { barrier.Release.TrySetResult(); }
        await check;
        if (change is "edit" or "navigate")
        {
            var stale = await Assert.ThrowsExactlyAsync<FieldReviewException>(() => write);
            Assert.AreEqual(change == "edit" ? "STALE_RECORD_REVIEW" : "STALE_QUEUE", stale.Code);
        }
        else if (change == "source") await Assert.ThrowsExactlyAsync<DocumentSourceException>(() => write);
        else Assert.AreEqual("Inserted", (await write).Queue!.Items[0].Status);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertFromQueueAsync(target, draft.Version!, preview.Token, default, queue, initial.ActiveId));
        Assert.AreEqual(change == "unchanged" ? 1 : 0, excel.Inserts);
    }

    private ReceiptState PrepareReceipt(DesktopReceiptReview receipt, WorkflowProfile profile, int source)
    {
        receipt.Register(Source(source), default);
        var state = receipt.Start(profile.Id, 1, default);
        return receipt.Prepare(state.Version!, Values(), default);
    }

    private sealed class PreviewBarrier
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task Hold() { Entered.TrySetResult(); return Release.Task; }
    }

    private sealed class TestRenderer : ILocalDocumentRenderer
    {
        public Task<RenderedDocumentPage> RenderAsync(byte[] bytes, LocalDocumentKind kind, int page, CancellationToken token)
        {
            Assert.IsTrue(bytes.AsSpan().StartsWith("%PDF-"u8));
            return Task.FromResult(new RenderedDocumentPage(page, 2, 10, 10, "synthetic"));
        }
    }

    private sealed class FakeExcel : IExcelHost, IWorkbookReader, ISectionWriter
    {
        public string Id => new('a', 32);
        public bool Ambiguous, Duplicate, DuplicateInvoice, Legacy, UnknownOutcome, RejectInsert;
        public int Inserts, Reads, UsedRanges, Previews;
        public Func<Task>? BeforePreview;
        public Task<ExcelHostInfo> StartAsync() => Task.FromResult(new ExcelHostInfo(1, "synthetic"));
        public Task<IReadOnlyList<ExcelWorkbook>> ListWorkbooksAsync() => Task.FromResult<IReadOnlyList<ExcelWorkbook>>([new(Id, "Synthetic.xlsx", "native-only", ["Person 1"])]);
        public Task<ExcelWorkbook> OpenAsync(string path) => throw new NotSupportedException();
        public Task ActivateAsync(string id, string sheet, CellRange range) => Task.CompletedTask;
        public Task CloseAsync(string id, bool discardChanges = false) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<CellRange> UsedRangeAsync(string id, string sheet) { UsedRanges++; return Task.FromResult(new CellRange(1, 1, 43, 32)); }
        public Task<PreservationSnapshot> CaptureAsync(string id) => throw new NotSupportedException();
        public Task<RangeData> ReadAsync(string id, string sheet, CellRange range)
        {
            Reads++;
            var values = new object?[43, 32];
            foreach (var anchor in LiquidationProfile.Create().Layout!.SheetMatch.Anchors) values[1, A1Formula.ColumnNumber(anchor.Column)-1] = anchor.Text;
            values[30, 2] = "TOTAL"; values[36, 2] = "TOTAL credit"; values[39, 2] = "TOTAL";
            if (Duplicate) values[3, 1] = "LOOP-1";
            if (DuplicateInvoice) { values[3, 3] = "Synthetic vendor"; values[3, 6] = "INV-1"; }
            if (Legacy) { values[3, 0] = "Existing vendor"; values[3, 2] = "SHARED-1"; values[5, 0] = "Subtotal"; }
            return Task.FromResult(new RangeData(range, values, new string?[43, 32], new string[43, 32]));
        }
        public async Task<SectionInsertPreview> PreviewInsertAsync(string id, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values)
        {
            Previews++;
            if (BeforePreview is not null) await BeforePreview();
            return new SectionInsertPreview(Guid.NewGuid().ToString("N"), id, section.Id, section.SheetName, "B29:AF29", 29, values, Ambiguous ? [new("J31:J31", "unsupported", "Review total")] : []);
        }
        public Task<SectionInsertResult> InsertAsync(string token)
        {
            Inserts++;
            if (RejectInsert) throw new ExcelInsertRejectedException("Target changed before writing.");
            if (UnknownOutcome) throw new InvalidOperationException("Unknown submission outcome.");
            return Task.FromResult(new SectionInsertResult(Id, "expenses", "Person 1", "B29:AF29", [], new("highlight", Id, "Person 1", new(29, 2, 1, 31), [])));
        }
    }
}
