using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class SourceRenderingTests
{
    private sealed class Renderer(Action action) : ILocalDocumentRenderer
    {
        public Task<RenderedDocumentPage> RenderAsync(byte[] bytes,LocalDocumentKind kind,int page,CancellationToken token)
        { Assert.IsTrue(bytes.AsSpan(0,5).SequenceEqual("%PDF-"u8));action();return Task.FromResult(new RenderedDocumentPage(page,2,10,10,"test observation")); }
    }
    [TestMethod]
    [DataRow("unchanged")] [DataRow("changed")] [DataRow("closed")] [DataRow("cancelled")]
    public async Task RenderChecksFreshSourceAfterWorkerAndNeverVerifiesFields(string scenario)
    {
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".pdf");var bytes="%PDF-1.7\n artificial source association only\n"u8.ToArray();File.WriteAllBytes(path,bytes);
        try
        {
            using var receipt=new DesktopReceiptReview();using var cancellation=new CancellationTokenSource();
            var state=receipt.Register(path,default);var version=state.Source!.Document.Version;
            receipt.Start("synthetic-payables",1,default);
            var before=receipt.State;
            var renderer=new Renderer(()=>{if(scenario=="changed")File.AppendAllText(path,"changed");if(scenario=="closed")receipt.Close();if(scenario=="cancelled")cancellation.Cancel();});
            if(scenario=="changed" || scenario=="closed")await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>receipt.RenderAsync(renderer,version,1,cancellation.Token));
            else if(scenario=="cancelled")await Assert.ThrowsExactlyAsync<OperationCanceledException>(()=>receipt.RenderAsync(renderer,version,1,cancellation.Token));
            else
            {
                var result=await receipt.RenderAsync(renderer,version,2,cancellation.Token);
                Assert.AreEqual(version,result.Source);Assert.AreEqual(2,result.Page.PageNumber);
                CollectionAssert.AreEqual(bytes,File.ReadAllBytes(path));
            }
            if(scenario!="closed")
            {
                Assert.AreEqual(before.Version,receipt.State.Version);
                CollectionAssert.AreEqual(before.Fields.ToArray(),receipt.State.Fields.ToArray());
                Assert.IsTrue(receipt.State.Fields.All(f=>f.Status=="Draft"));
                receipt.Close();
            }
            using(File.Open(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
        }
        finally{File.Delete(path);}
    }
}
