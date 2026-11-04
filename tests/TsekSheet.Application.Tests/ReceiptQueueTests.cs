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

    private sealed class FakeExcel : IExcelHost, IWorkbookReader, ISectionWriter
    {
        public string Id => new('a', 32);
        public bool Ambiguous, Duplicate, DuplicateInvoice, Legacy, UnknownOutcome, RejectInsert;
        public int Inserts, Reads, UsedRanges, Previews;
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
        public Task<SectionInsertPreview> PreviewInsertAsync(string id, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values)
        {
            Previews++; return Task.FromResult(new SectionInsertPreview(Guid.NewGuid().ToString("N"), id, section.Id, section.SheetName, "B29:AF29", 29, values, Ambiguous ? [new("J31:J31", "unsupported", "Review total")] : []));
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
