using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ExcelLayoutProfileTests
{
    [TestMethod]
    public void LocalCatalogLoadsLayoutAndKeepsDuplicateCodesAsDistinctColumns()
    {
        var root = Path.Combine(Path.GetTempPath(), "TsekSheet-profiles-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "layout.json"), LiquidationProfile.Json);
            var p = WorkflowProfiles.LoadDirectory(root).Single(p => p.Id == "synthetic-liquidation");
            Assert.AreEqual(19, p.Layout!.Accounts.Count);
            Assert.AreEqual("accountChoice", p.Fields.Single(f => f.Key == "account").Kind);
            Assert.AreEqual("money", p.Fields.Single(f => f.Key == "amount").Kind);
            Assert.AreEqual(19, WorkflowProfiles.Summary(p).Accounts!.Select(a => a.Column).Distinct().Count());
        }
        finally { File.Delete(Path.Combine(root, "layout.json")); Directory.Delete(root); }
    }

    [TestMethod]
    public void UnsupportedBlankRuleAndOverlappingFormulaInputsAreRejected()
    {
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Parse(LiquidationProfile.Json.Replace(ExcelLayoutProfile.SupportedBlankRule, "guess a blank row")));
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Parse(LiquidationProfile.Json.Replace("\"formulaColumns\":[\"N\",\"O\",\"AF\"]", "\"formulaColumns\":[\"J\",\"O\",\"AF\"]")));
    }

    [TestMethod]
    public void MarkersMoveWithTheWorkbookAndProtectedGrandTotalIsExcluded()
    {
        var p = LiquidationProfile.Create();
        var values = new object?[45, 32]; var formulas = new string?[45, 32]; var formats = new string[45, 32];
        foreach (var a in p.Layout!.SheetMatch.Anchors) values[1, A1Formula.ColumnNumber(a.Column) - 1] = a.Text;
        values[31, 2] = "TOTAL"; values[37, 2] = "TOTAL credit"; values[40, 2] = "TOTAL";
        var data = new RangeData(new(1, 1, 45, 32), values, formulas, formats);
        var expenses = ExcelLayoutMatching.Resolve(data, p.Layout.Section("expenses", "Any matching name"));
        var credits = ExcelLayoutMatching.Resolve(data, p.Layout.Section("credits", "Any matching name"));
        Assert.AreEqual(4, expenses.First); Assert.AreEqual(32, expenses.Subtotal);
        Assert.AreEqual(35, credits.First); Assert.AreEqual(38, credits.Subtotal);
        CollectionAssert.AreEqual(new[] { 41 }, expenses.Protected.ToArray());
        values[1, 2] = "wrong header";
        Assert.ThrowsExactly<ExcelInsertRejectedException>(() => ExcelLayoutMatching.Resolve(data, p.Layout.Section("expenses", "VENDOR")));
    }
}
