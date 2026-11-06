using System.Globalization;
using System.Text.RegularExpressions;
using OpenCvSharp;

namespace TsekSheet.OcrEval;

public static class PacketRules
{
    public static readonly string[] Fields = ["vendor", "tin", "invoice_type", "invoice", "date", "vatable", "vat", "total", "wht", "amount_due", "rfp_no", "voucher_no"];
    public static Mat Rotate(Mat source, int degrees)
    {
        var result = source.Clone();
        if (degrees != 0) Cv2.Rotate(source, result, degrees switch { 90 => RotateFlags.Rotate90Clockwise, 180 => RotateFlags.Rotate180, 270 => RotateFlags.Rotate90Counterclockwise, _ => throw new ArgumentException() });
        return result;
    }
    public static double OrientationScore(OcrResult result) => result.Words.Sum(w => w.Confidence * Math.Min(w.Text.Length, 20)) +
        20 * Regex.Matches(result.Text, @"\b(INVOICE|RECEIPT|TOTAL|DATE|VAT|TIN|PAYMENT|VOUCHER|REQUEST|AMOUNT|TAX|SALES|ORDER|BANK)\b", RegexOptions.IgnoreCase).Count;
    public static string Classify(string text)
    {
        var t = Metrics.Normalize(text);
        if (Regex.IsMatch(t, @"\b2307\b|CERTIFICATE OF CREDITABLE TAX")) return "bir_2307";
        if (t.Contains("REQUEST FOR PAYMENT") || Regex.IsMatch(t, @"\bRFP\b")) return "rfp";
        if (t.Contains("CHECK VOUCHER") || t.Contains("CHEQUE VOUCHER") || t.Contains("DISBURSEMENT VOUCHER")) return "check_voucher";
        if (t.Contains("ORDER FORM") || t.Contains("PURCHASE ORDER")) return "order_form";
        if (t.Contains("POLICY SCHEDULE")) return "policy_schedule";
        if (t.Contains("PAYMENT INSTRUCTION")) return "payment_instruction";
        if (t.Contains("COLLECTION RECEIPT")) return "collection_receipt";
        if (t.Contains("TRANSACTION SUCCESSFUL") || t.Contains("FUND TRANSFER") || t.Contains("DEPOSIT SLIP") || t.Contains("BANK TRANSFER")) return "bank_proof";
        if (t.Contains("SALES INVOICE") || t.Contains("SERVICE INVOICE")) return "sales_invoice";
        if (t.Contains("OFFICIAL RECEIPT") || Regex.IsMatch(t, @"\bINVOICE\b")) return "invoice_official_receipt";
        return "other";
    }
    public static string TruthType(string type) => type.Trim().ToLowerInvariant() switch
    {
        "service_invoice" => "sales_invoice", "invoice" => "invoice_official_receipt",
        "sales_invoice" or "invoice_official_receipt" or "check_voucher" or "rfp" or "order_form" or "bir_2307" or "bank_proof" or "collection_receipt" or "payment_instruction" or "policy_schedule" => type.Trim().ToLowerInvariant(), _ => "other"
    };
    public static string Canonical(string field, string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Equals("N/A", StringComparison.OrdinalIgnoreCase)) return "";
        if (field == "tin") return Regex.Replace(value, @"\D", "");
        if (field is "invoice" or "document_reference" or "rfp_no" or "voucher_no") return Regex.Replace(Metrics.Normalize(value), @"\s+", "");
        if (field == "invoice_type") { var v = Regex.Replace(Metrics.Normalize(value), @"[^A-Z0-9]", ""); return v switch { "SI" => "SALESINVOICE", "OR" => "OFFICIALRECEIPT", "INV" => "INVOICE", _ => v }; }
        if (field == "date") return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date.ToString("yyyy-MM-dd") : Metrics.Normalize(value);
        if (new[] { "vatable", "vat", "total", "wht", "amount_due" }.Contains(field)) return Extraction.NormalizeAmount(Regex.Replace(value, @"PHP|₱|\s", "", RegexOptions.IgnoreCase));
        return Metrics.Normalize(value);
    }
    public static Dictionary<string, string> Extract(string text, string ownTin, out Dictionary<string, string> labels)
        => Extract(new OcrResult(text,[]),ownTin,out labels);
    public static Dictionary<string,string> Extract(OcrResult result,string ownTin,out Dictionary<string,string> labels)
    {
        var text=result.Text;
        var basic = Extraction.Extract(text, []);
        var fields = Fields.ToDictionary(f => f, _ => ""); labels = new();
        fields["vendor"] = basic.Vendor; fields["date"] = basic.Date; fields["invoice"] = basic.Invoice;
        var tins = Regex.Matches(text, @"\b\d{3}[ -]*\d{3}[ -]*\d{3}(?:[ -]*\d{3,5})?\b").Select(m => m.Value);
        var own = Canonical("tin", ownTin);
        fields["tin"] = tins.FirstOrDefault(t => own.Length < 9 || !Canonical("tin", t).StartsWith(own[..9], StringComparison.Ordinal)) ?? "";
        var kind = Regex.Match(text, @"\b(SALES INVOICE|SERVICE INVOICE|OFFICIAL RECEIPT|COLLECTION RECEIPT|SI|OR|INV(?:OICE)?)\b", RegexOptions.IgnoreCase);
        fields["invoice_type"] = kind.Value;
        if (fields["invoice"].Length == 0) fields["invoice"] = Regex.Match(text, @"(?:INVOICE|RECEIPT)\s*(?:NO\.?|#)\s*[:.]?\s*([A-Z0-9/-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
        fields["rfp_no"] = Regex.Match(text, @"\bRFP\s*(?:NO\.?|#)?\s*[:.-]?\s*([A-Z0-9/-]*\d[A-Z0-9/-]*)", RegexOptions.IgnoreCase).Groups[1].Value;
        fields["voucher_no"] = Regex.Match(text, @"\b(?:VOUCHER|CV)\s*(?:NO\.?|NUMBER|#)\s*[:.-]?\s*([A-Z0-9/-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
        fields["document_reference"] = Regex.Match(text, @"\b(?:DOCUMENT|DOC\.?|INVOICE)\s*(?:NO\.?|NUMBER|REF(?:ERENCE)?\.?|#)\s*[:.-]?\s*([A-Z0-9/-]+)", RegexOptions.IgnoreCase).Groups[1].Value;
        SpatialAmounts.Apply(result,fields,labels);
        return fields;
    }
}
