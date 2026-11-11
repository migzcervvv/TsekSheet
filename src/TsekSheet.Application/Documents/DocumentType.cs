using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Documents;

public sealed record DocumentTypeReference(string Id, int Revision);
public sealed record DocumentField(string Key, string Label, string ValueType, bool Required, int MaxLength);

public sealed record DocumentType(DocumentTypeReference Identity, IReadOnlyList<DocumentField> Fields,
    IReadOnlyList<string> PageRoles, bool OneRecordPerPacket, string? AdvisoryIdentifier = null);

public static class DocumentTypes
{
    public static DocumentType Rfp { get; } = new(new("rfp", 1), Array.AsReadOnly<DocumentField>([
        new("dateReceived", "Date Received", "text", false, 256),
        new("payee", "Payee", "text", true, 256),
        new("particulars", "Particulars", "text", false, 256),
        new("rfpNo", "RFP No.", "text", true, 256),
        new("invoice", "Invoice #", "text", false, 256),
        new("amount", "Amount", "money", true, 256),
        new("otherCharges", "Other Charges", "money", false, 256),
        new("vat", "VAT", "money", false, 256),
        new("totalAmount", "Total Amount", "money", false, 256),
        new("ewt", "EWT", "money", false, 256),
        new("overpayment", "Overpayment", "money", false, 256),
        new("netAmount", "Net Amount", "money", false, 256),
        new("modeOfPayment", "Mode of Payment", "text", false, 256),
        new("cvNo", "CV#", "text", false, 256)
    ]), Array.AsReadOnly(["rfp-form", "invoice", "supporting-document"]), true, "rfpNo");

    public static DocumentType Exti { get; } = new(new("exti", 1), Array.AsReadOnly<DocumentField>([
        new("itemNo", "Item no.", "text", true, 256),
        new("particulars", "Particulars", "text", true, 256),
        new("vendor", "Vendor", "text", false, 256),
        new("tin", "TIN", "text", false, 256),
        new("address", "Address", "text", false, 256),
        new("invoice", "Invoice (SI#/OR#)", "text", false, 256),
        new("cvNo", "CV#", "text", false, 256),
        new("account", "Account", "accountChoice", true, 256),
        new("amount", "Amount", "money", true, 256)
    ]), Array.AsReadOnly(["liquidation-document"]), false);

    internal static DocumentType? ForProfile(WorkflowProfile profile) => profile.Id switch
    {
        "payables-rfp" => Rfp,
        _ when profile.Layout is { MappingMode: "account" } => ExtiForProfile(profile),
        _ => null
    };

    internal static WorkflowField[] ReviewFields(WorkflowProfile profile) => ForProfile(profile)?.Fields
        .Select(f => new WorkflowField(f.Key, f.Label, f.ValueType, f.Required, f.MaxLength)).ToArray() ?? profile.Fields.ToArray();

    private static DocumentType ExtiForProfile(WorkflowProfile profile)
    {
        var fields = profile.Fields.Select(f => new DocumentField(f.Key, f.Header, f.Kind, f.Required, f.MaxLength)).ToArray();
        // Existing installations can use their own field keys and labels. Project those
        // verification requirements while the local account layout remains legacy.
        var oneRecordPerPacket = profile.Layout!.SourceScope == "document";
        return Exti.Fields.SequenceEqual(fields) && !oneRecordPerPacket ? Exti : Exti with
        {
            Identity = new(profile.Id, profile.Revision), Fields = Array.AsReadOnly(fields), OneRecordPerPacket = oneRecordPerPacket
        };
    }
}
