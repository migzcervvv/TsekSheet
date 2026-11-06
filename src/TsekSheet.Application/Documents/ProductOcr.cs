namespace TsekSheet.Application.Documents;

public sealed record OcrRegion(double X, double Y, double Width, double Height)
{
    public void Validate()
    {
        if (!double.IsFinite(X+Y+Width+Height) || X<0 || Y<0 || Width<=0 || Height<=0 || X+Width>1 || Y+Height>1)
            throw new ArgumentException("Select a region inside the source page.");
    }
}
public sealed record OcrWord(string Text,double Confidence,double X,double Y,double Width,double Height);
public sealed record OcrProposals(IReadOnlyDictionary<string,string> Values,IReadOnlyList<OcrWord> Words);
public interface IProductOcr
{
    ILocalDocumentRenderer Renderer { get; }
    Task<OcrProposals> RecognizeAsync(RenderedDocumentPage page,OcrRegion? region,CancellationToken token);
}
public sealed class OcrIdentityRenderer(RenderedDocumentPage page) : ILocalDocumentRenderer
{
    public Task<RenderedDocumentPage> RenderAsync(byte[] bytes,LocalDocumentKind kind,int number,CancellationToken token) => Task.FromResult(page);
}
