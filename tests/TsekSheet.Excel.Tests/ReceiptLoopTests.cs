using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.TestFixtures;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class ReceiptLoopTests
{
    [TestInitialize]
    public void RequireRelease()
    {
#if DEBUG
        Assert.Fail("Excel integration tests must run in Release configuration.");
#endif
    }

    [TestMethod]
    public async Task OneDestinationSupportsTwentyConfirmedReceiptsAndPreservesAccountsFormulasAndNeighbors()
    {
#if DEBUG
        Assert.Fail("Excel integration tests must run in Release configuration.");
#endif
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await LiquidationFixture.CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var profile = LiquidationProfile.Create();
        var insertion = new ExcelReceiptInsertion(host, host, _ => profile);
        var book = (await insertion.ListAsync(path, profile.Id)).Single();
        using var queue = new ReceiptQueue(_ => profile);
        var sources = Enumerable.Range(0, 20).Select(i => Path.Combine(Path.GetDirectoryName(path)!, $"loop-{i}.pdf")).ToArray();
        for (var i = 0; i < sources.Length; i++) File.WriteAllText(sources[i], "%PDF-1.7\n" + new string('x', 100) + i);
        var q = queue.Add(sources, default);
        var before = await host.CaptureAsync(book.Id);
        double total = 275;
        for (var i = 0; i < 20; i++)
        {
            var state = queue.Active.Start(profile.Id, 1, default);
            var account = LiquidationProfile.Columns[i % 19];
            var values = profile.Fields.ToDictionary(f => f.Key, f => f.Key switch
            {
                "itemNo" => "FAST-" + i, "particulars" => "=literal expense", "account" => account, "amount" => "123.50", _ => ""
            });
            state = queue.Active.Prepare(state.Version!, values, default);
            var preview = await insertion.PreviewAsync(queue.Active, state.Version!, book.Id, "expenses", default, "Person 1", draft: true);
            Assert.AreEqual(29 + i, preview.InsertRow);
            Assert.AreEqual("Corrected", queue.Active.State.Fields.Single(f => f.Key == "amount").Status);
            var result = await insertion.InsertAsync(queue.Active, state.Version!, preview.Token, default, confirm: true);
            Assert.AreEqual($"B{29+i}:AF{29+i}", result.Range);
            var currentId = q.ActiveId!; q = queue.Complete(currentId);
            Assert.AreEqual(i + 1, q.Items.Count(e => e.Status == "Inserted"));
            if (i < 19) Assert.AreNotEqual(currentId, q.ActiveId);
            var after = await host.CaptureAsync(book.Id);
            foreach (var name in new[] { "VENDOR", "Person 2" })
            {
                var original = before.Sheets.Single(s => s.Name == name); var unchanged = after.Sheets.Single(s => s.Name == name);
                CollectionAssert.AreEqual(original.Data.Values, unchanged.Data.Values);
                CollectionAssert.AreEqual(original.Data.Formulas, unchanged.Data.Formulas);
                CollectionAssert.AreEqual(original.Data.NumberFormats, unchanged.Data.NumberFormats);
            }
            var data = after.Sheets.Single(s => s.Name == "Person 1").Data;
            for (var n = 0; n <= i; n++)
            {
                var row = 29 + n;
                Assert.AreEqual("FAST-" + n, ExcelLayoutMatching.Value(data, row, 2));
                Assert.AreEqual("=literal expense", ExcelLayoutMatching.Value(data, row, 3));
                Assert.IsNull(ExcelLayoutMatching.Formula(data, row, 3));
                foreach (var column in LiquidationProfile.Columns)
                    Assert.AreEqual(column == LiquidationProfile.Columns[n % 19] ? 123.50 : null, ExcelLayoutMatching.Value(data, row, A1Formula.ColumnNumber(column)));
                Assert.AreEqual($"=SUM(J{row}:M{row})*0.12", ExcelLayoutMatching.Formula(data, row, 14));
                Assert.AreEqual($"=SUM(J{row}:N{row})", ExcelLayoutMatching.Formula(data, row, 15));
                Assert.AreEqual($"=O{row}+SUM(Q{row}:AE{row})", ExcelLayoutMatching.Formula(data, row, 32));
            }
            total += (i % 19 < 4 ? 1.12 : 1) * 123.50;
            var grand = ExcelLayoutMatching.Marker(data, new("C", "TOTAL", 2));
            Assert.AreEqual(total, Convert.ToDouble(ExcelLayoutMatching.Value(data, grand, 32)), 0.000001);
            var subtotal = ExcelLayoutMatching.Marker(data, new("C", "TOTAL", 1));
            Assert.AreEqual($"=SUM(AF4:AF{subtotal-1})", ExcelLayoutMatching.Formula(data, subtotal, 32));
            var credit = ExcelLayoutMatching.Marker(data, new("C", "TOTAL credit", 1));
            Assert.AreEqual(5d, ExcelLayoutMatching.Value(data, credit, 17));
        }
        // A new review/source cannot duplicate an existing record identifier.
        using var duplicate = new DesktopReceiptReview(_ => profile); duplicate.Register(sources[0], default);
        var d = duplicate.Start(profile.Id, 1, default);
        d = duplicate.Prepare(d.Version!, profile.Fields.ToDictionary(f => f.Key, f => f.Key switch { "itemNo" => "FAST-0", "particulars" => "Duplicate", "account" => "J", "amount" => "1", _ => "" }), default);
        var error = await Assert.ThrowsExactlyAsync<TsekSheet.Application.Verification.FieldReviewException>(() => insertion.PreviewAsync(duplicate, d.Version!, book.Id, "expenses", default, "Person 1", draft: true));
        Assert.AreEqual("DUPLICATE_RECORD", error.Code);
    }

    [TestMethod]
    public async Task WorkbookChangesAfterDraftPreviewRejectTheConfirmedWrite()
    {
        var host = await ExcelTestScope.SharedHostAsync(); var path = await LiquidationFixture.CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path); var profile = LiquidationProfile.Create();
        using var receipt = new DesktopReceiptReview(_ => profile);
        var source = Path.Combine(Path.GetDirectoryName(path)!, "stale.pdf"); File.WriteAllText(source, "%PDF-1.7\n" + new string('x', 100));
        receipt.Register(source, default); var state = receipt.Start(profile.Id, 1, default);
        state = receipt.Prepare(state.Version!, profile.Fields.ToDictionary(f => f.Key, f => f.Key switch { "itemNo" => "STALE-1", "particulars" => "Stale target", "account" => "J", "amount" => "1", _ => "" }), default);
        var insertion = new ExcelReceiptInsertion(host, host, _ => profile);
        var preview = await insertion.PreviewAsync(receipt, state.Version!, book.Id, "expenses", default, "Person 1", draft: true);
        await host.WriteAsync(book.Id, "Person 2", new(1, 2, 1, 1), new object[,] { { "Explicit synthetic edit" } });
        var error = await Assert.ThrowsExactlyAsync<TsekSheet.Application.Verification.FieldReviewException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true));
        Assert.AreEqual("STALE_INSERT_PREVIEW", error.Code);
        Assert.IsNull((await host.ReadAsync(book.Id, "Person 1", new(29, 2, 1, 1))).Values[0, 0]);
    }
}
