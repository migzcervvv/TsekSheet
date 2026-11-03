using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class SectionInsertTests
{
    [TestInitialize]
    public void ReleaseOnly()
    {
#if DEBUG
        Assert.Fail("Excel integration tests require Release.");
#endif
    }

    [TestMethod]
    [DataRow("columns", false)]
    [DataRow("stacked", false)]
    [DataRow("table", false)]
    public async Task FiveInsertsPreserveNeighboursFillFormulasAndExtendOnlyTheSectionSubtotal(string id, bool date1904)
    {
        var fixture = await ExcelFixture.CreateAsync(sections: true, date1904: date1904);
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        var profile = WorkflowProfiles.Get("synthetic-payables");
        var section = profile.ExcelSections!.Single(s => s.Id == id);
        var neighbour = await host.ReadAsync(book.Id, section.SheetName, id == "stacked" ? new(7, 1, 2, 7) : new(2, 10, 3, 2));
        for (var i = 0; i < 5; i++)
        {
            var values = Values(profile, section, i);
            var plan = await host.PreviewInsertAsync(book.Id, section, values);
            Assert.AreEqual(5 + i, plan.InsertRow);
            var result = await host.InsertAsync(plan.Token);
            var row = await host.ReadAsync(book.Id, section.SheetName, new(5 + i, 1, 1, 7));
            Assert.AreEqual("=literal entry", row.Values[0, 0]); Assert.IsNull(row.Formulas[0, 0]);
            Assert.AreEqual(100d + i, row.Values[0, 4]);
            Assert.AreEqual(new DateTime(2026, 11, 3).ToOADate() - (date1904 ? 1462d : 0d), row.Values[0, 5]);
            Assert.AreEqual((100d + i) * 2, row.Values[0, 6]);
            Assert.AreEqual($"=E{5 + i}*2", row.Formulas[0, 6]);
            var subtotal = await host.ReadAsync(book.Id, section.SheetName, new(6 + i, 5, 1, 3));
            Assert.AreEqual($"=SUM(E3:E{5 + i})", subtotal.Formulas[0, 0]);
            Assert.AreEqual(50d + Enumerable.Range(0, i + 1).Sum(n => 100d + n), subtotal.Values[0, 0]);
            Assert.AreEqual($"=SUM(G3:G{5 + i})", subtotal.Formulas[0, 2]);
            var mixedSubtotal = await host.ReadAsync(book.Id, section.SheetName, new(6 + i, 3, 1, 1));
            Assert.AreEqual($"=SUM(E3:E{5 + i})+SUM(E3:E3)+E4", mixedSubtotal.Formulas[0, 0],
                "Only range ends equal to the old last data row may extend; single-cell references stay unchanged.");
            Assert.AreEqual(0, result.ManualReview.Count);
            await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.InsertAsync(plan.Token));
        }
        var neighbourAfter = await host.ReadAsync(book.Id, section.SheetName, id == "stacked" ? new(12, 1, 2, 7) : new(2, 10, 3, 2));
        CollectionAssert.AreEqual(neighbour.Values, neighbourAfter.Values);
        CollectionAssert.AreEqual(neighbour.Formulas, neighbourAfter.Formulas);
    }

    [TestMethod]
    public async Task ChangedWorkbookRejectsAnOldPreviewWithoutInserting()
    {
        var fixture = await ExcelFixture.CreateAsync(sections: true);
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        var profile = WorkflowProfiles.Get("synthetic-payables"); var section = profile.ExcelSections!.First();
        var plan = await host.PreviewInsertAsync(book.Id, section, Values(profile, section, 0));
        await host.WriteAsync(book.Id, "Other", new(1, 2, 1, 1), new object?[,] { { "Changed after preview" } });
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.InsertAsync(plan.Token));
        var subtotal = await host.ReadAsync(book.Id, "Columns", new(5, 1, 1, 1));
        Assert.AreEqual("Subtotal", subtotal.Values[0, 0]);
    }

    [TestMethod]
    public Task DatesUseTheWorkbooks1904Epoch() =>
        FiveInsertsPreserveNeighboursFillFormulasAndExtendOnlyTheSectionSubtotal("columns", true);

    [TestMethod]
    public async Task UnparseableSubtotalIsLeftVerbatimAndReportedWithoutBlockingTheInsert()
    {
        var fixture = await ExcelFixture.CreateAsync(sections: true, unsupportedSubtotal: true);
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        var profile = WorkflowProfiles.Get("synthetic-payables"); var section = profile.ExcelSections!.Single(s => s.Id == "columns");
        var plan = await host.PreviewInsertAsync(book.Id, section, Values(profile, section, 0));
        Assert.AreEqual(1, plan.ManualReview.Count);
        var result = await host.InsertAsync(plan.Token);
        Assert.AreEqual("G6:G6", result.ManualReview.Single().Cell);
        var subtotal = await host.ReadAsync(book.Id, "Columns", new(6, 5, 1, 3));
        Assert.AreEqual("=SUM(E3:E5)", subtotal.Formulas[0, 0]);
        Assert.AreEqual("=SUM(Records[Derived])", subtotal.Formulas[0, 2]);
    }

    private static InsertValue[] Values(WorkflowProfile profile, ExcelSectionDefinition section, int index) =>
        profile.Fields.Select(field => new InsertValue(field.Key, field.Header, A1Formula.ColumnNumber(section.ColumnMap[field.Key]),
            field.Key == "vendor" ? "=literal entry" : field.Kind == "decimal" ? (object)(100d + index) : field.Kind == "date" ? new DateTime(2026, 11, 3).ToOADate() : "Verified " + index,
            IsDate: field.Kind == "date")).ToArray();
}
