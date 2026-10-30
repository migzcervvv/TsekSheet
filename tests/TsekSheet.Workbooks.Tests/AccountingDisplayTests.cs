using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

public sealed partial class WorkbookInspectionTests
{
    [TestMethod]
    [DataRow("1234.5", "#,##0.00", "1,234.50")]
    [DataRow("-1234.5", "#,##0.00;(#,##0.00)", "(1,234.50)")]
    [DataRow("1234.5", "$#,##0.00;($#,##0.00)", "$1,234.50")]
    [DataRow("-1234.5", "$#,##0.00;($#,##0.00)", "($1,234.50)")]
    [DataRow("0", "#,##0.00;(#,##0.00);\"-\"", "-")]
    [DataRow("0", "0.00;(0.00);\"\"", "")]
    [DataRow("0", "0.00;(0.00);", "")]
    [DataRow("0", "0.00", "0.00")]
    [DataRow("0.125", "0.00%", "12.50%")]
    [DataRow("-0.125", "0.0%;(0.0%)", "(12.5%)")]
    [DataRow("12.5", "\"₱\" #,##0.00", "₱ 12.50")]
    public void AccountingGoldenDisplaysKeepRawValues(string raw, string format, string expected)
    {
        var path = InterpretedPackage($"<row><c r=\"A1\"><v>{raw}</v></c></row>", format); var hash = Hash(path);
        using var session = new WorkbookInspection().Open(path); var cell = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0];
        Assert.AreEqual(raw, cell.Value); Assert.AreEqual(expected, cell.Interpretation!.DisplayValue); Assert.AreEqual(hash, Hash(path));
    }
    [TestMethod, DataRow("[$-409]#,##0.00"), DataRow("[Red]0.00"), DataRow("0.00;[Red](0.00)"), DataRow("0.00_);(0.00)"), DataRow("0.00;0.00;0;@")]
    public void UnsupportedAccountingSyntaxStaysRaw(string format)
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage("<row><c r=\"A1\"><v>12.5</v></c></row>", format));
        var c = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Cells[0]; Assert.AreEqual("12.5", c.Interpretation!.DisplayValue); Assert.AreEqual("Unsupported", c.Interpretation.Confidence);
    }
    [TestMethod]
    public void TextIdentifiersAndBlankZeroDistinctionsRemainExact()
    {
        using var session = new WorkbookInspection().Open(InterpretedPackage("<row><c r=\"A1\" t=\"inlineStr\"><is><t>0000123</t></is></c><c r=\"B1\" t=\"inlineStr\"><is><t></t></is></c><c r=\"C1\"><v>0</v></c></row>", "0.00"));
        var v = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 4); Assert.AreEqual("0000123", v.Cells[0].Interpretation!.DisplayValue); Assert.AreEqual("Text", v.Cells[1].Kind); Assert.AreEqual("0.00", v.Cells[2].Interpretation!.DisplayValue); Assert.AreEqual("Blank", v.Cells[3].Kind);
    }
}
