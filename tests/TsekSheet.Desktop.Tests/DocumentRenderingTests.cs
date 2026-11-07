using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;

namespace TsekSheet.Desktop.Tests;

[TestClass]
public sealed class DocumentRenderingTests
{
    internal static string Worker=>Path.Combine(AppContext.BaseDirectory,"renderer","TsekSheet.Renderer.exe");
    internal static byte[] Pdf(int pageCount=2)
    {
        var font=3+pageCount*2;
        var objects=new List<string>{"<< /Type /Catalog /Pages 2 0 R >>",$"<< /Type /Pages /Kids [{string.Join(" ",Enumerable.Range(0,pageCount).Select(i=>$"{3+i*2} 0 R"))}] /Count {pageCount} >>"};
        for(var i=0;i<pageCount;i++)
        {
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 200] /Resources << /Font << /F1 {font} 0 R >> >> /Contents {4+i*2} 0 R >>");
            var content=$"BT /F1 20 Tf 20 120 Td (Synthetic receipt page {i+1} - 125.50) Tj ET\n";
            objects.Add($"<< /Length {content.Length} >>\nstream\n{content}endstream");
        }
        objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        var text=new StringBuilder("%PDF-1.7\n");var offsets=new List<int>{0};
        for(var i=0;i<objects.Count;i++){offsets.Add(text.Length);text.Append($"{i+1} 0 obj\n{objects[i]}\nendobj\n");}
        var xref=text.Length;text.Append($"xref\n0 {objects.Count+1}\n0000000000 65535 f \n");
        foreach(var offset in offsets.Skip(1))text.Append($"{offset:D10} 00000 n \n");
        text.Append($"trailer\n<< /Size {objects.Count+1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }
    internal static byte[] Image(bool jpeg=false)
    {
        var pixels=new byte[200*100*4];
        for(var i=0;i<pixels.Length;i+=4){pixels[i]=20;pixels[i+1]=90;pixels[i+2]=180;pixels[i+3]=255;}
        var bitmap=BitmapSource.Create(200,100,96,96,PixelFormats.Bgra32,null,pixels,800);
        BitmapEncoder encoder=jpeg?new JpegBitmapEncoder():new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output=new MemoryStream();encoder.Save(output);return output.ToArray();
    }

    [TestMethod]
    [DataRow(1)] [DataRow(5)]
    public async Task SingleAndFivePageSyntheticDocuments(int count)
    {
        var renderer=new LocalDocumentRenderer(Worker);
        for(var page=1;page<=count;page++)
        {
            var result=await renderer.RenderAsync(Pdf(count),LocalDocumentKind.Pdf,page,default);
            Assert.AreEqual(count,result.PageCount);Assert.AreEqual(page,result.PageNumber);
        }
    }
    [TestMethod]
    public async Task RealPdfPagesRenderOfflineWithBoundedDifferentPixels()
    {
        var renderer=new LocalDocumentRenderer(Worker);var bytes=Pdf();var original=bytes.ToArray();
        var first=await renderer.RenderAsync(bytes,LocalDocumentKind.Pdf,1,default);
        var second=await renderer.RenderAsync(bytes,LocalDocumentKind.Pdf,2,default);
        Assert.AreEqual(2,first.PageCount);Assert.AreEqual(2,second.PageNumber);
        // Windows exposes PDF page Size in DIPs (96/in), rather than PDF points.
        Assert.AreEqual(800,first.Width);Assert.AreEqual(534,first.Height);
        Assert.AreNotEqual(first.PngBase64,second.PngBase64);CollectionAssert.AreEqual(original,bytes);
        await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>renderer.RenderAsync(bytes,LocalDocumentKind.Pdf,3,default));
    }
    [TestMethod]
    [DataRow(false)] [DataRow(true)]
    public async Task RealImagesRenderWithoutChangingInput(bool jpeg)
    {
        var bytes=Image(jpeg);var original=bytes.ToArray();
        var page=await new LocalDocumentRenderer(Worker).RenderAsync(bytes,jpeg?LocalDocumentKind.Jpeg:LocalDocumentKind.Png,1,default);
        Assert.AreEqual(1,page.PageCount);Assert.AreEqual(200,page.Width);Assert.AreEqual(100,page.Height);
        CollectionAssert.AreEqual(original,bytes);
    }
    [TestMethod]
    public async Task MalformedCancelledTimedOutAndMissingWorkersFailClosed()
    {
        await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>new LocalDocumentRenderer(Worker).RenderAsync("%PDF-1.7 garbage"u8.ToArray(),LocalDocumentKind.Pdf,1,default));
        using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(()=>new LocalDocumentRenderer(Worker).RenderAsync(Pdf(),LocalDocumentKind.Pdf,1,cancelled.Token));
        var failure=await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>new LocalDocumentRenderer(Worker,TimeSpan.FromMilliseconds(1)).RenderAsync(Pdf(),LocalDocumentKind.Pdf,1,default));
        Assert.AreEqual("RENDER_TIMEOUT",failure.Code);
        await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>new LocalDocumentRenderer(Path.Combine(AppContext.BaseDirectory,"missing-renderer.exe")).RenderAsync(Pdf(),LocalDocumentKind.Pdf,1,default));
    }
    [TestMethod]
    public async Task RequestBoundsRejectBeforeNativeDecode()
    {
        var renderer=new LocalDocumentRenderer(Worker);
        foreach(var request in new[]{(new byte[7],LocalDocumentKind.Pdf,1),(Pdf(),LocalDocumentKind.Pdf,1001),(Pdf(),(LocalDocumentKind)7,1),(new byte[33554433],LocalDocumentKind.Png,1)})
        {
            var error=await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>renderer.RenderAsync(request.Item1,request.Item2,request.Item3,default));
            Assert.AreEqual("RENDER_LIMIT",error.Code);
        }
        await Assert.ThrowsExactlyAsync<DocumentSourceException>(()=>renderer.RenderAsync(Image(),LocalDocumentKind.Png,2,default));
    }
}
