using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class PayablesProfileTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.BuiltInProfiles.Single(p => p.Id == "payables-rfp");

    [TestMethod]
    public void FixedProfileHasFourteenColumnsAndTwentyTwoFlatDestinations()
    {
        var profile = Profile;
        WorkflowProfiles.Validate(profile);
        Assert.AreEqual("fixed", profile.Layout!.MappingMode);
        Assert.AreEqual(14, profile.Fields.Count);
        CollectionAssert.AreEqual(new[] { "payee", "rfpNo", "amount" }, profile.Fields.Where(f => f.Required).Select(f => f.Key).ToArray());
        Assert.AreEqual(22, profile.Layout.Sections.Count);
        Assert.AreEqual(14, profile.Layout.Sections.Count(s => s.Id.StartsWith("member-company-")));
        Assert.AreEqual(0, profile.Layout.Accounts.Count);
        Assert.AreEqual("text", profile.Fields.Single(f => f.Key == "dateReceived").Kind);
        Assert.AreEqual("G", profile.Layout.Section("employee", "Sheet").ColumnMap["amount"]);
        Assert.AreEqual("PAYEE (VENDOR NAME)", profile.Layout.SheetMatch.Anchors.Single(a => a.Column == "C").Text);
        Assert.AreEqual("blank-inputs", profile.Layout.HybridNewRowMode);
        Assert.IsTrue(profile.Layout.Sections.All(section => section.InsertMode == "entireRow"));
        Assert.IsFalse(profile.Layout.ProtectedRows.Any(marker => marker.Match == "nonempty-text"));
        CollectionAssert.AreEqual(new[] { "employee", "cash-advance", "dollars", "last-pay", "refund", "member-company-1", "member-company-2",
            "member-company-3", "member-company-4", "member-company-5", "member-company-6", "member-company-7", "member-company-8",
            "member-company-9", "member-company-10", "member-company-11", "member-company-12", "member-company-13", "member-company-14",
            "insurance", "mobile-subscription", "external" }, profile.Layout.Sections.Select(s => s.Id).ToArray());
    }

    [TestMethod]
    public void OnlyThreeRequiredFieldsPermitBlankInvoiceAndPreserveIdentifierZeros()
    {
        var values = new Dictionary<string, string> { ["payee"] = "Synthetic vendor", ["rfpNo"] = "00012", ["amount"] = "123.50" };
        var result = WorkflowValueValidation.Validate(Profile.Fields, [], values);
        Assert.AreEqual("00012", result["rfpNo"].Text);
        Assert.AreEqual("", result["invoice"].Text);
        Assert.AreEqual(123.50m, result["amount"].Number);
        foreach (var key in values.Keys.ToArray())
        {
            var missing = new Dictionary<string, string>(values) { [key] = "" };
            Assert.ThrowsExactly<WorkbookException>(() => WorkflowValueValidation.Validate(Profile.Fields, [], missing));
        }
        values["amount"] = "invalid";
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowValueValidation.Validate(Profile.Fields, [], values));
    }

    [TestMethod]
    public void ScopedRepeatedMarkersRejectChangedCountsAndFollowMovedRows()
    {
        var values = new object?[40, 2];
        var data = new RangeData(new(1, 1, 40, 2), values, new string?[40, 2], new string[40, 2]);
        values[4, 1] = "Member Company";
        values[30, 1] = "Grand Total";
        for (var i = 0; i < 14; i++) values[6 + i, 1] = "Sub-Total";
        var marker = new RowMarker("B", "Sub-Total", 14, After: new("B", "Member Company", ExpectedCount: 1), Before: new("B", "Grand Total", ExpectedCount: 1), ExpectedCount: 14);
        Assert.AreEqual(20, ExcelLayoutMatching.Marker(data, marker));
        values[19, 1] = null; values[22, 1] = "Sub-Total";
        Assert.AreEqual(23, ExcelLayoutMatching.Marker(data, marker));
        values[24, 1] = "Sub-Total";
        Assert.ThrowsExactly<ExcelInsertRejectedException>(() => ExcelLayoutMatching.Marker(data, marker));
    }

    [TestMethod, DataRow("2024 PAYABLES (EMPLOYEE)", true), DataRow("2025 PAYABLES (Employee)", true), DataRow("2024  PAYABLES( EMPLOYEE )", true),
        DataRow("Employee", false), DataRow("2024 PAYABLES (EMPLOYEE) notes", false), DataRow("2024 PAYABLES (EXTERNAL)", false)]
    public void YearCategoryContractRequiresTheWholeHeadingAndPermitsDifferentYears(string heading, bool matches)
    {
        var values = new object?[,] { { heading } };
        var data = new RangeData(new(1, 2, 1, 1), values, new string?[1, 1], new string[1, 1]);
        var marker = new RowMarker("B", "Employee", ExpectedCount: 1, Match: "year-category");
        if (matches) Assert.AreEqual(1, ExcelLayoutMatching.Marker(data, marker));
        else Assert.ThrowsExactly<ExcelInsertRejectedException>(() => ExcelLayoutMatching.Marker(data, marker));
    }

    [TestMethod, DataRow("2024 INVENTORY", true), DataRow("2026 inventory", true), DataRow("Inventory", false),
        DataRow("2024 INVENTORY notes", false)]
    public void InventoryContractRequiresYearAndExactHeading(string text, bool matches)
    {
        var data = new RangeData(new(1, 2, 1, 1), new object?[,] { { text } }, new string?[1, 1], new string[1, 1]);
        var marker = new RowMarker("B", "Inventory", ExpectedCount: 1, Match: "year-heading");
        if (matches) Assert.AreEqual(1, ExcelLayoutMatching.Marker(data, marker));
        else Assert.ThrowsExactly<ExcelInsertRejectedException>(() => ExcelLayoutMatching.Marker(data, marker));
        if (text == "Inventory") Assert.AreEqual(1, ExcelLayoutMatching.Marker(data, new("B", "Inventory", ExpectedCount: 1)));
    }

    [TestMethod, DataRow("TOTAL PAYABLES AS OF DEC 2025", true), DataRow("TOTAL PAYABLES AS OF JAN 2026", true),
        DataRow("TOTAL EXTERNAL AS OF DEC 2025", false), DataRow("Other text", false), DataRow("TOTAL PAYABLES AS OF BAD 2026", false)]
    public void OverallTotalContractIsDistinctAndPeriodAware(string text, bool matches)
    {
        var data = new RangeData(new(1, 3, 1, 1), new object?[,] { { text } }, new string?[1, 1], new string[1, 1]);
        var marker = new RowMarker("C", "Payables", ExpectedCount: 1, Match: "period-total");
        if (matches) Assert.AreEqual(1, ExcelLayoutMatching.Marker(data, marker));
        else Assert.ThrowsExactly<ExcelInsertRejectedException>(() => ExcelLayoutMatching.Marker(data, marker));
    }
}
