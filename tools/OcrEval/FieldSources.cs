namespace TsekSheet.OcrEval;

public sealed record FieldSource(int Page,string Status,string Scope)
{
    public bool Scorable => Status is "verified" or "annotated_page";
}
public static class FieldSources
{
    public static readonly string[] PacketFields=["rfp_no","voucher_no","wht","amount_due"];
    public static bool IsInvoice(string type)=>type is "sales_invoice" or "invoice_official_receipt";
    public static FieldSource Resolve(string field,int primary,string type,FieldSource? reviewed=null)
    {
        if(reviewed is not null)return reviewed;
        var scope=PacketFields.Contains(field)?"packet_reference":"page_local";
        if(PacketFields.Contains(field))return new(0,"unverified_source",scope);
        if(!IsInvoice(type) && field is "invoice" or "invoice_type")return new(primary,"non_invoice_field",scope);
        return new(primary,"annotated_page",scope);
    }
    public static Dictionary<(string File,string Field),FieldSource> Load(string input)
    {
        var path=Path.Combine(input,"field-sources.csv");var result=new Dictionary<(string,string),FieldSource>();if(!File.Exists(path))return result;
        var rows=Csv.Read(path);
        if(!rows[0].SequenceEqual(new[]{"file","field","page","status","sha256"}))throw new InvalidDataException("Invalid private field-source schema.");
        foreach(var row in rows.Skip(1))
        {
            if(row.Length!=5 || !PacketRules.Fields.Append("document_reference").Contains(row[1]) || !int.TryParse(row[2],out var page) || page<1 || row[3] is not ("verified" or "source_ambiguity" or "answer_key_mismatch" or "unverified_source"))throw new InvalidDataException("Invalid private field-source record.");
            var status=Configuration.Hash(Csv.InputPath(input,row[0]))==row[4]?row[3]:"stale_source";
            if(!result.TryAdd((row[0],row[1]),new(page,status,PacketFields.Contains(row[1])?"packet_reference":"page_local")))throw new InvalidDataException("Duplicate field-source record.");
        }
        return result;
    }
}
