using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.OcrEval;

namespace TsekSheet.OcrEval.Tests;

[TestClass]
public sealed class RulesTests
{
    [TestMethod]
    [DataRow("kitten", "sitting", 3, .5)]
    [DataRow("ABC", "ABC", 0, 0d)]
    [DataRow("ABC", "", 3, 1d)]
    [DataRow("", "", 0, 0d)]
    [DataRow("", "AB", 2, 2d)]
    [DataRow("Ñ₱𐐀", "Ñ₱A", 1, 1d / 3)]
    [DataRow("AB", "ABCDEF", 4, 2d)]
    public void CharacterErrorRateCountsSubstitutionsInsertionsDeletionsAndUnicode(string reference, string actual, int edits, double cer)
    {
        Assert.AreEqual(edits, Metrics.Distance(reference, actual));
        Assert.AreEqual(cer, Metrics.Cer(reference, actual), 1e-9);
    }
    [TestMethod]
    [DataRow("SI# AB-001", "AB-001")]
    [DataRow("OR # 123/45", "123/45")]
    [DataRow("INV# X001", "X001")]
    [DataRow("INVOICE NO. A123", "A123")]
    [DataRow("SI# AB - 001", "AB-001")]
    public void PhilippineReceiptFieldsUseTotalRatherThanVatAndVendorByTin(string invoiceLine, string expected)
    {
        var text = $"MISSPELLED STORE\nTIN 123-456-789-00000\n{invoiceLine}\nDATE: 11/05/2026\nVATable sales PHP 1,000.00\nVAT 12% PHP 120.00\nTOTAL PHP 1,120.00\n";
        var fields = Extraction.Extract(text, [new("Artificial Shop", "123-456-789-00000")]);
        Assert.AreEqual(new ReceiptFields("Artificial Shop", "123-456-789-00000", expected, "2026-11-05", "1120.00"), fields);
    }
    [TestMethod]
    [DataRow("2026-11-05")]
    [DataRow("05 Nov 2026")]
    [DataRow("Nov 5, 2026")]
    [DataRow("5-Nov-2026")]
    public void SupportedDatesNormalizeWithoutAnEngineSpecificRule(string date)
    {
        Assert.AreEqual("2026-11-05", Extraction.Extract("STORE\nDATE: " + date, []).Date);
    }
    [TestMethod]
    public void MatchingTotalsAreAcceptedAndConflictingTotalsRemainEmpty()
    {
        Assert.AreEqual("120.00", Extraction.Extract("STORE\nTOTAL 120.00\nAMOUNT DUE PHP 120.00", []).Amount);
        Assert.AreEqual("", Extraction.Extract("STORE\nTOTAL 120.00\nAMOUNT DUE PHP 130.00", []).Amount);
        Assert.AreEqual("", Extraction.Extract("STORE\nSUBTOTAL 120.00\nTOTAL VAT 12.00", []).Amount);
    }
    [TestMethod]
    public void VendorUsesNameSimilarityThenTopLineAndNineDigitTinIsSupported()
    {
        Assert.AreEqual("ARTIFICIAL BAYAN STORE", Extraction.Extract("ARTIFICIAL BAYAN ST0RE", [new("ARTIFICIAL BAYAN STORE", "999-999-999")]).Vendor);
        var fields = Extraction.Extract("ORANGE COFFEE\nTIN: 123 456 789", []);
        Assert.AreEqual("ORANGE COFFEE", fields.Vendor); Assert.AreEqual("", fields.Invoice); Assert.AreEqual("123-456-789", fields.Tin);
    }
}
