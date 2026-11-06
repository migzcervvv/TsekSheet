using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Foundation;
namespace TsekSheet.Application.Tests;
[TestClass]
public sealed class ProductOcrBridgeTests
{
    private sealed class Fake : IProductOcr, ILocalDocumentRenderer
    {
        public ILocalDocumentRenderer Renderer=>this;
        public bool Wait;
        public TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<RenderedDocumentPage> RenderAsync(byte[] bytes,LocalDocumentKind kind,int page,CancellationToken token)=>Task.FromResult(new RenderedDocumentPage(page,1,100,100,"fake"));
        public async Task<OcrProposals> RecognizeAsync(RenderedDocumentPage page,OcrRegion? region,CancellationToken token)
        {
            Started.TrySetResult();if(Wait)await Task.Delay(Timeout.Infinite,token);
            return new(new Dictionary<string,string>{{"amount","123.50"}},[]);
        }
    }
    [TestMethod]
    public async Task ProposalsDoNotVerifyAndCancellationLeavesReviewEditable()
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".png");
        var bytes=new byte[128];new byte[]{137,80,78,71,13,10,26,10}.CopyTo(bytes,0);File.WriteAllBytes(path,bytes);
        try
        {
            var messages=new List<string>();var fake=new Fake();var n=0;
            using var bridge=new BridgeSession(messages.Add,_=>Task.FromResult(new FileSelection("cancelled")),selectDocument:_=>Task.FromResult<string?>(path),renderer:fake,ocr:fake);
            async Task Send(string command,object payload)=>await bridge.HandleAsync(BridgePolicy.Origin,JsonSerializer.Serialize(new{protocolVersion=1,requestId="ocr-"+n++,command,payload},BridgeSession.JsonOptions));
            JsonElement Last()=>JsonDocument.Parse(messages[^1]).RootElement.Clone();
            await Send("foundation.handshake",new{supportedVersions=new[]{1}});
            await Send("receipt.select",new{});await Send("receipt.start",new{profileId="synthetic-payables",pageNumber=1});
            var state=Last().GetProperty("result").Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            await Send("receipt.ocr",new{source=state.Source!.Document.Version,pageNumber=1,region=(object?)null});
            Assert.IsTrue(Last().GetProperty("ok").GetBoolean());Assert.IsFalse(messages[^1].Contains(path));
            await Send("receipt.state",new{});
            var after=Last().GetProperty("result").Deserialize<ReceiptState>(BridgeSession.JsonOptions)!;
            Assert.AreEqual(state.Version,after.Version);Assert.IsTrue(after.Fields.All(f=>f.Status=="Draft" && f.Value==""));
            fake.Wait=true;fake.Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
            var pending=Send("receipt.ocr",new{source=state.Source.Document.Version,pageNumber=1,region=(object?)null});
            await fake.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));await Send("receipt.cancelRender",new{});await pending;
            Assert.IsTrue(messages.Any(m=>m.Contains("CANCELLED")));
            await Send("receipt.state",new{});Assert.IsTrue(Last().GetProperty("ok").GetBoolean());
        }
        finally { File.Delete(path); }
    }
}
