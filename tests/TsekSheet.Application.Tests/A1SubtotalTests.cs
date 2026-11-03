using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class A1SubtotalTests
{
    [TestMethod]
    [DataRow("=SUM(B3:B4)", "=SUM(B3:B5)")]
    [DataRow("=SUM($B$3:$B$4)+SUM(C1:C2)+D4", "=SUM($B$3:$B$5)+SUM(C1:C2)+D4")]
    [DataRow("=IF(COUNT(B3:B4)>0,SUM(B3:B4),0)", "=IF(COUNT(B3:B5)>0,SUM(B3:B5),0)")]
    [DataRow("=SUM('Same sheet'!B3:B4)/2", "=SUM('Same sheet'!B3:B5)/2")]
    [DataRow("=SUMIF(A3:A4,\"B3:B4\",B3:B4)", "=SUMIF(A3:A5,\"B3:B4\",B3:B5)")]
    [DataRow("=SUM(B1:B2)+SUM(B3:B7)", "=SUM(B1:B2)+SUM(B3:B7)")]
    public void ExtendsOnlySameSheetRangeEndsAtOldLastRow(string formula, string expected)
    {
        Assert.IsTrue(A1Formula.TryExtendSubtotal(formula, "Same sheet", 4, 5, out var actual));
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("=SUM(Records[Amount])")]
    [DataRow("=SUM(Other!B3:B4)")]
    [DataRow("=SUM(NamedAmount)")]
    [DataRow("=SUM(B3:B4")]
    [DataRow("=SUM([Other.xlsx]Sheet1!B3:B4)")]
    [DataRow("=SUM(B3#)")]
    public void UnsupportedFormulaRemainsVerbatimAndRequiresManualReview(string formula)
    {
        Assert.IsFalse(A1Formula.TryExtendSubtotal(formula, "Same sheet", 4, 5, out var actual));
        Assert.AreEqual(formula, actual);
    }
}
