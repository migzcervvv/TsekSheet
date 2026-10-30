using ClosedXML.Excel;
using System.IO;

namespace TsekSheet.TestFixtures;

public sealed class SectionReviewFixture : IDisposable
{
    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-section-review-" + Guid.NewGuid().ToString("N"));
    public string Path { get; }
    public static TsekSheet.Application.Workflows.ReadOnlySectionProfile Profile => new("artificial-review", 1, 1, 32, [1, 3, 4, 5, 6],
        [new("vendor", "Entity"),new("rfp","Request"),new("invoice","Invoice"),new("amount","Amount")],"vendor",["rfp","invoice"],["amount"],
        [new("north","North",null,new("North ledger",[1]),new("contiguous-sum",FormulaField:"amount")),
         new("tools","Tools","north",new("Tools",[1]),new("explicit-row",ExplicitRow:8))], [], []);
    public static string ProfileJson => System.Text.Json.JsonSerializer.Serialize(Profile,new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
    public SectionReviewFixture()
    {
        Directory.CreateDirectory(folder);Path=System.IO.Path.Combine(folder,"artificial-review.xlsx");
        using var wb=new XLWorkbook();var s=wb.AddWorksheet("Artificial review");
        foreach(var r in new[]{1,9}){s.Cell(r,3).Value=r==9?"Payee":"Entity";s.Cell(r,4).Value="Request";s.Cell(r,5).Value="Invoice";s.Cell(r,6).Value="Amount";}
        s.Cell(2,1).Value="North ledger";s.Cell(3,1).Value="Tools";
        void Record(int r,string vendor,string id){s.Cell(r,3).Value=vendor;s.Cell(r,4).Value=id;s.Cell(r,6).Value=10;}
        Record(4,"Artificial Alpha Ltd","SYN-A");Record(5,"","SYN-B");Record(6,"Artificial Alpha Ltd","SYN-A");Record(11,"Artificial Alpha, Ltd","SYN-C");
        s.Row(5).Hide();s.Cell(8,6).FormulaA1="SUM(F3:F7)";s.Cell(20,6).FormulaA1="SUM(F3:F19)";s.Cell(31,6).FormulaA1="F20";s.Cell(32,1).Value="Artificial footer";
        wb.AddWorksheet("Artificial context").Cell(1,1).Value="Context";
        using var stream=new FileStream(Path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);wb.SaveAs(stream,new SaveOptions{EvaluateFormulasBeforeSaving=false,GenerateCalculationChain=false});
    }
    public void Dispose(){File.Delete(Path);Directory.Delete(folder);}
}

// Artificial data only. CreateNew prevents accidental overwrite even in a test race.
public sealed class PayablesFixture : IDisposable
{
    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-payables-" + Guid.NewGuid().ToString("N"));
    public string Path { get; }
    public PayablesFixture(int externalHeading = 15, string scenario = "supported")
    {
        Directory.CreateDirectory(folder); Path = System.IO.Path.Combine(folder, "payables-synthetic.xlsx");
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Synthetic Ledger");
        sheet.Range("A1:F1").Merge().FirstCell().Value = "Artificial payables — no real accounting data";
        var headers = new[] { "Vendor name", "Particulars", "RFP number", "Invoice number", "Amount", "Record date" };
        for (var c = 0; c < headers.Length; c++) sheet.Cell(2,c+1).Value = headers[c];
        sheet.Row(2).Style.Font.Bold = true;
        sheet.Range("A5:F5").Merge().FirstCell().Value = "2025 PAYABLES (INTERNAL)";
        sheet.Range(externalHeading,1,externalHeading,6).Merge().FirstCell().Value = "2025 PAYABLES (EXTERNAL)";
        Add(6,"Acme Trading Incorporated","R1","I1"); Add(7,"Other Tools Ltd","R2","I2"); Add(8,"Acme Trading Incorporated","R3","I3");
        Add(externalHeading+1,"Cross Vendor Ltd","X1","XI1"); Add(externalHeading+2,"Acme Trading Incorporated","R4","I4"); Add(externalHeading+4,"Cross Vendor Ltd","X2","XI2");
        sheet.Cell(9,1).Value = "Note: synthetic supporting documents";
        sheet.Cell(10,1).Value = "INTERNAL SUBTOTAL"; sheet.Cell(10,5).FormulaA1 = "SUM(E6:E9)";
        sheet.Cell(externalHeading+7,1).Value = "EXTERNAL SUBTOTAL";
        sheet.Cell(externalHeading+7,5).FormulaA1 = $"SUM(E{externalHeading+1}:E{externalHeading+6})";
        sheet.Cell(externalHeading+10,1).Value = "Summary"; sheet.Cell(externalHeading+10,5).FormulaA1 = $"E10+E{externalHeading+7}";
        sheet.Range(externalHeading+12,1,externalHeading+12,6).Merge().FirstCell().Value = "Synthetic lower merged footer";
        sheet.Cell(externalHeading+13,10).FormulaA1 = $"'Synthetic Ledger'!$E${externalHeading+7}+1";
        sheet.Cell(2,8).Value = "Metadata key"; sheet.Cell(2,9).Value = "Metadata value";
        sheet.Cell(3,8).Value = "Classification"; sheet.Cell(3,9).Value = "artificial-only";
        sheet.Cell(4,8).Value = "Unrelated"; sheet.Cell(4,9).Value = 42;
        sheet.Range("H2:I4").CreateTable("SyntheticMetadata");
        workbook.DefinedNames.Add("InternalAmounts",sheet.Range("E6:E9"));
        workbook.DefinedNames.Add("ExternalAmounts",sheet.Range(externalHeading+1,5,externalHeading+6,5));
        var overview = workbook.AddWorksheet("Overview"); overview.Cell(1,1).FormulaA1 = $"'Synthetic Ledger'!E{externalHeading+7}";
        var hidden = workbook.AddWorksheet("Context"); hidden.Cell(1,1).Value = "Artificial context"; hidden.Visibility = XLWorksheetVisibility.Hidden;
        switch (scenario)
        {
            case "missing-subtotal": sheet.Cell(externalHeading+7,1).Clear(XLClearOptions.Contents); break;
            case "duplicate-subtotal": sheet.Cell(externalHeading+6,1).Value = "EXTERNAL SUBTOTAL"; break;
            case "outside-record": sheet.Cell(externalHeading+9,1).Value = "Outside Company"; sheet.Cell(externalHeading+9,3).Value = "OUTSIDE"; break;
        }
        using var output = new FileStream(Path,FileMode.CreateNew,FileAccess.ReadWrite,FileShare.None);
        workbook.SaveAs(output,new SaveOptions { GenerateCalculationChain = scenario == "calculation-chain", EvaluateFormulasBeforeSaving = false });
        void Add(int row,string vendor,string rfp,string invoice)
        {
            sheet.Cell(row,1).Value=vendor; sheet.Cell(row,2).Value="Artificial description"; sheet.Cell(row,3).Value=rfp; sheet.Cell(row,4).Value=invoice;
            sheet.Cell(row,5).Value=100; sheet.Cell(row,5).Style.NumberFormat.Format="#,##0.00";
            sheet.Cell(row,6).Value=new DateTime(2024,1,2); sheet.Cell(row,6).Style.NumberFormat.Format="yyyy-mm-dd";
            sheet.Cell(row,5).Style.Fill.BackgroundColor=XLColor.LightBlue;
        }
    }
    public void Dispose() { File.Delete(Path); Directory.Delete(folder); }
}
