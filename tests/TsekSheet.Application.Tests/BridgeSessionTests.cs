using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Foundation;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class BridgeSessionTests
{
    private const string Source = "https://tseksheet.local/index.html";
    private static string Request(string id, string command, object payload, int version = 1) =>
        JsonSerializer.Serialize(new { protocolVersion = version, requestId = id, command, payload });
    private static BridgeSession Session(List<JsonElement> messages, Func<int, CancellationToken, Task>? delay = null) =>
        new(json => messages.Add(JsonDocument.Parse(json).RootElement.Clone()), _ => Task.FromResult(new FileSelection("cancelled")), delay);
    private static Task Handshake(BridgeSession session) => session.HandleAsync(Source, Request("hello", "foundation.handshake", new { supportedVersions = new[] { 1 } }));

    [TestMethod]
    public async Task SharedProtocolFixture_MatchesSerializedHostResponse()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "protocol-v1.json")));
        var messages = new List<JsonElement>(); using var session = Session(messages);
        await session.HandleAsync(Source, fixture.RootElement.GetProperty("handshakeRequest").GetRawText());
        await session.HandleAsync(Source, Request("fixture-info", "foundation.getInfo", new { }));
        Assert.AreEqual(JsonSerializer.Serialize(fixture.RootElement.GetProperty("metadataResponse")),
            messages[^1].GetRawText());
    }

    [TestMethod]
    public async Task HandshakeAndMetadata_AreCorrelatedAndHonest()
    {
        var messages = new List<JsonElement>(); using var session = Session(messages);
        await Handshake(session);
        await session.HandleAsync(Source, Request("info", "foundation.getInfo", new { }));
        Assert.AreEqual("info", messages[1].GetProperty("requestId").GetString());
        Assert.AreEqual("TsekSheet", messages[1].GetProperty("result").GetProperty("applicationName").GetString());
        Assert.IsFalse(messages[0].GetProperty("result").GetProperty("capabilities").GetProperty("workbooks").GetBoolean());
    }

    [TestMethod]
    public async Task UntrustedOrigins_AreDroppedBeforeDispatch()
    {
        foreach (var source in new[] { "http://tseksheet.local", "https://tseksheet.local.evil", "https://tseksheet.local:444", "https://user@tseksheet.local", "file:///index.html" })
        {
            var messages = new List<JsonElement>(); using var session = Session(messages);
            await session.HandleAsync(source, Request("hello", "foundation.handshake", new { supportedVersions = new[] { 1 } }));
            Assert.AreEqual(0, messages.Count, source);
        }
    }

    [TestMethod]
    public async Task InvalidCommandsVersionsPayloadsAndDuplicateIds_AreRejected()
    {
        var messages = new List<JsonElement>(); using var session = Session(messages); await Handshake(session);
        await session.HandleAsync(Source, Request("unknown", "workbook.save", new { }));
        await session.HandleAsync(Source, Request("version", "foundation.getInfo", new { }, 2));
        await session.HandleAsync(Source, Request("payload", "diagnostics.run", new { steps = 999, delayMs = 10 }));
        await session.HandleAsync(Source, Request("hello", "foundation.getInfo", new { }));
        CollectionAssert.AreEqual(new[] { "UNSUPPORTED_COMMAND", "UNSUPPORTED_VERSION", "INVALID_REQUEST", "DUPLICATE_REQUEST" },
            messages.Skip(1).Select(m => m.GetProperty("error").GetProperty("code").GetString()).ToArray());
    }

    [TestMethod]
    public async Task MalformedAndOversizedMessages_DoNotEscapeOrExecute()
    {
        var messages = new List<JsonElement>(); using var session = Session(messages);
        foreach (var json in new[] { "not json", "null", "[]", new string(' ', BridgeSession.MaxMessageBytes + 1), "{\"requestId\":\"a\",\"protocolVersion\":\"bad\",\"command\":\"foundation.getInfo\",\"payload\":{}}" })
            await session.HandleAsync(Source, json);
        Assert.IsTrue(messages.All(m => !m.GetProperty("ok").GetBoolean()));
    }

    [TestMethod]
    public async Task Diagnostic_EmitsProgressAndExactlyOneTerminalResult()
    {
        var messages = new List<JsonElement>(); using var session = Session(messages, (_, _) => Task.CompletedTask); await Handshake(session);
        await session.HandleAsync(Source, Request("run", "diagnostics.run", new { steps = 3, delayMs = 10 }));
        Assert.AreEqual(3, messages.Count(m => m.GetProperty("type").GetString() == "event"));
        Assert.AreEqual(1, messages.Count(m => m.GetProperty("type").GetString() == "response" && m.GetProperty("requestId").GetString() == "run"));
        Assert.AreEqual("completed", messages[^1].GetProperty("result").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task Diagnostic_CanBeCancelledWithoutRealTimeDelays()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new List<JsonElement>();
        using var session = Session(messages, async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); });
        await Handshake(session);
        var run = session.HandleAsync(Source, Request("run", "diagnostics.run", new { steps = 3, delayMs = 10 }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await session.HandleAsync(Source, Request("cancel", "diagnostics.cancel", new { operationId = "run" }));
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual("cancelled", messages.Single(m => m.TryGetProperty("requestId", out var id) && id.GetString() == "run").GetProperty("result").GetProperty("status").GetString());
    }

    [TestMethod]
    public async Task DisposingThePage_CancelsWorkAndSuppressesLateMessages()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var messages = new List<JsonElement>();
        using var session = Session(messages, async (_, token) => { entered.SetResult(); await Task.Delay(Timeout.Infinite, token); });
        await Handshake(session);
        var run = session.HandleAsync(Source, Request("run", "diagnostics.run", new { steps = 3, delayMs = 10 }));
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        session.Dispose();
        await run.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.AreEqual(1, messages.Count, "A disposed page must receive no terminal or progress events.");
    }

    [TestMethod]
    public async Task Selection_ContainsNoNativePathAndRequiresHandshake()
    {
        var messages = new List<JsonElement>(); using var session = Session(messages);
        await session.HandleAsync(Source, Request("early", "foundation.selectFile", new { }));
        Assert.AreEqual("NOT_READY", messages[0].GetProperty("error").GetProperty("code").GetString());
        await Handshake(session);
        await session.HandleAsync(Source, Request("select", "foundation.selectFile", new { }));
        Assert.AreEqual("cancelled", messages[^1].GetProperty("result").GetProperty("status").GetString());
    }
}
