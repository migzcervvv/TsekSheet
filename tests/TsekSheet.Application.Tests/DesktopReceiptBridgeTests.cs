using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Foundation;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class DesktopReceiptBridgeTests
{
    [TestMethod]
    [DataRow("synthetic-payables", ".pdf", 3)]
    [DataRow("synthetic-laboratory", ".png", 1)]
    public async Task NativeSelectionManualCorrectionExplicitVerificationStaleRejectionAndCleanup(string profile, string extension, int page)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + extension);
        var bytes = new byte[128];
        (extension == ".pdf" ? Encoding.ASCII.GetBytes("%PDF-1.7\n") : new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }).CopyTo(bytes, 0);
        File.WriteAllBytes(path, bytes);
        try
        {
            var messages = new List<string>(); var calls = 0;
            using var bridge = new BridgeSession(messages.Add, _ => Task.FromResult(new FileSelection("cancelled")), selectDocument: _ => { calls++; return Task.FromResult<string?>(path); });
            var next = 0;
            async Task<JsonElement> Send(string command, object payload, bool ok = true)
            {
                await bridge.HandleAsync(BridgePolicy.Origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = "receipt-" + next++, command, payload }, BridgeSession.JsonOptions));
                var response = JsonDocument.Parse(messages[^1]).RootElement.Clone();
                Assert.AreEqual(ok, response.GetProperty("ok").GetBoolean(), response.ToString());
                return response.GetProperty(ok ? "result" : "error");
            }
            ReceiptState State(JsonElement result) => result.Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            await Send("receipt.select", new { }, false); Assert.AreEqual(0, calls);
            await Send("foundation.handshake", new { supportedVersions = new[] { 1 } });
            await Send("receipt.select", new { path }, false); Assert.AreEqual(0, calls);
            var state = State(await Send("receipt.select", new { })); Assert.AreEqual(1, calls);
            Assert.IsNotNull(state.Source); Assert.IsFalse(messages[^1].Contains(path));
            await Send("receipt.select", new { }, false); Assert.AreEqual(1, calls);
            state = State(await Send("receipt.start", new { profileId = profile, pageNumber = page }));
            Assert.IsTrue(state.Fields.All(f => f.Status == "Draft" && f.Value.Length == 0));
            var key = state.Fields[0].Key;
            var old = state.Version;
            state = State(await Send("receipt.correct", new { expected = state.Version, key, valueRevision = 0, value = "=literal manual entry" }));
            Assert.AreEqual("Corrected", state.Fields[0].Status);
            await Send("receipt.verify", new { expected = old, key, valueRevision = 1 }, false);
            state = State(await Send("receipt.verify", new { expected = state.Version, key, valueRevision = 1 }));
            Assert.AreEqual("Verified", state.Fields[0].Status);
            state = State(await Send("receipt.correct", new { expected = state.Version, key, valueRevision = 1, value = "corrected manual entry" }));
            Assert.AreEqual("Corrected", state.Fields[0].Status);
            state = State(await Send("receipt.verify", new { expected = state.Version, key, valueRevision = 2 }));
            state = State(await Send("receipt.retract", new { expected = state.Version, key, valueRevision = 2 }));
            Assert.AreEqual("Corrected", state.Fields[0].Status);
            var changed = bytes.ToArray(); changed[^1] = 9; File.WriteAllBytes(path, changed);
            var error = await Send("receipt.verify", new { expected = state.Version, key, valueRevision = 2 }, false);
            Assert.AreEqual("SOURCE_INVALIDATED", error.GetProperty("code").GetString());
            state = State(await Send("receipt.state", new { })); Assert.AreEqual("Invalidated", state.Status);
            state = State(await Send("receipt.close", new { })); Assert.IsNull(state.Source); Assert.IsNull(state.Version);
            CollectionAssert.AreEqual(changed, File.ReadAllBytes(path));
            using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public async Task CancelledNativeSelectionDoesNotCreateReceiptOrLeakUntrustedPaths()
    {
        var messages = new List<string>(); var calls = 0;
        using var bridge = new BridgeSession(messages.Add, _ => Task.FromResult(new FileSelection("cancelled")), selectDocument: _ => { calls++; return Task.FromResult<string?>(null); });
        async Task Send(string origin, string id, string command, object payload) => await bridge.HandleAsync(origin, JsonSerializer.Serialize(new { protocolVersion = 1, requestId = id, command, payload }));
        await Send("https://foreign.invalid", "foreign", "receipt.select", new { }); Assert.AreEqual(0, messages.Count);
        await Send(BridgePolicy.Origin, "handshake", "foundation.handshake", new { supportedVersions = new[] { 1 } });
        await Send(BridgePolicy.Origin, "select", "receipt.select", new { });
        Assert.AreEqual(1, calls); Assert.IsTrue(messages[^1].Contains("\"source\":null"));
    }
}
