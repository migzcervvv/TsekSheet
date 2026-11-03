using System.Text.Json;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

// Private one-shot pipe worker. Never accepts a filesystem path or writes a file.
try
{
    using var input=Console.OpenStandardInput();
    var header=new byte[12]; await input.ReadExactlyAsync(header);
    var page=BitConverter.ToInt32(header,0);var kind=BitConverter.ToInt32(header,4);var length=BitConverter.ToInt32(header,8);
    if(page is <1 or >1000 || kind is <0 or >2 || length is <8 or >33554432) throw new InvalidDataException();
    var bytes=new byte[length];await input.ReadExactlyAsync(bytes);
    using var source=new InMemoryRandomAccessStream();
    using(var writer=new DataWriter(source)){writer.WriteBytes(bytes);await writer.StoreAsync();writer.DetachStream();}
    source.Seek(0);
    using var output=new InMemoryRandomAccessStream();
    uint count=1,width,height;
    if(kind==0)
    {
        var document=await PdfDocument.LoadFromStreamAsync(source);
        count=document.PageCount;
        if(count is <1 or >1000 || page>count || document.IsPasswordProtected) throw new InvalidDataException();
        using var pdfPage=document.GetPage((uint)page-1);
        var w=pdfPage.Size.Width;var h=pdfPage.Size.Height;
        if(!double.IsFinite(w)||!double.IsFinite(h)||w<=0||h<=0||w>14400||h>14400)throw new InvalidDataException();
        var scale=Math.Min(2,1600/Math.Max(w,h));
        width=(uint)Math.Max(1,Math.Ceiling(w*scale));height=(uint)Math.Max(1,Math.Ceiling(h*scale));
        await pdfPage.RenderToStreamAsync(output,new PdfPageRenderOptions{DestinationWidth=width,DestinationHeight=height});
    }
    else
    {
        if(page!=1)throw new InvalidDataException();
        var decoder=await BitmapDecoder.CreateAsync(source);
        if(decoder.PixelWidth==0||decoder.PixelHeight==0||(ulong)decoder.PixelWidth*decoder.PixelHeight>20000000)throw new InvalidDataException();
        var scale=Math.Min(1,1600d/Math.Max(decoder.PixelWidth,decoder.PixelHeight));
        width=(uint)Math.Max(1,Math.Ceiling(decoder.PixelWidth*scale));height=(uint)Math.Max(1,Math.Ceiling(decoder.PixelHeight*scale));
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,
            new BitmapTransform{ScaledWidth=width,ScaledHeight=height},ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage);
        width=(uint)bitmap.PixelWidth;height=(uint)bitmap.PixelHeight;
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
        encoder.SetSoftwareBitmap(bitmap);await encoder.FlushAsync();
    }
    if(output.Size>12*1024*1024)throw new InvalidDataException();
    output.Seek(0);var png=new byte[(int)output.Size];
    using(var reader=new DataReader(output)){await reader.LoadAsync((uint)png.Length);reader.ReadBytes(png);reader.DetachStream();}
    Console.Write(JsonSerializer.Serialize(new{pageNumber=page,pageCount=count,width,height,pngBase64=Convert.ToBase64String(png)}));
}
catch { Console.Error.Write("RENDER_REJECTED"); Environment.ExitCode=2; }
