using System.Text.Json;
using Windows.Data.Pdf;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

// Private one-shot pipe worker. Never accepts a filesystem path or writes a file.
try
{
    var dpi=0;
    if(args.Length!=0 && (args.Length!=2 || args[0]!="--dpi" || !int.TryParse(args[1],out dpi) || dpi is <72 or >1200))throw new InvalidDataException();
    using var input=Console.OpenStandardInput();
    var header=new byte[12]; await input.ReadExactlyAsync(header);
    var page=BitConverter.ToInt32(header,0);var kind=BitConverter.ToInt32(header,4);var length=BitConverter.ToInt32(header,8);
    if(page is <1 or >1000 || kind is <0 or >2 || length<8 || length>(dpi==0?33554432:134217728)) throw new InvalidDataException();
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
        var effectiveDpi=dpi;
        if(dpi!=0)
        {
            // Image bounds are PDF points (72/in); Windows page Size is DIPs (96/in).
            // Only metadata is used: no image export, URL access or input rewriting.
            using var metadata=UglyToad.PdfPig.PdfDocument.Open(bytes);
            foreach(var image in metadata.GetPage(page).GetImages())
            {
                var bounds=image.BoundingBox;
                if(bounds.Width>0 && bounds.Height>0)
                {
                    var native=Math.Max(image.WidthInSamples*72/bounds.Width,image.HeightInSamples*72/bounds.Height);
                    if(!double.IsFinite(native)||native>1200)throw new InvalidDataException("Embedded image DPI exceeds evaluation bounds.");
                    effectiveDpi=Math.Max(effectiveDpi,(int)Math.Ceiling(native));
                }
            }
        }
        var scale=dpi==0?Math.Min(2,1600/Math.Max(w,h)):effectiveDpi/96d;
        width=(uint)Math.Max(1,Math.Ceiling(w*scale));height=(uint)Math.Max(1,Math.Ceiling(h*scale));
        if(width>16384||height>16384||(ulong)width*height>64000000)throw new InvalidDataException();
        await pdfPage.RenderToStreamAsync(output,new PdfPageRenderOptions{DestinationWidth=width,DestinationHeight=height});
    }
    else
    {
        if(page!=1)throw new InvalidDataException();
        var decoder=await BitmapDecoder.CreateAsync(source);
        if(decoder.PixelWidth==0||decoder.PixelHeight==0||(ulong)decoder.PixelWidth*decoder.PixelHeight>(dpi==0?20000000UL:64000000UL))throw new InvalidDataException();
        var scale=dpi==0?Math.Min(1,1600d/Math.Max(decoder.PixelWidth,decoder.PixelHeight)):1;
        width=(uint)Math.Max(1,Math.Ceiling(decoder.PixelWidth*scale));height=(uint)Math.Max(1,Math.Ceiling(decoder.PixelHeight*scale));
        using var bitmap=await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8,BitmapAlphaMode.Premultiplied,
            new BitmapTransform{ScaledWidth=width,ScaledHeight=height},ExifOrientationMode.RespectExifOrientation,ColorManagementMode.DoNotColorManage);
        width=(uint)bitmap.PixelWidth;height=(uint)bitmap.PixelHeight;
        var encoder=await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId,output);
        encoder.SetSoftwareBitmap(bitmap);await encoder.FlushAsync();
    }
    if(output.Size>(dpi==0?12UL:48UL)*1024*1024)throw new InvalidDataException();
    output.Seek(0);var png=new byte[(int)output.Size];
    using(var reader=new DataReader(output)){await reader.LoadAsync((uint)png.Length);reader.ReadBytes(png);reader.DetachStream();}
    Console.Write(JsonSerializer.Serialize(new{pageNumber=page,pageCount=count,width,height,pngBase64=Convert.ToBase64String(png)}));
}
catch { Console.Error.Write("RENDER_REJECTED"); Environment.ExitCode=2; }
