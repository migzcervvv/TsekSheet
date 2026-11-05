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
    public sealed record Metric(string Engine, string Profile, int Packet, int Page, bool Handwritten, string Field, bool Present, bool Exact, int Edits, int Characters, string Label, bool Expected, string Status, string Scope);
    private sealed record Summary(string Engine, string Profile, int Packet, bool Handwritten, bool Pick, int Pages, int Rotated, int Errors, double Seconds, double Peak,bool InvoiceEligible);
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
        var sources=FieldSources.Load(input);
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
        results.WriteLine("engine,preprocess,packet,page,notes_contains_handwritten,metric,applicable,exact,edits,reference_characters,label,predicted_class,truth_class,rotation_degrees,seconds,peak_mb,error,scope,source_status,expected_present");
        void Row(string engine, string profile, int packet, int page, bool hand, string metric, bool present, bool exact, int edits, int chars, string label = "", string predicted = "", string expected = "", int rotation = 0, double seconds = 0, double peak = 0, bool error = false,string scope="",string status="",bool expectedPresent=false) =>
            results.WriteLine(Csv.Line([engine, profile, packet.ToString(), page.ToString(), hand ? "1" : "0", metric, present ? "1" : "0", exact ? "1" : "0", edits.ToString(), chars.ToString(), label, predicted, expected, rotation.ToString(), N(seconds), N(peak), error ? "1" : "0",scope,status,expectedPresent?"1":"0"]));
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
                        var transform=Stopwatch.StartNew();
                        using var image = OpenCvSharp.Cv2.ImDecode(png, OpenCvSharp.ImreadModes.Color);
                        using var corrected = PacketRules.Rotate(image, correction); png = corrected.ToBytes(".png");
                        orientSeconds+=transform.Elapsed.TotalSeconds;
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
                            fields = PacketRules.Extract(recognized.Result, config.OwnTin, out labels);
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
                    summaries.Add(new(engine,profile,packet,hand,pick,count,observations.Count(p => p.Rotation != 0), observations.Count(p => p.Error), observations.Sum(p => p.Seconds), observations.Max(p => p.Peak),group.Any(t=>FieldSources.IsInvoice(t.Type))));
                    var invoiceEligible=group.Any(t=>FieldSources.IsInvoice(t.Type));
                    Row(engine, profile, packet, chosen.Number, hand, "invoice_page_pick", invoiceEligible, invoiceEligible && pick, 0, 0,scope:invoiceEligible?"invoice_packet":"non_invoice_packet");
                    foreach (var t in group)
                    {
                        var actual = observations.Single(p => p.Number == t.Page);
                        Row(engine,profile,packet,t.Page,t.Handwritten,"classification",true,!actual.Error && actual.Type == t.Type,0,0,predicted:actual.Type,expected:t.Type);
                        foreach (var field in PacketRules.Fields)
                        {
                            var source=FieldSources.Resolve(field,t.Page,t.Type,sources.GetValueOrDefault((t.File,field)));
                            var sourcePage=observations.SingleOrDefault(p=>p.Number==source.Page);
                            var expected = PacketRules.Canonical(field,t.Fields[field]);
                            var observed = sourcePage is null?"":PacketRules.Canonical(field,sourcePage.Fields[field]);
                            var expectedPresent=expected.Length>0;
                            var present=expectedPresent && source.Scorable && sourcePage is not null;
                            var status=!expectedPresent?"no_expectation":source.Scorable && sourcePage is null?"source_page_missing":source.Status;
                            var m = new Metric(engine,profile,packet,source.Page,t.Handwritten,field,present,present && !sourcePage!.Error && expected==observed,present?Metrics.Distance(expected,observed):0,present?expected.EnumerateRunes().Count():0,sourcePage?.Labels.GetValueOrDefault(field,"")??"",expectedPresent,status,source.Scope); metrics.Add(m);
                            Row(engine,profile,packet,source.Page,t.Handwritten,field,present,m.Exact,m.Edits,m.Characters,m.Label,scope:m.Scope,status:m.Status,expectedPresent:expectedPresent);
                        }
                    }
                    var invoice = chosen;
                    foreach (var other in observations)
                    {
                        string? metric = other.Type switch { "rfp" => "invoice_rfp_amount", "check_voucher" => "voucher_document_no", "bir_2307" => "2307_tin", _ => null };
                        if (metric is null) continue;
                        var field = other.Type == "rfp" ? "total" : other.Type == "check_voucher" ? "invoice" : "tin";
                        var a = PacketRules.Canonical(field,invoice.Fields[field]);
                        var b = PacketRules.Canonical(field,other.Fields[other.Type == "check_voucher" ? "document_reference" : field]);
                        var refStatus=sources.GetValueOrDefault((group.Key,"document_reference"));
                        var available=invoiceEligible && a.Length>0 && b.Length>0 && (other.Type!="check_voucher" || refStatus is null || refStatus.Scorable);
                        Row(engine,profile,packet,other.Number,false,metric,available,available && a==b,0,0,scope:other.Type=="rfp"?"gross_to_gross":"report_only");
                        if (other.Type == "bir_2307") { a = PacketRules.Canonical("wht",invoice.Fields["wht"]); b = PacketRules.Canonical("wht",other.Fields["wht"]); Row(engine,profile,packet,other.Number,false,"2307_wht",invoiceEligible && a.Length > 0 && b.Length > 0,invoiceEligible && a == b,0,0); }
                    }
                }
                if (Configuration.Hash(path) != hash) throw new IOException("Packet changed during evaluation.");
            }
        }
        finally { foreach (var client in clients.Values) await client.DisposeAsync(); }
        WriteReport(input,report,metrics,summaries,"Completed the requested evaluation scope.",Path.Combine(output,"results.csv"));
        return summaries.Any(s => s.Errors > 0) ? 1 : 0;
    }
    private static void WriteReport(string input,string report,List<Metric> metrics,List<Summary> summaries,string scopeNote,string resultsPath)
    {
        var text = new List<string> { "## Real documents — T3b-FIX (corrected)", "", "No engine decision. Prior T3b results are superseded. Notes flags describe annotations, not measured handwriting or printed-field subsets. Only verified packet expectations enter scoring; no truth values were changed.", "", "| Engine | Profile | Scored/expected | Notes-clear exact | Notes-flag exact | CER | Invoice pick | Rotated/pages | s/page | Peak MB | Errors |", "|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|" };
        text.Insert(2,scopeNote);
        var cohort=summaries.Where(s=>s.Engine==summaries[0].Engine && s.Profile==summaries[0].Profile).ToArray();
        text.Insert(3,$"Scope: {cohort.Length} packets ({cohort.Count(s=>s.InvoiceEligible)} invoice, {cohort.Count(s=>!s.InvoiceEligible)} non-invoice); {cohort.Sum(s=>s.Pages)} distinct pages, {summaries.Sum(s=>s.Pages)} page/configuration observations. Notes-flagged packets: {cohort.Count(s=>s.Handwritten)}; notes-clear packets: {cohort.Count(s=>!s.Handwritten)}.");
        static string Accuracy(IEnumerable<Metric> rows) { var a=rows.Where(m=>m.Present).ToArray();return a.Length==0?"n/a":(a.Count(m=>m.Exact)/(double)a.Length).ToString("P1",CultureInfo.InvariantCulture); }
        static string Cer(IEnumerable<Metric> rows) { var a=rows.Where(m=>m.Present).ToArray();return a.Length==0?"n/a":((double)a.Sum(m=>m.Edits)/Math.Max(1,a.Sum(m=>m.Characters))).ToString("P2",CultureInfo.InvariantCulture); }
        foreach(var g in summaries.GroupBy(s=>(s.Engine,s.Profile)))
        {
            var m=metrics.Where(m=>(m.Engine,m.Profile)==g.Key).ToArray();var eligible=g.Where(s=>s.InvoiceEligible).ToArray();
            text.Add($"| {g.Key.Engine} | {g.Key.Profile} | {m.Count(m=>m.Present)}/{m.Count(m=>m.Expected)} | {Accuracy(m.Where(m=>!m.Handwritten))} | {Accuracy(m.Where(m=>m.Handwritten))} | {Cer(m)} | {eligible.Count(s=>s.Pick)}/{eligible.Length} | {g.Sum(s=>s.Rotated)}/{g.Sum(s=>s.Pages)} | {g.Sum(s=>s.Seconds)/g.Sum(s=>s.Pages):F3} | {g.Max(s=>s.Peak):F1} | {g.Sum(s=>s.Errors)} |");
        }
        text.AddRange(["", "| Engine | Profile | Notes group | Scope | Field | Expected | Scored | Exact | CER |", "|---|---|---|---|---|---:|---:|---:|---:|"]);
        foreach(var g in metrics.GroupBy(m=>(m.Engine,m.Profile,m.Handwritten,m.Scope,m.Field)))
            text.Add($"| {g.Key.Engine} | {g.Key.Profile} | {(g.Key.Handwritten?"notes_contains_handwritten":"notes_clear")} | {g.Key.Scope} | {g.Key.Field} | {g.Count(m=>m.Expected)} | {g.Count(m=>m.Present)} | {Accuracy(g)} | {Cer(g)} |");
        text.AddRange(["", "| Unscored reason | Field expectations (all six settings) |", "|---|---:|"]);
        foreach(var g in metrics.Where(m=>m.Expected && !m.Present).GroupBy(m=>m.Status))text.Add($"| {g.Key} | {g.Count()} |");
        var resultRows=Csv.Read(resultsPath).Skip(1).ToArray();foreach(var row in resultRows)MetricSchema.Validate(row);
        text.AddRange(["", "Cross-checks are OCR-to-OCR agreement, not verified accuracy. Missing operands or ambiguous document references remain unavailable.", "", "| Engine | Profile | Cross-check | Agree/available | Unavailable |", "|---|---|---|---:|---:|"]);
        foreach(var g in resultRows.Where(r=>r[5] is "invoice_rfp_amount" or "voucher_document_no" or "2307_tin" or "2307_wht").GroupBy(r=>(Engine:r[0],Profile:r[1],Check:r[5])))
            text.Add($"| {g.Key.Engine} | {g.Key.Profile} | {g.Key.Check} | {g.Count(r=>r[6]=="1" && r[7]=="1")}/{g.Count(r=>r[6]=="1")} | {g.Count(r=>r[6]=="0")} |");
        var reviewPath=Path.Combine(input,"inspection-counts.csv");
        if(File.Exists(reviewPath))
        {
            var reviewed=Csv.Read(reviewPath);
            if(!reviewed[0].SequenceEqual(new[]{"category","affected_packets"}))throw new InvalidDataException("Invalid anonymous review schema.");
            text.AddRange(["", "Private inspection: three representative packets (ordinary invoice, notes-flagged source, non-invoice source). Counts below are reviewer-labelled affected packets, overlap across categories, and are not an exhaustive error tally or an engine ranking.", "", "| Failure category | Affected reviewed packets |", "|---|---:|"]);
            foreach(var row in reviewed.Skip(1))
            {
                if(row.Length!=2 || row[0] is not ("recognition_error" or "layout_association_error" or "extraction_error" or "answer_key_source_mismatch" or "source_ambiguity" or "orientation_failure") || !int.TryParse(row[1],out var value) || value<0 || value>3)throw new InvalidDataException("Invalid anonymous review count.");
                text.Add($"| {row[0]} | {value} |");
            }
        }
        text.AddRange(["", "Page-local fields use owner-annotated primary pages unless private source review marks them ambiguous/mismatched. RFP/voucher/WHT/net-payment fields require independent, hash-bound field-source verification; absent, ambiguous, stale or mismatched expectations remain unscored. Field accuracy never verifies its own answer key. Identifiers retain letters, separators and leading zeros. Gross/net amounts stay distinct; no first/largest selection or gross/net fallback is used. Voucher cross-checks compare the invoice identifier to document reference, never voucher number. Non-invoice primary sources are excluded from invoice-page selection denominators and invoice identifier/type scoring. All pages in retained complete packets are processed. Cardinal corrections are inferred; full rotation correctness remains unverified. The private review observed one residual orientation failure; this is not a measured study-wide rotation success rate. Time includes renderer, orientation probes, full parent bitmap decode/rotation/re-encoding, preprocessing and OCR/IPC; engine initialization is excluded. Peak is sampled active OCR-job memory, excluding parent/renderer. CER/accuracy apply only to scorable expectations; no private text/values/names are persisted in metrics. No engine/model/UI/Excel changes; no T4."]);
        var existing=File.Exists(report)?File.ReadAllText(report):"# OCR bake-off\n";
        var correctedMarker=existing.IndexOf("## Real documents — T3b-FIX (corrected)",StringComparison.Ordinal);if(correctedMarker>=0)existing=existing[..correctedMarker];
        existing=existing.Replace("## Real documents\n","## Real documents — T3b (superseded)\n\n**Superseded: incorrect page-local packet-reference scoring, digits-only identifiers, whole-line amount extraction and incomplete timing. Do not use these tables to interpret engine quality.**\n");
        Directory.CreateDirectory(Path.GetDirectoryName(report)!);File.WriteAllText(report,existing.TrimEnd()+"\n\n"+string.Join('\n',text)+"\n");
    }
    public static int Summarize(string input,string run,string output,string report,int maxPacket)
    {
        input=Configuration.Local(input);run=Configuration.Local(run);output=Configuration.Local(output);report=Configuration.Local(report);
        if(maxPacket<1 || Directory.Exists(output) || output.StartsWith(input.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !report.EndsWith(".md",StringComparison.OrdinalIgnoreCase))throw new IOException("Use a new metrics output outside the input tree and a Markdown report.");
        var all=Csv.Read(Path.Combine(run,"results.csv"));
        var header="engine,preprocess,packet,page,notes_contains_handwritten,metric,applicable,exact,edits,reference_characters,label,predicted_class,truth_class,rotation_degrees,seconds,peak_mb,error,scope,source_status,expected_present".Split(',');
        if(all.Length<2 || !all[0].SequenceEqual(header))throw new InvalidDataException("Only corrected metrics schema can be summarized.");
        var rows=all.Skip(1).ToArray();foreach(var row in rows)MetricSchema.Validate(row);
        var selected=rows.Where(r=>int.Parse(r[2],CultureInfo.InvariantCulture)<=maxPacket).ToArray();
        var metrics=new List<Metric>();var summaries=new List<Summary>();
        int I(string[] row,int n)=>int.Parse(row[n],CultureInfo.InvariantCulture);
        double D(string[] row,int n)=>double.Parse(row[n],CultureInfo.InvariantCulture);
        foreach(var g in selected.GroupBy(r=>(Engine:r[0],Profile:r[1],Packet:I(r,2))))
        {
            var pages=g.Where(r=>r[5]=="page").ToArray();var picks=g.Where(r=>r[5]=="invoice_page_pick").ToArray();
            if(picks.Length!=1 || pages.Select(r=>I(r,3)).Distinct().Count()!=pages.Length || pages.Length==0 || pages.Length!=pages.Max(r=>I(r,3)) || g.Where(r=>PacketRules.Fields.Contains(r[5])).Select(r=>r[5]).Distinct().Count()!=PacketRules.Fields.Length)throw new InvalidDataException("Requested subset contains an incomplete configuration.");
            var pick=picks[0];summaries.Add(new(g.Key.Engine,g.Key.Profile,g.Key.Packet,I(pick,4)==1,I(pick,7)==1,pages.Length,pages.Count(r=>I(r,13)!=0),pages.Count(r=>I(r,16)!=0),pages.Sum(r=>D(r,14)),pages.Max(r=>D(r,15)),I(pick,6)==1));
        }
        if(summaries.Select(s=>s.Packet).Distinct().Count()!=maxPacket || summaries.GroupBy(s=>s.Packet).Any(g=>g.Count()!=6))throw new InvalidDataException("The requested complete packet boundary is unavailable.");
        foreach(var r in selected.Where(r=>PacketRules.Fields.Contains(r[5])))metrics.Add(new(r[0],r[1],I(r,2),I(r,3),I(r,4)==1,r[5],I(r,6)==1,I(r,7)==1,I(r,8),I(r,9),r[10],I(r,19)==1,r[18],r[17]));
        Directory.CreateDirectory(output);using(var writer=new StreamWriter(Path.Combine(output,"results.csv"))){writer.WriteLine(Csv.Line(header));foreach(var row in selected)writer.WriteLine(Csv.Line(row));}
        File.Copy(Path.Combine(run,"machine.json"),Path.Combine(output,"machine.json"));
        WriteReport(input,report,metrics,summaries,$"Owner-requested boundary: packets 1–{maxPacket}. Later partial observations are retained in the original local run but excluded here. Packets beyond this boundary are not part of the corrected accepted study.",Path.Combine(output,"results.csv"));
        return summaries.Any(s=>s.Errors>0)?1:0;
    }

}
