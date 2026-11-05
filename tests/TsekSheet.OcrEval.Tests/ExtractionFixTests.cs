using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.OcrEval;
namespace TsekSheet.OcrEval.Tests;
[TestClass]
public sealed class ExtractionFixTests
{
    private static WordBox W(string text,double x,double y,double width=100)=>new(text,1,x,y,width,20);
    [TestMethod]
    public void MultiColumnHeadersAssociateByCoordinatesNotMagnitude()
    {
        var result=new OcrResult("",[W("Gross Total",0,0),W("VAT Amount",400,0),W("Net Amount Due",800,0),W("120.00",0,30),W("20.00",400,30),W("117.00",800,30)]);
        var f=PacketRules.Extract(result,"111-222-333-000",out _);
        Assert.AreEqual("120.00",f["total"]);Assert.AreEqual("20.00",f["vat"]);Assert.AreEqual("117.00",f["amount_due"]);
    }
    [TestMethod]
    public void AmountBeforeLabelAndGrossNetStaySeparate()
    {
        var f=PacketRules.Extract("159.25 TOTAL\n151.50 Net Amount Due","111-222-333-000",out var labels);
        Assert.AreEqual("159.25",f["total"]);Assert.AreEqual("151.50",f["amount_due"]);Assert.AreEqual("net_amount_due",labels["amount_due"]);
    }
    [TestMethod]
    public void ConflictingTotalsAreAmbiguousNotFirstOrLargest()
    {
        var f=PacketRules.Extract("TOTAL 175.25\nGRAND TOTAL 164.50","111-222-333-000",out var labels);
        Assert.AreEqual("",f["total"]);Assert.AreEqual("ambiguous",labels["total"]);
    }
    [TestMethod]
    public void ExemptAndZeroRatedSalesAreBoundariesNotVatAmounts()
    {
        var f=PacketRules.Extract(new OcrResult("",[W("Withholding Tax",0,0,160),W("2.00",200,0),W("Zero Rated Sales",500,0,180),W("0.00",800,0),W("VAT Exempt Sales",500,30,180),W("0.00",800,30),W("VAT Amount",500,60),W("12.00",800,60)]),"111-222-333-000",out _);
        Assert.AreEqual("2.00",f["wht"]);Assert.AreEqual("12.00",f["vat"]);
    }
    [TestMethod]
    public void MultipleAmountsWithoutColumnMeaningRemainAmbiguous()
    {
        var f=PacketRules.Extract(new OcrResult("",[W("TOTAL",0,0),W("0.00",300,0),W("999.99",600,0)]),"111-222-333-000",out var labels);
        Assert.AreEqual("",f["total"]);Assert.AreEqual("ambiguous",labels["total"]);
    }
    [TestMethod]
    public void WithholdingColumnDoesNotTakeOtherColumns()
    {
        var f=PacketRules.Extract(new OcrResult("",[W("Total",300,0),W("Tax Withheld",800,0),W("for the Quarter",800,25),W("200.00",0,60),W("200.00",300,60),W("4.00",800,60)]),"111-222-333-000",out _);
        Assert.AreEqual("4.00",f["wht"]);Assert.AreEqual("",f["total"]);
    }
    [TestMethod]
    public void VoucherNumberIsNotTheMatchingInvoiceReference()
    {
        var f=PacketRules.Extract("INV# AB-00042\nVoucher No. VN-00991\nDocument No. AB-00042","111-222-333-000",out _);
        Assert.AreEqual("VN-00991",f["voucher_no"]);Assert.AreEqual("AB-00042",f["document_reference"]);
        Assert.AreEqual(PacketRules.Canonical("invoice",f["invoice"]),PacketRules.Canonical("document_reference",f["document_reference"]));
        Assert.AreNotEqual(PacketRules.Canonical("invoice","AB-00042"),PacketRules.Canonical("invoice","CD-00042"));
        Assert.AreNotEqual(PacketRules.Canonical("invoice","AB-00042"),PacketRules.Canonical("invoice","AB-42"));
    }
    [TestMethod]
    public void UnverifiedPacketExpectationCannotVerifyItself()
    {
        Assert.IsFalse(FieldSources.Resolve("wht",4,"sales_invoice").Scorable);
        Assert.IsFalse(FieldSources.Resolve("amount_due",4,"sales_invoice").Scorable);
        var verified=FieldSources.Resolve("rfp_no",4,"sales_invoice",new(2,"verified","packet_reference"));
        Assert.AreEqual(2,verified.Page);Assert.IsTrue(verified.Scorable);
        Assert.IsFalse(FieldSources.Resolve("voucher_no",4,"sales_invoice",new(1,"answer_key_mismatch","packet_reference")).Scorable);
        Assert.IsFalse(FieldSources.Resolve("invoice",5,"policy_schedule").Scorable);
    }
}
