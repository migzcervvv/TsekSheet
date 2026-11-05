namespace TsekSheet.OcrEval;

public sealed record WordBox(string Text, double Confidence, double X, double Y, double Width, double Height);
public sealed record OcrResult(string Text, IReadOnlyList<WordBox> Words);
public interface IOcrEngine : IDisposable
{
    Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken token);
}
public sealed record ReceiptFields(string Vendor, string Tin, string Invoice, string Date, string Amount)
{
    public string[] Values => [Vendor, Tin, Invoice, Date, Amount];
}
public sealed record Vendor(string Name, string Tin);
public sealed record Truth(string File, ReceiptFields Fields);
public sealed record EngineRequest(string Engine, byte[] Png);
public sealed record WorkerReply(OcrResult? Result, string? Error, double PreprocessSeconds = 0, double OcrSeconds = 0);

public sealed record EvalConfig
{
    public string Tesseract { get; init; } = "";
    public string Tessdata { get; init; } = "";
    public string DetectionModel { get; init; } = "";
    public string RecognitionModel { get; init; } = "";
    public string Dictionary { get; init; } = "";
    public string VendorList { get; init; } = "";
    public string Renderer { get; init; } = "";
    public Dictionary<string, string> Sha256 { get; init; } = [];
    public int Threads { get; init; } = 2;
    public int TimeoutSeconds { get; init; } = 120;
    public int MemoryMb { get; init; } = 1536;
    public int DetectionMaxSide { get; init; } = 960;
    public float DetectionThreshold { get; init; } = .3f;
    public float BoxThreshold { get; init; } = .6f;
    public float UnclipRatio { get; init; } = 1.5f;
    public void Validate()
    {
        if (Threads is < 1 or > 32 || TimeoutSeconds is < 1 or > 600 || MemoryMb is < 256 or > 4096 ||
            DetectionMaxSide is < 320 or > 2048 || DetectionThreshold is <= 0 or >= 1 ||
            BoxThreshold is <= 0 or >= 1 || UnclipRatio is < 1 or > 4)
            throw new InvalidDataException("Invalid OCR resource or detector configuration.");
    }
}
