using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.OcrEval;
namespace TsekSheet.OcrEval.Tests;
[TestClass]
public sealed class PacketTests
{
    [TestMethod]
    [DataRow("SALES INVOICE", "sales_invoice")]
    [DataRow("OFFICIAL RECEIPT", "invoice_official_receipt")]
    [DataRow("CHECK VOUCHER", "check_voucher")]
    [DataRow("REQUEST FOR PAYMENT", "rfp")]
    [DataRow("PURCHASE ORDER", "order_form")]
    [DataRow("BIR 2307", "bir_2307")]
    [DataRow("FUND TRANSFER", "bank_proof")]
    [DataRow("Unclassified", "other")]
    public void Classification(string text,string expected) => Assert.AreEqual(expected,PacketRules.Classify(text));
    [TestMethod]
    public void AmountsKeepLabelsAndExcludeOwnTin()
    {
        var f = PacketRules.Extract("DEMO\nTIN 111-222-333-00000\nTIN 123-456-789-000\nSI# X-123\nVATable sales 100.00\nVAT 12.00\nTOTAL 112.00\nWHT 2.00\nTotal Amount Due 110.00", "111-222-333-000",out var labels);
        Assert.AreEqual("123-456-789-000",f["tin"]); Assert.AreEqual("100.00",f["vatable"]); Assert.AreEqual("12.00",f["vat"]); Assert.AreEqual("112.00",f["total"]); Assert.AreEqual("2.00",f["wht"]); Assert.AreEqual("110.00",f["amount_due"]); Assert.AreEqual("total_amount_due",labels["amount_due"]);
        Assert.AreEqual("X-123",PacketRules.Canonical("invoice",f["invoice"]));
        Assert.AreEqual(PacketRules.Canonical("invoice_type","SI"),PacketRules.Canonical("invoice_type","Sales Invoice"));
    }
    [TestMethod]
    public void ConflictingDueLabelsAreNotMerged()
    {
        var f = PacketRules.Extract("Amount Due 110.00\nTotal Amount Due 110.00","111-222-333-000",out var labels);
        Assert.AreEqual("",f["amount_due"]); Assert.AreEqual("ambiguous",labels["amount_due"]);
    }
    [TestMethod]
    public void OrientationProbeUsesTextAndConfidenceWithoutTruth()
    {
        Assert.IsTrue(PacketRules.OrientationScore(new("TOTAL INVOICE",[new("TOTAL",.9,0,0,10,10)])) > PacketRules.OrientationScore(new("xxx",[new("xxx",.2,0,0,10,10)])));
        using var image = new OpenCvSharp.Mat(20,30,OpenCvSharp.MatType.CV_8UC3);
        using var rotated = PacketRules.Rotate(image,90); Assert.AreEqual(30,rotated.Height); Assert.AreEqual(20,rotated.Width);
    }
}
