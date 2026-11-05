using System.Text.RegularExpressions;
namespace TsekSheet.OcrEval;
public static class MetricSchema
{
    public static void Validate(string[] row)
    {
        if(row.Length!=20)throw new InvalidDataException("Invalid metrics width.");
        var categories=new Dictionary<int,string[]>{
            [0]=["tesseract-psm6","paddle-v5-mobile"], [1]=Imaging.Profiles,
            [5]=[..PacketRules.Fields,"page","classification","invoice_page_pick","invoice_rfp_amount","voucher_document_no","2307_tin","2307_wht"],
            [10]=["","vatable_sales","vat_amount","withholding_tax","total","total_amount_due","amount_due","net_amount_due","ambiguous"],
            [11]=["","sales_invoice","invoice_official_receipt","check_voucher","rfp","order_form","bir_2307","bank_proof","other","policy_schedule","payment_instruction","collection_receipt"],
            [17]=["","page_local","packet_reference","invoice_packet","non_invoice_packet","gross_to_gross","report_only"],
            [18]=["","verified","annotated_page","unverified_source","source_ambiguity","answer_key_mismatch","stale_source","non_invoice_field","source_page_missing","no_expectation"]};
        categories[12]=categories[11];
        for(var i=0;i<row.Length;i++)
            if(categories.TryGetValue(i,out var allowed)?!allowed.Contains(row[i]):!Regex.IsMatch(row[i],@"^\d+(\.\d+)?$"))throw new InvalidDataException("Metrics contain a non-metric value.");
    }
}
