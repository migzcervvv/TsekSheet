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
}
