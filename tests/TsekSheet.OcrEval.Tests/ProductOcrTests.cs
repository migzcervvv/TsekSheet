using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.OcrEval;
namespace TsekSheet.OcrEval.Tests;
[TestClass]
public sealed class ProductOcrTests
{
    [TestMethod]
    public void RegionRejectsNonfiniteAndOutOfPageCoordinates()
    {
        Assert.ThrowsExactly<ArgumentException>(()=>new OcrRegion(double.NaN,0,1,1).Validate());
        Assert.ThrowsExactly<ArgumentException>(()=>new OcrRegion(.9,0,.2,1).Validate());
        new OcrRegion(.1,.1,.5,.5).Validate();
    }
    [TestMethod]
    public async Task SyntheticProductWorkerRecognizesRegionsCancelsAndRecovers()
    {
        var config=Environment.GetEnvironmentVariable("TSEK_TEST_OCR_CONFIG");
        if(string.IsNullOrEmpty(config)){Assert.Inconclusive("Local OCR assets not configured.");return;}
        var folder=Path.Combine(Path.GetTempPath(),"TsekSheet-T4-"+Guid.NewGuid().ToString("N"));
        Synthetic.Generate(folder,1);
        var bytes=File.ReadAllBytes(Directory.GetFiles(folder,"*clean.png").Single());
        await using var ocr=new ProductOcr(config);
        var page=await ocr.RenderAsync(bytes,LocalDocumentKind.Png,1,default);
        var result=await ocr.RecognizeAsync(page,null,default);
        Assert.IsTrue(result.Words.Count>0);Assert.IsTrue(result.Values["amount"].Length>0);
        var region=await ocr.RecognizeAsync(page,new(0,0,1,.5),default);
        Assert.IsTrue(region.Words.All(w=>w.Y<.6));
        using var cancel=new CancellationTokenSource();cancel.CancelAfter(10);
        await Assert.ThrowsAsync<OperationCanceledException>(()=>ocr.RecognizeAsync(page,null,cancel.Token));
        Assert.IsTrue((await ocr.RecognizeAsync(page,null,default)).Words.Count>0);
    }
}
