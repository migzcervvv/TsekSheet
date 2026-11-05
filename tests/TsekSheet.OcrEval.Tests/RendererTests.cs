using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;

namespace TsekSheet.OcrEval.Tests;

[TestClass]
public sealed class RendererTests
{
    private static string Worker
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
            return Path.Combine(directory!.FullName, "src/TsekSheet.Renderer/bin/Release/net10.0-windows10.0.19041.0/TsekSheet.Renderer.exe");
        }
    }
    [TestMethod]
    public async Task VectorPdfUses300DpiAndPreviewRetainsItsOriginalDimensions()
    {
        var bytes = Pdf(false); var before = bytes.ToArray(); var renderer = new LocalDocumentRenderer(Worker);
        var preview = await renderer.RenderAsync(bytes, LocalDocumentKind.Pdf, 1, default);
        var ocr = await renderer.RenderAtDpiAsync(bytes, LocalDocumentKind.Pdf, 1, 300, default);
        Assert.AreEqual(384, preview.Width); Assert.AreEqual(192, preview.Height);
        Assert.AreEqual(600, ocr.Width); Assert.AreEqual(300, ocr.Height);
        CollectionAssert.AreEqual(before, bytes);
    }
    [TestMethod]
    public async Task Embedded600DpiImageRetainsNativeResolutionAboveRequested300Dpi()
    {
        var bytes = Pdf(true); var renderer = new LocalDocumentRenderer(Worker);
        var page = await renderer.RenderAtDpiAsync(bytes, LocalDocumentKind.Pdf, 1, 300, default);
        Assert.AreEqual(600, page.Width); Assert.AreEqual(600, page.Height);
    }
    private static byte[] Pdf(bool image)
    {
        static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
        static byte[] Stream(byte[] value, string attributes = "")
        {
            using var output = new MemoryStream(); output.Write(Ascii($"<< /Length {value.Length} {attributes} >>\nstream\n")); output.Write(value); output.Write(Ascii("\nendstream")); return output.ToArray();
        }
        var objects = new List<byte[]>
        {
            Ascii("<< /Type /Catalog /Pages 2 0 R >>"), Ascii("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
            Ascii(image ? "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 72] /Resources << /XObject << /Im0 5 0 R >> >> /Contents 4 0 R >>" : "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 144 72] /Resources << >> /Contents 4 0 R >>"),
            Stream(Ascii(image ? "q 72 0 0 72 0 0 cm /Im0 Do Q" : "0 0 0 rg 10 10 100 20 re f"))
        };
        if (image)
        {
            var pixels = new byte[600 * 600 * 3];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = (byte)(i % 251);
            using var compressed = new MemoryStream(); using (var zlib = new ZLibStream(compressed, CompressionLevel.Optimal, true)) zlib.Write(pixels);
            objects.Add(Stream(compressed.ToArray(), "/Type /XObject /Subtype /Image /Width 600 /Height 600 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode"));
        }
        using var pdf = new MemoryStream(); pdf.Write(Ascii("%PDF-1.7\n")); var offsets = new List<long> { 0 };
        for (var i = 0; i < objects.Count; i++) { offsets.Add(pdf.Position); pdf.Write(Ascii($"{i + 1} 0 obj\n")); pdf.Write(objects[i]); pdf.Write(Ascii("\nendobj\n")); }
        var xref = pdf.Position; pdf.Write(Ascii($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n"));
        foreach (var offset in offsets.Skip(1)) pdf.Write(Ascii($"{offset:D10} 00000 n \n"));
        pdf.Write(Ascii($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n")); return pdf.ToArray();
    }
}
