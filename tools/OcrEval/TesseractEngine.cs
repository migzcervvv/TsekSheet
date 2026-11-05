using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace TsekSheet.OcrEval;

internal sealed class TesseractEngine(EvalConfig config, int psm) : IOcrEngine
{
    public async Task<OcrResult> RecognizeAsync(byte[] png, CancellationToken token)
    {
        var start = new ProcessStartInfo(config.Tesseract) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "stdin", "stdout", "--tessdata-dir", config.Tessdata, "-l", "eng", "--oem", "1", "--psm", psm.ToString(CultureInfo.InvariantCulture), "-c", "tessedit_create_tsv=1" }) start.ArgumentList.Add(arg);
        start.Environment["OMP_THREAD_LIMIT"] = config.Threads.ToString(CultureInfo.InvariantCulture);
        start.Environment["PATH"] = Path.GetDirectoryName(config.Tesseract) + ";" + Environment.GetEnvironmentVariable("PATH");
        using var process = Process.Start(start) ?? throw new IOException("Tesseract failed to start.");
        var output = ReadBounded(process.StandardOutput, token); var error = ReadBounded(process.StandardError, token);
        await process.StandardInput.BaseStream.WriteAsync(png, token); process.StandardInput.Close();
        await process.WaitForExitAsync(token);
        var tsv = await output; var stderr = await error;
        if (process.ExitCode != 0) throw new IOException($"Tesseract exit {process.ExitCode}: {stderr}");
        var words = new List<WordBox>();
        foreach (var line in tsv.Split('\n').Skip(1))
        {
            var c = line.TrimEnd('\r').Split('\t', 12);
            if (c.Length != 12 || c[0] != "5" || string.IsNullOrWhiteSpace(c[11])) continue;
            var word = new WordBox(c[11], double.Parse(c[10], CultureInfo.InvariantCulture) / 100,
                int.Parse(c[6], CultureInfo.InvariantCulture), int.Parse(c[7], CultureInfo.InvariantCulture), int.Parse(c[8], CultureInfo.InvariantCulture), int.Parse(c[9], CultureInfo.InvariantCulture));
            words.Add(word);
        }
        return new(ReadingOrder.Text(words), words);
    }
    private static async Task<string> ReadBounded(StreamReader reader, CancellationToken token)
    {
        var result = new StringBuilder(); var buffer = new char[8192]; int length;
        while ((length = await reader.ReadAsync(buffer, token)) > 0)
        {
            if (result.Length + length > 16_000_000) throw new IOException("Tesseract output limit exceeded.");
            result.Append(buffer, 0, length);
        }
        return result.ToString();
    }
    public void Dispose() { }
}
