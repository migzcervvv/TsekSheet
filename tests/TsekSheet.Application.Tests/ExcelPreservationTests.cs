using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ExcelPreservationTests
{
    [TestMethod]
    public void CompareRejectsUnintendedValuesFormulasFormatsAndEveryRequiredStructure()
    {
        var beforeSheet = new SheetSnapshot("Entry",
            new(new(1, 1, 1, 2), new object?[,] { { 1d, 2d } }, new string?[1, 2], new[,] { { "General", "General" } }),
            [], [], 0, 0, 0);
        var before = new PreservationSnapshot([beforeSheet], []);
        var correct = beforeSheet with { Data = beforeSheet.Data with { Values = new object?[,] { { 5d, 2d } } } };
        var after = new PreservationSnapshot([correct], []);
        var target = new CellRange(1, 1, 1, 1);
        var intended = new object?[,] { { 5d } };
        Assert.AreEqual(0, PreservationCheck.Compare(before, after, "Entry", target, intended).Count);
        SheetSnapshot[] mutations =
        [
            correct with { Data = correct.Data with { Values = new object?[,] { { 4d, 2d } } } },
            correct with { Data = correct.Data with { Values = new object?[,] { { 5d, 3d } } } },
            correct with { Data = correct.Data with { Formulas = new string?[,] { { null, "=1+1" } } } },
            correct with { Data = correct.Data with { NumberFormats = new[,] { { "General", "0.00" } } } },
            correct with { MergedAreas = ["D1:E1"] },
            correct with { Tables = [new("Unexpected", "F1:H4")] },
            correct with { ValidationCells = 1 },
            correct with { ValidationAreas = 1 },
            correct with { ConditionalFormats = 1 }
        ];
        foreach (var mutation in mutations)
            Assert.IsTrue(PreservationCheck.Compare(before, new([mutation], []), "Entry", target, intended).Count > 0);
        Assert.IsTrue(PreservationCheck.Compare(before, after with { Names = [new("Changed", "=Entry!$A$1")] }, "Entry", target, intended).Count > 0);
    }

    [TestMethod]
    public void DependentFormulaCachesMayRecalculateButFormulaTextMustRemainIdentical()
    {
        var first = new SheetSnapshot("Entry",
            new(new(1, 1, 1, 2), new object?[,] { { 1d, 2d } }, new string?[,] { { null, "=A1*2" } }, new[,] { { "General", "General" } }),
            [], [], 0, 0, 0);
        var second = first with { Data = first.Data with { Values = new object?[,] { { 5d, 10d } } } };
        Assert.AreEqual(0, PreservationCheck.Compare(new([first], []), new([second], []), "Entry", new(1, 1, 1, 1), new object?[,] { { 5d } }).Count);
        second = second with { Data = second.Data with { Formulas = new string?[,] { { null, "=A1+5" } } } };
        Assert.IsTrue(PreservationCheck.Compare(new([first], []), new([second], []), "Entry", new(1, 1, 1, 1), new object?[,] { { 5d } }).Count > 0);
    }
}
