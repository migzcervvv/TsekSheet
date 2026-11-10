using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workflows;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class PayablesInsertTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.BuiltInProfiles.Single(p => p.Id == "payables-rfp");
    private const string Sheet = "Unpaid RFPs - SEP";

    [TestInitialize]
    public void ReleaseOnly()
    {
#if DEBUG
        Assert.Fail("Excel tests require Release.");
#endif
    }

    [TestMethod]
    public async Task ReusesCompatibleRowsThenInsertsBeforeSubtotalWithExcelPreservation()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var layout = Profile.Layout!;
        var before = await host.CaptureAsync(book.Id);
        var data = before.Sheets.Single().Data;
        foreach (var destination in layout.Sections)
        {
            var boundary = ExcelLayoutMatching.Resolve(data, layout.Section(destination.Id, Sheet));
            Assert.IsTrue(boundary.First > 3 && boundary.Subtotal < 900);
        }
        var section = layout.Section("employee", Sheet);
        foreach (var expected in new[] { 8, 9, 10 })
        {
            var inputs = Values(section, "000" + expected).Select(v => expected == 8 && v.Key == "invoice" ? v with { Value = "00042" } : v).ToArray();
            var preview = await host.PreviewInsertAsync(book.Id, section, inputs);
            if (expected == 8) Assert.AreEqual("Existing formula preserved", preview.Values.Single(v => v.Key == "totalAmount").DisplayValue);
            Assert.AreEqual(expected, preview.InsertRow);
            Assert.AreEqual($"B{expected}:O{expected}", preview.Range);
            Assert.AreEqual(7, preview.Values.Single(v => v.Key == "amount").Column);
            await host.InsertAsync(preview.Token); // Runs the full Excel readback preservation check.
        }
        var after = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        Assert.AreEqual("0008", ExcelLayoutMatching.Value(after, 8, 5));
        Assert.AreEqual("00042", ExcelLayoutMatching.Value(after, 8, 6));
        Assert.AreEqual("=1/0", ExcelLayoutMatching.Formula(after, 8, 10));
        Assert.AreEqual("=1+1", ExcelLayoutMatching.Formula(after, 7, 7));
        Assert.AreEqual("Sub-Total", ExcelLayoutMatching.Value(after, 11, 2));
        Assert.AreEqual("Inventory", ExcelLayoutMatching.Value(after, 901, 2));
        Assert.AreEqual("UNRELATED", ExcelLayoutMatching.Value(after, 902, 3));
        Assert.IsNull(ExcelLayoutMatching.Value(after, 10, 10), "No optional calculation may be invented.");
    }

    [TestMethod]
    public async Task SuppliedOptionalAmountSkipsFormulaRowsAndUnsafeFormulaTemplateIsRejected()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var section = Profile.Layout!.Section("employee", Sheet);
        var values = Values(section, "00099").Select(v => v.Key == "totalAmount" ? v with { Value = 42d } : v).ToArray();
        var preview = await host.PreviewInsertAsync(book.Id, section, values);
        Assert.AreEqual(9, preview.InsertRow, "Row 8 contains a Total Amount formula and cannot accept 42.");
        await host.InsertAsync(preview.Token);
        var read = await host.ReadAsync(book.Id, Sheet, new(8, 7, 2, 4));
        Assert.AreEqual("=1/0", read.Formulas[0, 3]);
        Assert.AreEqual(42d, read.Values[1, 3]);
        var formulaSection = Profile.Layout.Section("cash-advance", Sheet);
        // Its final template contains a formula; no formula is guessed or copied into a new row.
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.PreviewInsertAsync(book.Id, formulaSection, Values(formulaSection, "00098")));
    }

    [TestMethod]
    public async Task ManualDraftCheckWarnsForDuplicateButExplicitInsertAndSourceGuardRemain()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var source = Path.Combine(Path.GetDirectoryName(path)!, "manual.pdf");
        File.WriteAllText(source, "%PDF-1.7\n" + new string('x', 100));
        using var receipt = new DesktopReceiptReview(_ => Profile);
        receipt.Register(source, default);
        var state = receipt.Start(Profile.Id, 1, default);
        var values = Profile.Fields.ToDictionary(f => f.Key, f => f.Key switch { "payee" => "Manual vendor", "rfpNo" => "00001", "amount" => "125.50", _ => "" });
        state = receipt.Prepare(state.Version!, values, default);
        var insertion = new ExcelReceiptInsertion(host, host, _ => Profile);
        var preview = await insertion.PreviewAsync(receipt, state.Version!, book.Id, "employee", default, Sheet, draft: true);
        Assert.AreEqual(1, preview.AdvisoryWarnings!.Count);
        Assert.AreEqual(book.Id, preview.WorkbookId);
        Assert.AreEqual(Sheet, preview.Sheet);
        Assert.AreEqual("employee", preview.SectionId);
        Assert.AreEqual("00001", preview.Values.Single(v => v.Key == "rfpNo").Value);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.InsertAsync(receipt, state.Version!, preview.Token, default));
        preview = await insertion.PreviewAsync(receipt, state.Version!, book.Id, "employee", default, Sheet, draft: true);
        await insertion.InsertAsync(receipt, state.Version!, preview.Token, default, confirm: true);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(receipt, receipt.State.Version!, book.Id, "employee", default, Sheet, draft: true));
        using var repeat = new DesktopReceiptReview(_ => Profile);
        repeat.Register(source, default);
        var restarted = repeat.Start(Profile.Id, 2, default);
        restarted = repeat.Prepare(restarted.Version!, values, default);
        await Assert.ThrowsExactlyAsync<FieldReviewException>(() => insertion.PreviewAsync(repeat, restarted.Version!, book.Id, "employee", default, Sheet, draft: true));
    }

    private static InsertValue[] Values(ExcelSectionDefinition section, string rfp) => section.ColumnMap.Select(p =>
        new InsertValue(p.Key, Profile.Fields.Single(f => f.Key == p.Key).Header, A1Formula.ColumnNumber(p.Value),
            p.Key switch { "payee" => "Synthetic vendor", "rfpNo" => rfp, "amount" => 125.50d, _ => null })).ToArray();

    private static async Task<string> CreateAsync(ExcelHost host)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        var root = Path.Combine(directory!.FullName, "fixtures", "excel", "t1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "payables-synthetic.xlsx");
        var queue = (StaCommandQueue)typeof(ExcelHost).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        await queue.InvokeAsync(() => Author(host, path));
        return path;
    }

    private static int Author(ExcelHost host, string path)
    {
        var app = (Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        Interop.Workbooks? books = null; Interop.Workbook? book = null;
        Interop.Sheets? sheets = null; Interop.Worksheet? sheet = null;
        try
        {
            books = app.Workbooks; book = books.Add(Interop.XlWBATemplate.xlWBATWorksheet);
            sheets = book.Worksheets; sheet = (Interop.Worksheet)(object)sheets[1]; sheet.Name = Sheet;
            var screen = app.ScreenUpdating; var events = app.EnableEvents; var calculation = app.Calculation;
            try
            {
                app.ScreenUpdating = false; app.EnableEvents = false; app.Calculation = Interop.XlCalculation.xlCalculationManual;
                var data = new object?[902, 15];
                foreach (var field in Profile.Layout!.Fields) data[2, A1Formula.ColumnNumber(field.Column!) - 1] = field.Label;
                for (var i = 0; i < 22; i++)
                {
                    var heading = 5 + i * 7;
                    if (i <= 8) data[heading - 1, 1] = i < 8 ? Profile.Layout.Sections[i].Label : "Member Company";
                    for (var r = heading + 1; r <= heading + 4; r++)
                    {
                        if (i == 0 && r > heading + 1) continue;
                        data[r - 1, 2] = "Synthetic payee"; data[r - 1, 4] = "'00001"; data[r - 1, 6] = 10d;
                    }
                    data[heading + 4, 1] = "Sub-Total";
                }
                data[160, 1] = "Grand Total"; data[899, 1] = "Inventory"; data[900, 2] = "UNRELATED";
                Put(sheet, new(1, 1, 902, 15), r => r.Value2 = data);
                var financial = new object?[156, 7];
                for (var r = 6; r <= 161; r++)
                    for (var c = 7; c <= 13; c++) financial[r - 6, c - 7] = data[r - 1, c - 1];
                financial[1, 0] = "=1+1";
                financial[2, 3] = "=1/0";
                financial[10, 3] = "=G16";
                for (var i = 0; i < 22; i++)
                {
                    var subtotal = 10 + i * 7;
                    financial[subtotal - 6, 0] = $"=SUM(G{subtotal - 4}:G{subtotal - 1})";
                }
                financial[155, 0] = "=SUM(G6:G159)";
                Put(sheet, new(6, 7, 156, 7), r => r.Formula2 = financial);
            }
            finally { app.Calculation = calculation; app.EnableEvents = events; app.ScreenUpdating = screen; }
            book.SaveAs(path, Interop.XlFileFormat.xlOpenXMLWorkbook);
            return 0;
        }
        finally
        {
            ComLifetime.Release(sheet); ComLifetime.Release(sheets);
            try { if (book is not null) book.Close(false); }
            finally { ComLifetime.Release(book); ComLifetime.Release(books); }
        }
    }

    private static void Put(Interop.Worksheet sheet, CellRange bounds, Action<Interop.Range> action)
    {
        Interop.Range? range = null;
        try { range = sheet.get_Range(bounds.Address); action(range); }
        finally { ComLifetime.Release(range); }
    }
}
