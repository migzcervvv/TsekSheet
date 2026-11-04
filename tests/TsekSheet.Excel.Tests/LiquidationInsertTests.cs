using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workflows;
using TsekSheet.TestFixtures;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class LiquidationInsertTests
{
    [TestInitialize]
    public void RequireRelease()
    {
#if DEBUG
        Assert.Fail("Excel integration tests must run in Release configuration.");
#endif
    }

    [TestMethod]
    public async Task BlankFormulaRowsThenInsertionRespectAccountAndPreserveTheReplica()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await LiquidationFixture.CreateAsync(host);
        using (var zip = System.IO.Compression.ZipFile.OpenRead(path))
            Assert.IsTrue(zip.Entries.Any(e => e.FullName.StartsWith("xl/printerSettings/", StringComparison.Ordinal)), "Replica must contain Excel-authored printer settings.");
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var profile = LiquidationProfile.Create();
        var insertion = new ExcelReceiptInsertion(host, host, _ => profile);
        var choices = await insertion.ListAsync(profileId: profile.Id);
        CollectionAssert.AreEquivalent(new[] { "Person 1", "Person 2" }, choices.Single().Sheets.ToArray());
        var before = await host.CaptureAsync(book.Id);
        foreach (var item in new[] { (Row: 29, Column: "J", Amount: 123.50, Total: 413.32), (Row: 30, Column: "Q", Amount: 44.25, Total: 457.57), (Row: 31, Column: "L", Amount: 60.25, Total: 525.05) })
        {
            var section = Mapped(profile, "expenses", "Person 1", item.Column);
            var values = Values(section, item.Column, item.Amount, item.Row);
            var plan = await host.PreviewInsertAsync(book.Id, section, values);
            Assert.AreEqual(item.Row, plan.InsertRow);
            var result = await host.InsertAsync(plan.Token);
            Assert.AreEqual($"B{item.Row}:AF{item.Row}", result.Range);
            var after = await host.CaptureAsync(book.Id);
            foreach (var name in new[] { "Person 2", "VENDOR" }) AssertUnchanged(before.Sheets.Single(s => s.Name == name), after.Sheets.Single(s => s.Name == name));
            var target = after.Sheets.Single(s => s.Name == "Person 1").Data;
            foreach (var column in LiquidationProfile.Columns)
                Assert.AreEqual(column == item.Column ? item.Amount : null, ExcelLayoutMatching.Value(target, item.Row, A1Formula.ColumnNumber(column)), "Only the chosen account may receive an amount.");
            Assert.AreEqual($"=SUM(J{item.Row}:M{item.Row})*0.12", ExcelLayoutMatching.Formula(target, item.Row, 14));
            Assert.AreEqual($"=SUM(J{item.Row}:N{item.Row})", ExcelLayoutMatching.Formula(target, item.Row, 15));
            Assert.AreEqual($"=O{item.Row}+SUM(Q{item.Row}:AE{item.Row})", ExcelLayoutMatching.Formula(target, item.Row, 32));
            var grand = ExcelLayoutMatching.Marker(target, new("C", "TOTAL", 2));
            Assert.AreEqual(item.Total, Convert.ToDouble(ExcelLayoutMatching.Value(target, grand, 32)), 0.000001);
            var moved = item.Row == 31 ? 1 : 0;
            var originalCredits = before.Sheets.Single(s => s.Name == "Person 1").Data;
            for (var row = 33; row <= 37; row++)
                for (var col = 1; col <= 32; col++)
                {
                    var formula = ExcelLayoutMatching.Formula(originalCredits, row, col);
                    if (formula is not null && moved == 1) Assert.IsTrue(A1Formula.TryFillDown(formula, out formula));
                    Assert.AreEqual(formula, ExcelLayoutMatching.Formula(target, row + moved, col));
                    if (formula is null) Assert.AreEqual(ExcelLayoutMatching.Value(originalCredits, row, col), ExcelLayoutMatching.Value(target, row + moved, col));
                }
        }
        var final = await host.ReadAsync(book.Id, "Person 1", new(32, 10, 1, 23));
        Assert.AreEqual("=SUM(J4:J31)", final.Formulas[0, 0]);
        Assert.AreEqual("=SUM(AF4:AF31)", final.Formulas[0, 22]);
    }

    [TestMethod]
    public async Task VerifiedAccountChoiceMapsTheUnconvertedAmountThroughTheApplication()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await LiquidationFixture.CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var profile = LiquidationProfile.Create();
        using var receipt = new DesktopReceiptReview(_ => profile);
        var source = Path.Combine(Path.GetDirectoryName(path)!, "receipt.pdf");
        File.WriteAllText(source, "%PDF-1.7\n" + new string('x', 100));
        receipt.Register(source, default);
        var state = receipt.Start(profile.Id, 1, default);
        foreach (var field in new[] { ("itemNo", "NEW-29"), ("particulars", "Verified expense"), ("account", "Q"), ("amount", "123.50") })
        {
            state = receipt.Change("correct", state.Version!, field.Item1, 0, field.Item2, default);
            state = receipt.Change("verify", state.Version!, field.Item1, 1, null, default);
        }
        var insertion = new ExcelReceiptInsertion(host, host, _ => profile);
        var book = (await insertion.ListAsync(path, profile.Id)).Single();
        var preview = await insertion.PreviewAsync(receipt, state.Version!, book.Id, "expenses", default, "Person 1");
        Assert.AreEqual(17, preview.Values.Single(v => v.Key == "amount").Column);
        Assert.AreEqual(123.50, preview.Values.Single(v => v.Key == "amount").Value);
        Assert.AreEqual("123.50", preview.Values.Single(v => v.Key == "amount").DisplayValue);
        await insertion.InsertAsync(receipt, state.Version!, preview.Token, default);
        var data = await host.ReadAsync(book.Id, "Person 1", new(29, 10, 1, 23));
        Assert.IsNull(data.Values[0, 0]); Assert.AreEqual(123.50, data.Values[0, 7]);
    }

    [TestMethod]
    public async Task CreditsUseTheirMarkerBoundaryAndLeaveExpensesUntouched()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await LiquidationFixture.CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var before = await host.ReadAsync(book.Id, "Person 2", new(1, 1, 32, 32));
        var profile = LiquidationProfile.Create();
        var section = Mapped(profile, "credits", "Person 2", "AE");
        var plan = await host.PreviewInsertAsync(book.Id, section, Values(section, "AE", 17.25, 35));
        Assert.AreEqual(35, plan.InsertRow);
        await host.InsertAsync(plan.Token);
        var after = await host.ReadAsync(book.Id, "Person 2", before.Range);
        CollectionAssert.AreEqual(before.Values, after.Values);
        CollectionAssert.AreEqual(before.Formulas, after.Formulas);
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.PreviewInsertAsync(book.Id, section with { SheetName = "VENDOR" }, Values(section, "AE", 1, 1)));
    }

    internal static ExcelSectionDefinition Mapped(WorkflowProfile profile, string id, string sheet, string account)
    {
        var section = profile.Layout!.Section(id, sheet);
        return section with { ColumnMap = section.ColumnMap.Append(new KeyValuePair<string, string>("amount", account)).ToDictionary() };
    }
    internal static InsertValue[] Values(ExcelSectionDefinition section, string account, double amount, int index) =>
        section.ColumnMap.Select(p => new InsertValue(p.Key, p.Key, A1Formula.ColumnNumber(p.Value),
            p.Key == "amount" ? amount : p.Key == "itemNo" ? "NEW-" + index : p.Key == "particulars" ? "=literal particulars" : null)).ToArray();
    private static void AssertUnchanged(SheetSnapshot before, SheetSnapshot after)
    {
        Assert.AreEqual(before.Data.Range, after.Data.Range);
        CollectionAssert.AreEqual(before.Data.Values, after.Data.Values); CollectionAssert.AreEqual(before.Data.Formulas, after.Data.Formulas);
        CollectionAssert.AreEqual(before.Data.NumberFormats, after.Data.NumberFormats);
        CollectionAssert.AreEqual(before.MergedAreas.ToArray(), after.MergedAreas.ToArray()); CollectionAssert.AreEqual(before.Tables.ToArray(), after.Tables.ToArray());
        Assert.AreEqual(before.ValidationCells, after.ValidationCells); Assert.AreEqual(before.ConditionalFormats, after.ConditionalFormats);
    }
}
