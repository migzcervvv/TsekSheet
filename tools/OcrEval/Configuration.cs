using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TsekSheet.OcrEval;

public static class Configuration
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public static EvalConfig Load(string path, bool verify = true)
    {
        path = Local(path);
        var config = JsonSerializer.Deserialize<EvalConfig>(File.ReadAllText(path), Json) ?? throw new InvalidDataException("Empty config.");
        var root = Path.GetDirectoryName(path)!;
        string Resolve(string value) => Local(Path.IsPathRooted(value) ? value : Path.Combine(root, value));
        config = config with
        {
            Tesseract = Resolve(config.Tesseract), Tessdata = Resolve(config.Tessdata),
            DetectionModel = Resolve(config.DetectionModel), RecognitionModel = Resolve(config.RecognitionModel), Dictionary = Resolve(config.Dictionary),
            VendorList = config.VendorList.Length == 0 ? "" : Resolve(config.VendorList),
            Renderer = config.Renderer.Length == 0 ? Path.Combine(AppContext.BaseDirectory, "renderer", "TsekSheet.Renderer.exe") : Resolve(config.Renderer)
        };
        config.Validate();
        if (verify)
        {
            foreach (var (key, file) in new[] { ("tesseract", config.Tesseract), ("eng", Path.Combine(config.Tessdata, "eng.traineddata")), ("det", config.DetectionModel), ("rec", config.RecognitionModel), ("dictionary", config.Dictionary) })
            {
                if (!config.Sha256.TryGetValue(key, out var expected) || !string.Equals(Hash(file), expected, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"Missing/mismatched SHA-256 for {key}.");
            }
            if (!File.Exists(config.Renderer)) throw new FileNotFoundException("Build the bundled renderer before evaluation.");
        }
        return config;
    }
    public static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }
    public static string Local(string path)
    {
        var full = Path.GetFullPath(path);
        if (full.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(Path.GetPathRoot(full)!).DriveType == DriveType.Network)
            throw new InvalidDataException("Evaluation accepts local paths only.");
        return full;
    }
}
