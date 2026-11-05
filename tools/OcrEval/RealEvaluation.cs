using System.Diagnostics;
using System.Globalization;
using System.Text;
using TsekSheet.Application.Documents;

namespace TsekSheet.OcrEval;

// Private text/values exist only in memory. Persist only fixed vocabulary and numeric metrics.
public static class RealEvaluation
{
    private sealed record Truth(string File, int Page, string Type, Dictionary<string,string> Fields, bool Handwritten);
    private sealed record Page(int Number, string Type, Dictionary<string,string> Fields, Dictionary<string,string> Labels, double Seconds, double Peak, int Rotation, bool Error, double Candidate);
    public sealed record Metric(string Engine, string Profile, int Packet, int Page, bool Handwritten, string Field, bool Present, bool Exact, int Edits, int Characters, string Label);
    private sealed record Summary(string Engine, string Profile, int Packet, bool Handwritten, bool Pick, int Pages, int Rotated, int Errors, double Seconds, double Peak);
    private static readonly string[] Header = "file,page,doc_type,vendor,tin,invoice_type,invoice,date,vatable,vat,total,wht,amount_due,rfp_no,voucher_no,notes".Split(',');
    private static string N(double n) => n.ToString("0.000000", CultureInfo.InvariantCulture);
    public static async Task<int> Run(string input, string configPath, string output, string report, CancellationToken token)
    {
        input = Configuration.Local(input); output = Configuration.Local(output); report = Configuration.Local(report); configPath = Configuration.Local(configPath);
        var root = input.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (output.StartsWith(root, StringComparison.OrdinalIgnoreCase) || report.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !report.EndsWith(".md") || Directory.Exists(output)) throw new IOException("Use a new output directory and Markdown report outside the input tree.");
        var csv = Csv.Read(Path.Combine(input, "truth.csv"));
        if (!csv[0].SequenceEqual(Header)) throw new InvalidDataException("Packet truth schema mismatch.");
        var truth = csv.Skip(1).Where(r => r.Any(s => !string.IsNullOrWhiteSpace(s))).Select(r =>
        {
            if (r.Length != Header.Length || !int.TryParse(r[1], out var page) || page < 1) throw new InvalidDataException("Invalid packet truth row.");
            var fields = Header.Zip(r).ToDictionary(x => x.First, x => x.Second);
            _ = Csv.InputPath(input, r[0]);
            return new Truth(r[0], page, PacketRules.TruthType(r[2]), PacketRules.Fields.ToDictionary(f => f, f => fields[f]), fields["notes"].Contains("handwritten", StringComparison.OrdinalIgnoreCase));
        }).ToArray();
        if (truth.Length == 0 || truth.GroupBy(t => (t.File,t.Page)).Any(g => g.Count() > 1)) throw new InvalidDataException("Empty or duplicate packet truth.");
        var config = Configuration.Load(configPath);
        var own = PacketRules.Canonical("tin", config.OwnTin);
        if (own.Length < 9 || own.Take(9).All(c => c == '0')) throw new InvalidDataException("Set ownTin in local configuration before packet evaluation.");
        // PSM6 is T3a's highest field-accuracy Tesseract setting across profiles.
        var engines = new[] { "tesseract-psm6", "paddle-v5-mobile" };
        var clients = engines.ToDictionary(e => e, e => new EngineClient(configPath, e, config));
        await using var orientation = new EngineClient(configPath, "tesseract-psm6", config);
        var renderer = new LocalDocumentRenderer(config.Renderer, TimeSpan.FromSeconds(config.TimeoutSeconds));
        var metrics = new List<Metric>(); var summaries = new List<Summary>();
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"machine.json"),System.Text.Json.JsonSerializer.Serialize(Evaluation.Machine(),Configuration.Json));
        using var results = new StreamWriter(Path.Combine(output, "results.csv")) { AutoFlush = true };
        results.WriteLine("engine,preprocess,packet,page,handwritten,metric,applicable,exact,edits,reference_characters,label,predicted_class,truth_class,rotation_degrees,seconds,peak_mb,error");
        void Row(string engine, string profile, int packet, int page, bool hand, string metric, bool present, bool exact, int edits, int chars, string label = "", string predicted = "", string expected = "", int rotation = 0, double seconds = 0, double peak = 0, bool error = false) =>
            results.WriteLine(Csv.Line([engine, profile, packet.ToString(), page.ToString(), hand ? "1" : "0", metric, present ? "1" : "0", exact ? "1" : "0", edits.ToString(), chars.ToString(), label, predicted, expected, rotation.ToString(), N(seconds), N(peak), error ? "1" : "0"]));
        try
        {
            var packet = 0;
            foreach (var group in truth.GroupBy(t => t.File))
            {
                packet++; var path = Csv.InputPath(input, group.Key); var hash = Configuration.Hash(path); var bytes = File.ReadAllBytes(path);
                var pages = engines.SelectMany(e => Imaging.Profiles.Select(p => (e,p))).ToDictionary(k => k, _ => new List<Page>());
                var count = 1;
                for (var number = 1; number <= count; number++)
                {
                    token.ThrowIfCancellationRequested();
                    byte[]? png = null; double renderSeconds = 0, orientSeconds = 0, orientPeak = 0; int correction = 0;
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        var page = await renderer.RenderAtDpiAsync(bytes, LocalDocumentKind.Pdf, number, 300, token); count = page.PageCount;
                        png = Convert.FromBase64String(page.PngBase64); renderSeconds = watch.Elapsed.TotalSeconds;
                        var probe = await orientation.Recognize(png, "orientation", token); orientSeconds = probe.Seconds; orientPeak = probe.PeakMb; correction = orientation.LastRotation;
                        using var image = OpenCvSharp.Cv2.ImDecode(png, OpenCvSharp.ImreadModes.Color);
                        using var corrected = PacketRules.Rotate(image, correction); png = corrected.ToBytes(".png");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch { png = null; renderSeconds = watch.Elapsed.TotalSeconds; }
                    foreach (var engine in engines) foreach (var profile in Imaging.Profiles)
                    {
                        var fields = PacketRules.Fields.ToDictionary(f => f, _ => ""); var labels = new Dictionary<string,string>(); var type = "other";
                        double seconds = renderSeconds + orientSeconds, peak = orientPeak, candidate = 0; bool error = png is null;
                        if (png is not null) try
                        {
                            var recognized = await clients[engine].Recognize(png, profile, token);
                            seconds += recognized.Seconds; peak = Math.Max(peak, recognized.PeakMb);
                            type = PacketRules.Classify(recognized.Result.Text);
                            fields = PacketRules.Extract(recognized.Result.Text, config.OwnTin, out labels);
                            candidate = (type is "sales_invoice" or "invoice_official_receipt" ? 100 : 0) +
                                (System.Text.RegularExpressions.Regex.IsMatch(recognized.Result.Text, @"\b(INVOICE|RECEIPT)\b", System.Text.RegularExpressions.RegexOptions.IgnoreCase) ? 20 : 0) +
                                (fields["invoice"].Length > 0 ? 3 : 0) + (fields["total"].Length > 0 ? 2 : 0);
                        }
                        catch (OperationCanceledException) { throw; }
                        catch { error = true; }
                        pages[(engine,profile)].Add(new(number,type,fields,labels,seconds,peak,correction,error,candidate));
                        Row(engine, profile, packet, number, false, "page", true, false, 0, 0, predicted:type, rotation:correction, seconds:seconds, peak:peak, error:error);
                    }
                    Console.WriteLine($"Packet {packet}; page {number}/{count} complete.");
                }
                if (group.Any(t => t.Page > count)) throw new InvalidDataException("Truth page exceeds packet page count.");
                foreach (var ((engine,profile), observations) in pages)
                {
                    var chosen = observations.OrderByDescending(p => p.Candidate).ThenBy(p => p.Number).First();
                    var pick = group.Any(t => t.Page == chosen.Number) && !chosen.Error;
                    var hand = group.Any(t => t.Handwritten);
                    summaries.Add(new(engine,profile,packet,hand,pick,count,observations.Count(p => p.Rotation != 0), observations.Count(p => p.Error), observations.Sum(p => p.Seconds), observations.Max(p => p.Peak)));
                    Row(engine, profile, packet, chosen.Number, hand, "invoice_page_pick", true, pick, 0, 0);
                    foreach (var t in group)
                    {
                        var actual = observations.Single(p => p.Number == t.Page);
                        Row(engine,profile,packet,t.Page,t.Handwritten,"classification",true,!actual.Error && actual.Type == t.Type,0,0,predicted:actual.Type,expected:t.Type);
                        foreach (var field in PacketRules.Fields)
                        {
                            var expected = PacketRules.Canonical(field,t.Fields[field]); var observed = PacketRules.Canonical(field,actual.Fields[field]);
                            var present = expected.Length > 0;
                            var m = new Metric(engine,profile,packet,t.Page,t.Handwritten,field,present,!actual.Error && expected == observed,Metrics.Distance(expected,observed),expected.EnumerateRunes().Count(),actual.Labels.GetValueOrDefault(field,"")); metrics.Add(m);
                            Row(engine,profile,packet,t.Page,t.Handwritten,field,present,m.Exact,m.Edits,m.Characters,m.Label);
                        }
                    }
                    var invoice = chosen;
                    foreach (var other in observations)
                    {
                        string? metric = other.Type switch { "rfp" => "invoice_rfp_amount", "check_voucher" => "voucher_document_no", "bir_2307" => "2307_tin", _ => null };
                        if (metric is null) continue;
                        var field = other.Type == "rfp" ? "amount_due" : other.Type == "check_voucher" ? "invoice" : "tin";
                        var a = PacketRules.Canonical(field,invoice.Fields[field == "amount_due" && invoice.Fields[field].Length == 0 ? "total" : field]);
                        var b = PacketRules.Canonical(field,other.Fields[other.Type == "check_voucher" ? "voucher_no" : field == "amount_due" && other.Fields[field].Length == 0 ? "total" : field]);
                        Row(engine,profile,packet,other.Number,false,metric,a.Length > 0 && b.Length > 0,a == b,0,0);
                        if (other.Type == "bir_2307") { a = PacketRules.Canonical("wht",invoice.Fields["wht"]); b = PacketRules.Canonical("wht",other.Fields["wht"]); Row(engine,profile,packet,other.Number,false,"2307_wht",a.Length > 0 && b.Length > 0,a == b,0,0); }
                    }
                }
                if (Configuration.Hash(path) != hash) throw new IOException("Packet changed during evaluation.");
            }
        }
        finally { foreach (var client in clients.Values) await client.DisposeAsync(); }
        var text = new List<string> { "## Real documents", "", "No engine decision. Private packet data stays in memory; results contain metrics only. Tesseract PSM6 selected from T3a field accuracy. Handwritten annotations are separated, not recognized by a handwriting model.", "", "| Engine | Preprocess | Printed exact | Handwritten exact | Field CER | Page pick | Corrected/pages | s/page | Peak MB | Errors |", "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|" };
        static string Accuracy(IEnumerable<Metric> rows) { var a = rows.Where(m => m.Present).ToArray(); return a.Length == 0 ? "n/a" : (a.Count(m => m.Exact)/(double)a.Length).ToString("P1",CultureInfo.InvariantCulture); }
        foreach (var g in summaries.GroupBy(s => (s.Engine,s.Profile)))
        {
            var m = metrics.Where(m => (m.Engine,m.Profile) == g.Key && m.Present).ToArray();
            text.Add($"| {g.Key.Engine} | {g.Key.Profile} | {Accuracy(m.Where(m => !m.Handwritten))} | {Accuracy(m.Where(m => m.Handwritten))} | {(double)m.Sum(m => m.Edits)/Math.Max(1,m.Sum(m => m.Characters)):P2} | {g.Count(s => s.Pick)}/{g.Count()} | {g.Sum(s => s.Rotated)}/{g.Sum(s => s.Pages)} | {g.Sum(s => s.Seconds)/g.Sum(s => s.Pages):F3} | {g.Max(s => s.Peak):F1} | {g.Sum(s => s.Errors)} |");
        }
        text.AddRange(["", "| Engine | Preprocess | Group | Field | Scored | Exact | CER |", "|---|---|---|---|---:|---:|---:|"]);
        foreach (var g in metrics.GroupBy(m => (m.Engine,m.Profile,m.Handwritten,m.Field))) { var a = g.Where(m => m.Present).ToArray(); text.Add($"| {g.Key.Engine} | {g.Key.Profile} | {(g.Key.Handwritten ? "handwritten" : "printed")} | {g.Key.Field} | {a.Length} | {Accuracy(a)} | {(a.Length == 0 ? "n/a" : ((double)a.Sum(m => m.Edits)/Math.Max(1,a.Sum(m => m.Characters))).ToString("P2",CultureInfo.InvariantCulture))} |"); }
        text.AddRange(["", "Only nonempty truth fields enter exact/CER denominators. Fields are scored on labelled pages independently of page selection. Invoice numbers compare digits only; invoice type separately. Service invoice maps to sales_invoice, generic invoice to invoice_official_receipt, other out-of-taxonomy types to other. Unlabelled pages are classified but classification accuracy is unavailable for them. Rotation counts are inferred corrections, not verified rotation accuracy: truth has no orientation labels. Orientation uses four Tesseract text/confidence probes before final OCR; time includes renderer, orientation probes and OCR/IPC. Peak is max sampled orientation/engine job memory, excluding parent/renderer. Report-only cross-checks and exact amount-label categories are in metrics CSV; no values/text/names are persisted. Empty cross-check operands are unavailable, never agreement evidence."]);
        text.Add("Summary exact match pools nonempty field instances, not unweighted field-type averages. All fields, including RFP/voucher references, are scored on their annotated page rather than combined across the packet; this limits interpretation of reference-field misses. Cross-check voucher numbers use the first voucher/document/CV-number label. Time excludes full-resolution parent bitmap rotation/re-encoding and engine initialization. These are extraction-pipeline measurements, not isolated OCR-transcription accuracy.");
        var existing = File.Exists(report) ? File.ReadAllText(report) : "# OCR bake-off\n";
        var marker = existing.IndexOf("## Real documents",StringComparison.Ordinal); if (marker >= 0) existing = existing[..marker];
        Directory.CreateDirectory(Path.GetDirectoryName(report)!); File.WriteAllText(report,existing.TrimEnd()+"\n\n"+string.Join('\n',text)+"\n");
        return summaries.Any(s => s.Errors > 0) ? 1 : 0;
    }
}
