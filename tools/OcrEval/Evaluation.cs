using System.Diagnostics;
using System.Globalization;
using System.Management;
using System.Text.Json;
using TsekSheet.Application.Documents;

namespace TsekSheet.OcrEval;

public sealed record EvaluationRow(string File, int Page, string Variant, string Engine, string Preprocess, string Status,
    ReceiptFields Expected, ReceiptFields Actual, int[] Exact, int Edits, int ReferenceCharacters,
    double RenderSeconds, double PreprocessSeconds, double OcrSeconds, double SecondsPage, double PeakMb, string Error);

public static class Evaluation
{
    private sealed class DocumentMeasurement
    {
        public readonly System.Text.StringBuilder Text = new();
        public double Render, Preprocess, Ocr, Elapsed, Peak;
        public string Error = "";
    }
    public static readonly string[] Engines = ["tesseract-psm3", "tesseract-psm6", "tesseract-psm11", "paddle-v5-mobile"];
    private static string Number(double value) => value.ToString("0.000000", CultureInfo.InvariantCulture);
    public static async Task<int> Run(string input, string configPath, string output, string report, CancellationToken token)
    {
        input = Configuration.Local(input); configPath = Configuration.Local(configPath); output = Configuration.Local(output); report = Configuration.Local(report);
        var inputRoot = input.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (output.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase) || report.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase) || !report.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            throw new IOException("Reports must be Markdown outside the input tree; run artifacts must also be outside the input tree.");
        var config = Configuration.Load(configPath); var truths = Csv.Truths(input);
        foreach (var truth in truths)
        {
            var canonical = Canonical(truth.Fields);
            if (truth.Fields.Tin.Length > 0 && canonical.Tin.Length == 0 || truth.Fields.Date.Length > 0 && canonical.Date.Length == 0 || truth.Fields.Amount.Length > 0 && canonical.Amount.Length == 0)
                throw new InvalidDataException($"Invalid ground-truth TIN/date/amount in {truth.File}.");
        }
        if (Directory.Exists(output)) throw new IOException("A run directory must be new; previous evidence is never replaced.");
        Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "words"));
        var vendorPath = config.VendorList.Length > 0 ? config.VendorList : Path.Combine(input, "vendors.csv");
        var vendors = File.Exists(vendorPath) ? Csv.Read(vendorPath).Skip(1).Select(r => r.Length == 2 ? new Vendor(r[0], r[1]) : throw new InvalidDataException("vendors.csv requires name,tin rows.")).ToArray() : [];
        var machine = Machine();
        File.WriteAllText(Path.Combine(output, "machine.json"), JsonSerializer.Serialize(machine, Configuration.Json));
        File.WriteAllText(Path.Combine(output, "run.json"), JsonSerializer.Serialize(new
        {
            StartedUtc = DateTime.UtcNow, TruthSha256 = Configuration.Hash(Path.Combine(input, "truth.csv")),
            TruthFiles = truths.Length, VendorListSha256 = File.Exists(vendorPath) ? Configuration.Hash(vendorPath) : null,
            Config = config, Runtime = Environment.Version.ToString(), OpenCv = OpenCvSharp.Cv2.GetVersionString(),
            CerScope = "sum of Levenshtein edits across five canonical truth fields / sum of reference Unicode characters; not full-page CER",
            PeakScope = "20ms sampled aggregate resident working set of active OCR worker job, including preprocessing and Tesseract child; excludes renderer/evaluator and other idle engine jobs",
            TimingScope = "render + preprocessing + OCR + pipe overhead; engine initialization reported separately; serial warm engine workers",
            WordBoxScope = "preprocessed bitmap; Paddle word subdivisions estimated from CTC timestep alignment within detected line bounds"
        }, Configuration.Json));
        var clients = Engines.ToDictionary(e => e, e => new EngineClient(configPath, e, config));
        var rows = new List<EvaluationRow>();
        using var csv = new StreamWriter(new FileStream(Path.Combine(output, "results.csv"), FileMode.CreateNew)) { AutoFlush = true };
        csv.WriteLine("file,pages,variant,engine,preprocess,status,expected_vendor,expected_tin,expected_invoice,expected_date,expected_amount,vendor,tin,invoice,date,amount,vendor_exact,tin_exact,invoice_exact,date_exact,amount_exact,cer,edits,reference_characters,render_seconds,preprocess_seconds,ocr_seconds,seconds_page,peak_mb,error");
        var renderer = new LocalDocumentRenderer(config.Renderer, TimeSpan.FromSeconds(config.TimeoutSeconds));
        var counter = 0;
        try
        {
            foreach (var truth in truths)
            {
                token.ThrowIfCancellationRequested();
                var path = Csv.InputPath(input, truth.File); var info = new FileInfo(path);
                if (info.Length > 134217728) throw new InvalidDataException("Input exceeds the 128 MiB renderer limit.");
                var bytes = File.ReadAllBytes(path); var sourceHash = Configuration.Hash(path);
                var measurements = (from profile in Imaging.Profiles from engine in Engines select (engine, profile)).ToDictionary(k => k, _ => new DocumentMeasurement());
                var kind = Path.GetExtension(path).ToLowerInvariant() switch { ".pdf" => LocalDocumentKind.Pdf, ".png" => LocalDocumentKind.Png, _ => LocalDocumentKind.Jpeg };
                var pages = 1;
                for (var page = 1; page <= pages; page++)
                {
                    byte[]? png = null; string renderError = ""; var renderWatch = Stopwatch.StartNew();
                    try
                    {
                        var rendered = await renderer.RenderAtDpiAsync(bytes, kind, page, 300, token); pages = rendered.PageCount;
                        png = Convert.FromBase64String(rendered.PngBase64);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error) { renderError = error.GetType().Name + ": " + error.Message; }
                    var renderSeconds = renderWatch.Elapsed.TotalSeconds;
                    foreach (var profile in Imaging.Profiles) foreach (var engine in Engines)
                    {
                        token.ThrowIfCancellationRequested();
                        OcrResult result = new("", []); string error = renderError; double seconds = 0, peak = 0, preprocess = 0, ocr = 0;
                        var requestWatch = Stopwatch.StartNew();
                        if (png is not null)
                            try
                            {
                                var measured = await clients[engine].Recognize(png, profile, token);
                                (result, seconds, peak, preprocess, ocr) = measured;
                            }
                            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                            catch (Exception failure) { seconds = requestWatch.Elapsed.TotalSeconds; error = failure.GetType().Name + ": " + failure.Message; }
                        var measuredDocument = measurements[(engine, profile)];
                        if (measuredDocument.Text.Length + result.Text.Length <= 16_000_000) measuredDocument.Text.AppendLine(result.Text);
                        else error = "Document OCR text exceeds limits.";
                        measuredDocument.Render += renderSeconds; measuredDocument.Preprocess += preprocess; measuredDocument.Ocr += ocr;
                        measuredDocument.Elapsed += renderSeconds + seconds; measuredDocument.Peak = Math.Max(measuredDocument.Peak, peak);
                        if (error.Length > 0) measuredDocument.Error = error.Length > 4096 ? error[..4096] : error;
                        File.WriteAllText(Path.Combine(output, "words", $"{++counter:000000}.json"), JsonSerializer.Serialize(new { File = truth.File, Page = page, Engine = engine, Preprocess = profile, Error = error, Result = result }, Configuration.Json));
                        Console.WriteLine($"{counter}: {truth.File} page {page} {engine}/{profile}: {(error.Length == 0 ? "ok" : error)}");
                    }
                }
                foreach (var ((engine, profile), measurement) in measurements)
                {
                        var error = measurement.Error;
                        var actual = Canonical(Extraction.Extract(measurement.Text.ToString(), vendors)); var expected = Canonical(truth.Fields);
                        var exact = expected.Values.Zip(actual.Values, (a, b) => error.Length == 0 && Metrics.Normalize(a) == Metrics.Normalize(b) ? 1 : 0).ToArray();
                        var edits = expected.Values.Zip(actual.Values, (a, b) => Metrics.Distance(Metrics.Normalize(a), Metrics.Normalize(b))).Sum();
                        var characters = expected.Values.Sum(v => Metrics.Normalize(v).EnumerateRunes().Count());
                        var variant = Path.GetFileNameWithoutExtension(truth.File).Split("__").Last();
                        if (!Path.GetFileNameWithoutExtension(truth.File).Contains("__")) variant = "input";
                        var row = new EvaluationRow(truth.File, pages, variant, engine, profile, error.Length == 0 ? "ok" : "error", expected, actual, exact, edits, characters, measurement.Render, measurement.Preprocess, measurement.Ocr, measurement.Elapsed / pages, measurement.Peak, error);
                        rows.Add(row);
                        csv.WriteLine(Csv.Line(new[] { truth.File, pages.ToString(CultureInfo.InvariantCulture), variant, engine, profile, row.Status }.Concat(expected.Values).Concat(actual.Values)
                            .Concat(exact.Select(i => i.ToString(CultureInfo.InvariantCulture))).Concat(new[] { Number((double)edits / Math.Max(1, characters)), edits.ToString(CultureInfo.InvariantCulture), characters.ToString(CultureInfo.InvariantCulture), Number(row.RenderSeconds), Number(row.PreprocessSeconds), Number(row.OcrSeconds), Number(row.SecondsPage), Number(row.PeakMb), error })));
                }
                if (sourceHash != Configuration.Hash(path)) throw new IOException("Input changed during evaluation; discard this run's comparisons.");
            }
        }
        finally
        {
            foreach (var client in clients.Values) await client.DisposeAsync();
            File.WriteAllText(Path.Combine(output, "initialization.json"), JsonSerializer.Serialize(clients.ToDictionary(c => c.Key, c => c.Value.InitializationSeconds), Configuration.Json));
            if (rows.Count > 0) WriteSummary(rows, output, report, machine);
        }
        return rows.Any(r => r.Status != "ok") ? 1 : 0;
    }
    public static ReceiptFields Canonical(ReceiptFields fields) => new(Metrics.Normalize(fields.Vendor), Extraction.NormalizeTin(fields.Tin), Metrics.Normalize(fields.Invoice), Extraction.NormalizeDate(fields.Date), Extraction.NormalizeAmount(fields.Amount));
    internal static Dictionary<string, object?> Machine()
    {
        using var cpuQuery = new ManagementObjectSearcher("SELECT Name, NumberOfLogicalProcessors FROM Win32_Processor");
        using var computerQuery = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
        using var osQuery = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem");
        using var cpus = cpuQuery.Get(); using var computers = computerQuery.Get(); using var systems = osQuery.Get();
        var cpuNames = new List<string>(); var logical = 0;
        foreach (ManagementObject cpu in cpus) using (cpu) { cpuNames.Add((string)cpu["Name"]); logical += Convert.ToInt32(cpu["NumberOfLogicalProcessors"]); }
        ulong ram = 0; foreach (ManagementObject computer in computers) using (computer) ram += Convert.ToUInt64(computer["TotalPhysicalMemory"]);
        string os = "", version = "", build = "";
        foreach (ManagementObject system in systems) using (system) { os = (string)system["Caption"]; version = (string)system["Version"]; build = (string)system["BuildNumber"]; }
        return new() { ["source"] = "local WMI root/CIMV2", ["cpu"] = cpuNames, ["logicalProcessors"] = logical, ["ramBytes"] = ram, ["os"] = os, ["version"] = version, ["build"] = build };
    }
    private static void WriteSummary(List<EvaluationRow> rows, string output, string report, Dictionary<string, object?> machine)
    {
        var text = new List<string> { "# OCR bake-off — T3a synthetic evaluation", "", "No engine decision. These are evaluation results, not verified product fields.", "", $"Run: `{Path.GetRelativePath(Directory.GetCurrentDirectory(), output).Replace('\\', '/')}`. {rows.Count} engine/profile/page observations; {rows.Count(r => r.Status != "ok")} errors.", "", "Field accuracy is macro exact match over vendor/TIN/invoice/date/amount after documented canonicalization. CER is the micro character error rate across those five fields, not the full receipt transcript. Seconds/page includes rendering, preprocessing, OCR and IPC; engine initialization is separate. Peak MB is sampled resident OCR-job memory (worker + Tesseract child), including preprocessing. See `run.json`, `machine.json`, `variant-summary.csv` and raw word JSON.", "", $"Machine (WMI): {string.Join("; ", (List<string>)machine["cpu"]!)}; {Convert.ToDouble(machine["ramBytes"]) / 1073741824d:0.0} GiB; {machine["os"]} {machine["version"]} build {machine["build"]}.", "", "| Engine | Preprocess | Pages | Errors | Field accuracy | CER | s/page | Peak MB |", "|---|---|---:|---:|---:|---:|---:|---:|" };
        using var variants = new StreamWriter(new FileStream(Path.Combine(output, "variant-summary.csv"), FileMode.Create));
        variants.WriteLine("engine,preprocess,variant,pages,errors,vendor_accuracy,tin_accuracy,invoice_accuracy,date_accuracy,amount_accuracy,field_accuracy,cer,seconds_page,peak_mb");
        foreach (var group in rows.GroupBy(r => (r.Engine, r.Preprocess)))
        {
            var a = group.ToArray();
            text.Add($"| {group.Key.Engine} | {group.Key.Preprocess} | {a.Sum(r => r.Page)} | {a.Count(r => r.Status != "ok")} | {a.Sum(r => r.Exact.Sum()) / (5d * a.Length):P1} | {(double)a.Sum(r => r.Edits) / Math.Max(1, a.Sum(r => r.ReferenceCharacters)):P2} | {a.Sum(r => r.SecondsPage * r.Page) / a.Sum(r => r.Page):F3} | {a.Max(r => r.PeakMb):F1} |");
            foreach (var variant in a.GroupBy(r => r.Variant))
            {
                var values = variant.ToArray();
                variants.WriteLine(Csv.Line(new[] { group.Key.Engine, group.Key.Preprocess, variant.Key, values.Sum(r => r.Page).ToString(), values.Count(r => r.Status != "ok").ToString() }
                    .Concat(Enumerable.Range(0, 5).Select(i => Number(values.Average(r => r.Exact[i])))).Concat(new[] { Number(values.Sum(r => r.Exact.Sum()) / (5d * values.Length)), Number((double)values.Sum(r => r.Edits) / Math.Max(1, values.Sum(r => r.ReferenceCharacters))), Number(values.Sum(r => r.SecondsPage * r.Page) / values.Sum(r => r.Page)), Number(values.Max(r => r.PeakMb)) })));
            }
        }
        text.Add(""); text.Add("Synthetic-only evidence. Redacted-document evaluation remains T3b; T4 integration has not started. Failed rows remain in all denominators. Paddle word boxes are CTC-aligned estimates in preprocessed coordinates. Models and binaries are local, hash-checked and never committed.");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.WriteAllLines(report, text);
    }
}
