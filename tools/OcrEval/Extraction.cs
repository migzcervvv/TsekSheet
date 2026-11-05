using System.Globalization;
using System.Text.RegularExpressions;

namespace TsekSheet.OcrEval;

public static class Extraction
{
    private static readonly Regex Tin = new(@"\b(\d{3})[ \t-]*(\d{3})[ \t-]*(\d{3})(?:[ \t-]*(\d{3,5}))?\b", RegexOptions.Compiled);
    private static readonly Regex Invoice = new(@"^\s*(?:SI|OR|INV(?:OICE)?)\b(?:\s*(?:NO\.?|NUMBER))?\s*#?\s*[:.-]?\s*([A-Z0-9]+(?:\s*[-/‐‑–—]\s*[A-Z0-9]+)*)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Total = new(@"\b(?:GRAND\s+TOTAL|TOTAL(?:\s+AMOUNT)?|AMOUNT\s+DUE)\s*[:=]?\s*(?:PHP|P|₱)?\s*(-?\d[\d,]*\.\d{2})\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly string[] Formats = ["yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy", "M/d/yyyy", "MM-dd-yyyy", "dd MMM yyyy", "d MMM yyyy", "MMM dd, yyyy", "MMM d, yyyy", "MMMM d, yyyy", "dd-MMM-yyyy", "d-MMM-yyyy"];
    public static string NormalizeTin(string text)
    {
        var match = Tin.Match(text);
        return match.Success ? string.Join("-", match.Groups.Cast<Group>().Skip(1).Where(g => g.Success).Select(g => g.Value)) : "";
    }
    public static string NormalizeAmount(string text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) ? amount.ToString("0.00", CultureInfo.InvariantCulture) : "";
    public static string NormalizeDate(string text) => DateTime.TryParseExact(text.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var date) ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
    public static ReceiptFields Extract(string text, IReadOnlyList<Vendor> vendors)
    {
        var lines = text.Split('\n').Select(l => Regex.Replace(l.Trim(), @"\s+", " ")).Where(l => l.Length > 0).ToArray();
        var tin = NormalizeTin(text);
        var invoice = lines.Select(l => Invoice.Match(l)).FirstOrDefault(m => m.Success)?.Groups[1].Value.ToUpperInvariant() ?? "";
        invoice = Regex.Replace(Regex.Replace(invoice, @"[‐‑–—]", "-"), @"\s+", "");
        var date = "";
        foreach (var line in lines.OrderByDescending(l => l.Contains("DATE", StringComparison.OrdinalIgnoreCase)))
        {
            var candidate = Regex.Replace(line, @"^.*?\bDATE\s*:?\s*", "", RegexOptions.IgnoreCase);
            foreach (Match match in Regex.Matches(candidate, @"\d{4}[-/]\d{1,2}[-/]\d{1,2}|\d{1,2}[-/]\d{1,2}[-/]\d{4}|\d{1,2}[- ]+[A-Za-z]{3,9}[- ]+\d{4}|[A-Za-z]{3,9}\s+\d{1,2},\s*\d{4}"))
                if ((date = NormalizeDate(match.Value)) != "") break;
            if (date != "") break;
        }
        var totals = lines.SelectMany(l => Total.Matches(l).Cast<Match>()).Select(m => NormalizeAmount(m.Groups[1].Value)).Distinct().ToArray();
        var vendor = "";
        var tinMatches = vendors.Where(v => tin != "" && NormalizeTin(v.Tin) == tin).ToArray();
        if (tinMatches.Length == 1) vendor = tinMatches[0].Name;
        else if (vendors.Count > 0 && lines.Length > 0)
        {
            var scores = vendors.Select(v => (v.Name, Score: lines.Take(5).Select(l =>
            {
                var name = Metrics.Normalize(v.Name); var observed = Metrics.Normalize(l);
                return 1d - (double)Metrics.Distance(name, observed) / Math.Max(name.Length, observed.Length);
            }).Max())).OrderByDescending(v => v.Score).ToArray();
            if (scores[0].Score >= .65 && (scores.Length == 1 || scores[0].Score > scores[1].Score)) vendor = scores[0].Name;
        }
        return new(vendor == "" ? lines.FirstOrDefault() ?? "" : vendor, tin, invoice, date, totals.Length == 1 ? totals[0] : "");
    }
}
