using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Foundation;
using TsekSheet.Application.Documents;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ExcelReceiptBridgeTests
{
    [TestMethod]
    public async Task BridgeUsesLiveVerificationAndServerPreviewTokensWithoutSendingNativePaths()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllBytes(path, Encoding.ASCII.GetBytes("%PDF-1.7\n" + new string('x', 100)));
        try
        {
            var host = new FakeExcel(); var messages = new List<string>(); var n = 0;
            using var bridge = new BridgeSession(messages.Add, _ => Task.FromResult(new FileSelection("cancelled")),
                selectDocument: _ => Task.FromResult<string?>(path), excel: new(host, host), selectExcelWorkbook: _ => Task.FromResult<string?>(@"C:\native-only\Synthetic.xlsx"));
            async Task<JsonElement> Send(string command, object payload, bool ok = true)
            {
                await bridge.HandleAsync(BridgePolicy.Origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = "excel-" + n++, command, payload }, BridgeSession.JsonOptions));
                var response = JsonDocument.Parse(messages[^1]).RootElement.Clone(); Assert.AreEqual(ok, response.GetProperty("ok").GetBoolean(), response.ToString());
                Assert.IsFalse(messages[^1].Contains("native-only"));
                return response.GetProperty(ok ? "result" : "error");
            }
            await Send("excel.listWorkbooks", new { open = true }, false); Assert.AreEqual(0, host.Opened);
            await Send("foundation.handshake", new { supportedVersions = new[] { 1 } });
            await Send("excel.listWorkbooks", new { open = true, path = "untrusted" }, false); Assert.AreEqual(0, host.Opened);
            await Send("receipt.select", new { });
            var state = (await Send("receipt.start", new { profileId = "synthetic-payables", pageNumber = 1 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            var opened = await Send("excel.listWorkbooks", new { open = true }); Assert.AreEqual(1, host.Opened);
            Assert.AreEqual(host.Id, opened.GetProperty("openedWorkbookId").GetString());
            Assert.AreNotEqual(host.Id, opened.GetProperty("workbooks")[0].GetProperty("id").GetString(), "Identity must come from Excel open, not list order.");
            var refreshed = await Send("excel.listWorkbooks", new { open = false });
            Assert.AreEqual(JsonValueKind.Null, refreshed.GetProperty("openedWorkbookId").ValueKind);
            await Send("excel.previewInsert", new { expected = state.Version, workbookId = host.Id, sectionId = "columns" }, false); Assert.AreEqual(0, host.Previews);
            foreach (var item in new[] { ("vendor", "=literal vendor"), ("amount", "123.50"), ("rfp", "RFP-verified-1") })
            {
                state = (await Send("receipt.correct", new { expected = state.Version, key = item.Item1, valueRevision = 0, value = item.Item2 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
                state = (await Send("receipt.verify", new { expected = state.Version, key = item.Item1, valueRevision = 1 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            }
            var preview = await Send("excel.previewInsert", new { expected = state.Version, workbookId = host.Id, sectionId = "columns", sheet = "Columns" });
            Assert.AreEqual(1, host.Previews);
            Assert.IsFalse(preview.ToString().Contains("C:\\"), "Unparseable native formulas must not expose external file paths.");
            var oldVersion = state.Version;
            state = (await Send("receipt.retract", new { expected = state.Version, key = "vendor", valueRevision = 1 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            await Send("excel.insert", new { expected = oldVersion, previewToken = preview.GetProperty("token").GetString() }, false);
            Assert.AreEqual(0, host.Inserts, "Retracted verification must invalidate an already rendered preview.");
            state = (await Send("receipt.verify", new { expected = state.Version, key = "vendor", valueRevision = 1 })).Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            preview = await Send("excel.previewInsert", new { expected = state.Version, workbookId = host.Id, sectionId = "columns" });
            await Send("excel.insert", new { expected = state.Version, previewToken = "forged-token" }, false); Assert.AreEqual(0, host.Inserts);
            await Send("excel.insert", new { expected = state.Version, previewToken = preview.GetProperty("token").GetString() }); Assert.AreEqual(1, host.Inserts);
            await Send("excel.insert", new { expected = state.Version, previewToken = preview.GetProperty("token").GetString() }, false); Assert.AreEqual(1, host.Inserts);
            await Send("excel.previewInsert", new { expected = state.Version, workbookId = host.Id, sectionId = "columns" }, false);
        }
        finally { File.Delete(path); }
    }

    private sealed class FakeExcel : IExcelHost, ISectionWriter
    {
        public string Id { get; } = new('a', 32);
        public int Opened, Previews, Inserts;
        public Task<ExcelHostInfo> StartAsync() => Task.FromResult(new ExcelHostInfo(1, "synthetic"));
        public Task<IReadOnlyList<ExcelWorkbook>> ListWorkbooksAsync() => Task.FromResult<IReadOnlyList<ExcelWorkbook>>([new(new string('d', 32), "Other.xlsx", @"C:\native-only\Other.xlsx", ["Columns"]), new(Id, "Synthetic.xlsx", @"C:\native-only\Synthetic.xlsx", ["Columns"])]);
        public Task<ExcelWorkbook> OpenAsync(string path) { Opened++; return Task.FromResult(new ExcelWorkbook(Id, "Synthetic.xlsx", path, ["Columns"])); }
        public Task ActivateAsync(string workbookId, string sheet, CellRange range) => Task.CompletedTask;
        public Task CloseAsync(string workbookId, bool discardChanges = false) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        public Task<SectionInsertPreview> PreviewInsertAsync(string workbookId, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values)
        {
            Previews++; Assert.AreEqual("=literal vendor", values.Single(v => v.Key == "vendor").Value);
            return Task.FromResult(new SectionInsertPreview(new string('b', 32), workbookId, section.Id, "Columns", "A5:G5", 5, values, [new("G6:G6", "='C:\\native-only\\[Other.xlsx]Sheet'!A1", "Review subtotal manually.")]));
        }
        public Task<SectionInsertResult> InsertAsync(string previewToken)
        {
            Inserts++; return Task.FromResult(new SectionInsertResult(Id, "columns", "Columns", "A5:G5", [], new("highlight", Id, "Columns", new(5, 1, 1, 7), [])));
        }
    }
}
