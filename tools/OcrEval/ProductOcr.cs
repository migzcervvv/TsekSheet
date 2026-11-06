using TsekSheet.Application.Documents;
namespace TsekSheet.OcrEval;

// Reuses the evaluated engines; no recognition runs inside the desktop process.
public sealed class ProductOcr(string configPath) : IProductOcr, ILocalDocumentRenderer, IAsyncDisposable
{
    private EngineClient? client;
    private readonly SemaphoreSlim serial = new(1,1);
    public ILocalDocumentRenderer Renderer => this;
    public Task<RenderedDocumentPage> RenderAsync(byte[] bytes,LocalDocumentKind kind,int page,CancellationToken token) =>
        new LocalDocumentRenderer(Path.Combine(AppContext.BaseDirectory,"renderer","TsekSheet.Renderer.exe"))
            .RenderAtDpiAsync(bytes,kind,page,300,token);
    public async Task<OcrProposals> RecognizeAsync(RenderedDocumentPage page,OcrRegion? region,CancellationToken token)
    {
        region?.Validate();
        await serial.WaitAsync(token);
        try
        {
            EvalConfig config;
            try { config=Configuration.Load(configPath); }
            catch(Exception error) when(error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
            {
                throw new DocumentSourceException("OCR_ASSETS_UNAVAILABLE","Local OCR configuration or assets are missing or invalid. Restore the configured assets and retry, or enter values manually.");
            }
            client ??= new EngineClient(configPath,"paddle-v5-mobile",config);
            var png=Convert.FromBase64String(page.PngBase64);
            var r=region??new OcrRegion(0,0,1,1);
            using var image=OpenCvSharp.Cv2.ImDecode(png,OpenCvSharp.ImreadModes.Color);
            var x=(int)(r.X*image.Width);var y=(int)(r.Y*image.Height);
            var width=Math.Max(1,Math.Min(image.Width-x,(int)(r.Width*image.Width)));
            var height=Math.Max(1,Math.Min(image.Height-y,(int)(r.Height*image.Height)));
            using var crop=new OpenCvSharp.Mat(image,new OpenCvSharp.Rect(x,y,width,height));
            var result=await client.Recognize(crop.ToBytes(".png"),"none",token);
            var values=PacketRules.Extract(result.Result,config.OwnTin,out _);
            values["amount"]=values["total"];
            return new(values,result.Result.Words.Take(2000).Select(w=>new OcrWord(w.Text,w.Confidence,
                (w.X+x)/image.Width,(w.Y+y)/image.Height,w.Width/image.Width,w.Height/image.Height)).ToArray());
        }
        finally { serial.Release(); }
    }
    public async ValueTask DisposeAsync(){await serial.WaitAsync();try{if(client!=null)await client.DisposeAsync();}finally{serial.Release();}}
}
