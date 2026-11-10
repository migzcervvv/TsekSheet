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
        foreach (var expected in new[] { 8, 9, 52 })
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
        Assert.AreEqual("Sub-Total", ExcelLayoutMatching.Value(after, 53, 6));
        Assert.AreEqual("2024 INVENTORY", ExcelLayoutMatching.Value(after, 902, 2));
        Assert.AreEqual("UNRELATED", ExcelLayoutMatching.Value(after, 903, 3));
        Assert.IsNull(ExcelLayoutMatching.Value(after, 52, 10), "No optional calculation may be invented.");
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
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.PreviewInsertAsync(book.Id, formulaSection with { BlankHybridNewRows = false }, Values(formulaSection, "00098")));
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

    [TestMethod]
    public async Task AllVerifiedBoundariesResolveAgainAfterAnEarlierSectionInsertion()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var layout = Profile.Layout!;
        var before = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        Assert.AreEqual("PAYEE (VENDOR NAME)", ExcelLayoutMatching.Value(before, 3, 3));
        var expectedEnds = new[] { 52, 59, 63, 68, 72, 197, 233, 293, 448, 510, 541, 556, 572, 592, 604, 643, 654, 674, 679, 710, 749, 893 };
        var expectedStarts = new[] { 5, 55, 62, 66, 71, 75, 198, 234, 294, 449, 511, 542, 557, 573, 593, 605, 644, 655, 675, 684, 713, 753 };
        var insertion = new ExcelReceiptInsertion(host, host, _ => Profile);
        Assert.AreEqual(14, insertion.Sections(Profile.Id).Count(s => s.Id.StartsWith("member-company-")));
        for (var i = 0; i < layout.Sections.Count; i++)
        {
            var section = layout.Section(layout.Sections[i].Id, Sheet);
            var boundary = ExcelLayoutMatching.Resolve(before, section);
            Assert.AreEqual(expectedStarts[i], boundary.First, section.Id);
            Assert.AreEqual(expectedEnds[i], boundary.Subtotal, section.Id);
            Assert.AreEqual("F", section.SubtotalColumn);
            CollectionAssert.IsSubsetOf(new[] { 895, 897, 901 }, boundary.Protected.ToArray());
        }
        Assert.AreEqual("member-company-14", layout.Sections[18].Id);
        Assert.AreEqual("insurance", layout.Sections[19].Id);
        var employee = layout.Section("employee", Sheet) with { FillBlankRowsFirst = false };
        var preview = await host.PreviewInsertAsync(book.Id, employee, Values(employee, "SHIFT-1"));
        Assert.AreEqual(52, preview.InsertRow);
        await host.InsertAsync(preview.Token);
        var after = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        for (var i = 0; i < layout.Sections.Count; i++)
        {
            var boundary = ExcelLayoutMatching.Resolve(after, layout.Section(layout.Sections[i].Id, Sheet));
            Assert.AreEqual(expectedStarts[i] + (i == 0 ? 0 : 1), boundary.First);
            Assert.AreEqual(expectedEnds[i] + 1, boundary.Subtotal);
            CollectionAssert.IsSubsetOf(new[] { 896, 898, 902 }, boundary.Protected.ToArray());
        }
        Assert.AreEqual("Sub-Total", ExcelLayoutMatching.Value(after, 905, 6), "Inventory subtotals are excluded from Payables marker counts.");
    }

    [TestMethod]
    public async Task BlankRowReuseDoesNotShiftStatusOrInventoryAndSkipsOccupiedOffBandRow()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var section = Profile.Layout!.Section("employee", Sheet);
        var before = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        var preview = await host.PreviewInsertAsync(book.Id, section, Values(section, "REUSE-8"));
        Assert.AreEqual(8, preview.InsertRow);
        await host.InsertAsync(preview.Token);
        var after = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        Assert.AreEqual(before.Range, after.Range);
        Assert.AreEqual("Sub-Total", ExcelLayoutMatching.Value(after, 52, 6));
        Assert.AreEqual("Member-status-00196", ExcelLayoutMatching.Value(after, 196, 1));
        Assert.AreEqual("STATUS-MC-0196", ExcelLayoutMatching.Value(after, 196, 5));
        Assert.AreEqual("Reserved previous processing note", ExcelLayoutMatching.Value(after, 10, 1));
        Assert.IsNull(ExcelLayoutMatching.Value(after, 10, 5));
        Assert.AreEqual("2024 INVENTORY", ExcelLayoutMatching.Value(after, 901, 2));
        Assert.AreEqual("Synthetic inventory reference", ExcelLayoutMatching.Value(after, 901, 16));
        Assert.AreEqual("INV-ROW-902", ExcelLayoutMatching.Value(after, 902, 16));
        Assert.AreEqual("=G903*2", ExcelLayoutMatching.Formula(after, 903, 16));
    }

    [TestMethod]
    public async Task RepeatedWholeRowInsertionsKeepStatusAssociationsAndInventoryAligned()
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var layout = Profile.Layout!;
        var section = layout.Section("member-company-1", Sheet);
        var before = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        for (var i = 0; i < 3; i++)
        {
            var preview = await host.PreviewInsertAsync(book.Id, section, Values(section, "NEW-MEMBER-" + i));
            Assert.AreEqual(197 + i, preview.InsertRow);
            await host.InsertAsync(preview.Token);
            var data = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
            Assert.AreEqual("Member-status-00196", ExcelLayoutMatching.Value(data, 196, 1));
            Assert.AreEqual("STATUS-MC-0196", ExcelLayoutMatching.Value(data, 196, 5));
            Assert.AreEqual("=G196*0.12", ExcelLayoutMatching.Formula(data, 196, 9));
            Assert.AreEqual("Member-status-00198", ExcelLayoutMatching.Value(data, 199 + i, 1));
            Assert.AreEqual("STATUS-MC-0198", ExcelLayoutMatching.Value(data, 199 + i, 5));
            for (var inserted = 0; inserted <= i; inserted++)
            {
                Assert.IsNull(ExcelLayoutMatching.Value(data, 197 + inserted, 1));
                Assert.AreEqual("NEW-MEMBER-" + inserted, ExcelLayoutMatching.Value(data, 197 + inserted, 5));
                for (var c = 7; c <= 13; c++) Assert.IsNull(ExcelLayoutMatching.Formula(data, 197 + inserted, c));
            }
            Assert.AreEqual("External-status-00753", ExcelLayoutMatching.Value(data, 754 + i, 1));
            Assert.AreEqual("STATUS-EXT-0753", ExcelLayoutMatching.Value(data, 754 + i, 5));
            Assert.AreEqual("TOTAL EXTERNAL AS OF DEC 2025", ExcelLayoutMatching.Value(data, 896 + i, 3));
            Assert.AreEqual("TOTAL PAYABLES AS OF DEC 2025", ExcelLayoutMatching.Value(data, 898 + i, 3));
            Assert.AreEqual(Convert.ToDouble(ExcelLayoutMatching.Value(before, 897, 7)) + (i + 1) * 125.50d, Convert.ToDouble(ExcelLayoutMatching.Value(data, 898 + i, 7)));
            Assert.AreEqual(ExcelLayoutMatching.Value(before, 904, 7), ExcelLayoutMatching.Value(data, 905 + i, 7));
            Assert.AreEqual("2024 INVENTORY", ExcelLayoutMatching.Value(data, 902 + i, 2));
            Assert.AreEqual("Synthetic inventory reference", ExcelLayoutMatching.Value(data, 902 + i, 16));
            Assert.AreEqual("UNRELATED", ExcelLayoutMatching.Value(data, 903 + i, 3));
            Assert.AreEqual("INV-ROW-902", ExcelLayoutMatching.Value(data, 903 + i, 16));
            Assert.AreEqual($"=G{904 + i}*2", ExcelLayoutMatching.Formula(data, 904 + i, 16));
            Assert.AreEqual($"=SUM(G{903 + i}:G{904 + i})", ExcelLayoutMatching.Formula(data, 905 + i, 7));
            foreach (var destination in layout.Sections) _ = ExcelLayoutMatching.Resolve(data, layout.Section(destination.Id, Sheet));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public async Task MemberCompany01InsertsBlankInputsAboveFormulaBearingFinalRow(bool supplyVat)
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var section = Profile.Layout!.Section("member-company-1", Sheet);
        var values = Values(section, "MEMBER-0001").Select(v => v.Key == "vat" && supplyVat ? v with { Value = 7.25d } : v).ToArray();
        var before = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
        Assert.AreEqual("=G196*0.12", ExcelLayoutMatching.Formula(before, 196, 9));
        var queue = (StaCommandQueue)typeof(ExcelHost).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        // Force the Excel setting on, then prove the writer restores it after its bounded batch.
        var originalExtendList = await queue.InvokeAsync(() => { var app = (Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!; var original = app.ExtendList; app.ExtendList = true; return original; });
        try
        {
            var preview = await host.PreviewInsertAsync(book.Id, section, values);
            Assert.AreEqual(197, preview.InsertRow);
            await host.InsertAsync(preview.Token); // Full preservation check includes neighbors, totals, inventory and their shifts.
            var after = (await host.CaptureAsync(book.Id)).Sheets.Single().Data;
            Assert.AreEqual("=G196*0.12", ExcelLayoutMatching.Formula(after, 196, 9));
            Assert.IsNull(ExcelLayoutMatching.Formula(after, 197, 9));
            Assert.AreEqual(supplyVat ? 7.25d : null, ExcelLayoutMatching.Value(after, 197, 9));
            Assert.AreEqual(125.50d, ExcelLayoutMatching.Value(after, 197, 7));
            Assert.AreEqual("Sub-Total", ExcelLayoutMatching.Value(after, 198, 6));
            Assert.AreEqual("=SUM(G75:G197)", ExcelLayoutMatching.Formula(after, 198, 7));
            Assert.AreEqual("TOTAL PAYABLES AS OF DEC 2025", ExcelLayoutMatching.Value(after, 898, 3));
            Assert.AreEqual(Convert.ToDouble(ExcelLayoutMatching.Value(before, 897, 7)) + 125.50d, Convert.ToDouble(ExcelLayoutMatching.Value(after, 898, 7)));
            Assert.AreEqual(ExcelLayoutMatching.Value(before, 904, 7), ExcelLayoutMatching.Value(after, 905, 7));
            Assert.AreEqual("2024 INVENTORY", ExcelLayoutMatching.Value(after, 902, 2));
            Assert.AreEqual("UNRELATED", ExcelLayoutMatching.Value(after, 903, 3));
            Assert.AreEqual("Synthetic inventory reference", ExcelLayoutMatching.Value(after, 902, 16));
            Assert.AreEqual("INV-ROW-902", ExcelLayoutMatching.Value(after, 903, 16));
            Assert.AreEqual("=G904*2", ExcelLayoutMatching.Formula(after, 904, 16));
            Assert.AreEqual("Member-status-00196", ExcelLayoutMatching.Value(after, 196, 1));
            Assert.AreEqual("STATUS-MC-0196", ExcelLayoutMatching.Value(after, 196, 5));
            Assert.IsNull(ExcelLayoutMatching.Value(after, 197, 1));
            Assert.AreEqual("MEMBER-0001", ExcelLayoutMatching.Value(after, 197, 5));
            Assert.AreEqual("Member-status-00198", ExcelLayoutMatching.Value(after, 199, 1));
            Assert.AreEqual("STATUS-MC-0198", ExcelLayoutMatching.Value(after, 199, 5));
            var restored = await queue.InvokeAsync(() => ((Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!).ExtendList);
            Assert.IsTrue(restored);
        }
        finally
        {
            await queue.InvokeAsync(() => { ((Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!).ExtendList = originalExtendList; return 0; });
        }
    }

    [TestMethod, DataRow("heading"), DataRow("subtotal"), DataRow("missing-overall"), DataRow("extra-overall"), DataRow("missing-external"), DataRow("extra-external")]
    public async Task ChangedStructureRejectsPreviewWithoutFallbackOrWorkbookMutation(string fault)
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host, fault);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var before = await host.CaptureAsync(book.Id);
        // Even an Employee preview validates the Member/Insurance structure; it never selects another destination.
        var section = Profile.Layout!.Section("employee", Sheet);
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.PreviewInsertAsync(book.Id, section, Values(section, "NO-WRITE")));
        var after = await host.CaptureAsync(book.Id);
        CollectionAssert.AreEqual(before.Sheets.Single().Data.Values, after.Sheets.Single().Data.Values);
        CollectionAssert.AreEqual(before.Sheets.Single().Data.Formulas, after.Sheets.Single().Data.Formulas);
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tseksheet-*.xlsx").Length);
    }

    [TestMethod, DataRow("merge-template"), DataRow("offband-merge")]
    public async Task MergedMemberCompanyTemplateRemainsBlockedBeforeAnyWrite(string fault)
    {
        var host = await ExcelTestScope.SharedHostAsync();
        var path = await CreateAsync(host, fault);
        await using var scope = new ExcelTestScope();
        var book = await host.OpenAsync(path);
        var before = await host.CaptureAsync(book.Id);
        var section = Profile.Layout!.Section("member-company-1", Sheet);
        await Assert.ThrowsExactlyAsync<ExcelInsertRejectedException>(() => host.PreviewInsertAsync(book.Id, section, Values(section, "BLOCKED")));
        var after = await host.CaptureAsync(book.Id);
        CollectionAssert.AreEqual(before.Sheets.Single().Data.Values, after.Sheets.Single().Data.Values);
        CollectionAssert.AreEqual(before.Sheets.Single().Data.Formulas, after.Sheets.Single().Data.Formulas);
        CollectionAssert.AreEqual(before.Sheets.Single().MergedAreas.ToArray(), after.Sheets.Single().MergedAreas.ToArray());
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(path)!, "*.tseksheet-*.xlsx").Length);
    }

    private static InsertValue[] Values(ExcelSectionDefinition section, string rfp) => section.ColumnMap.Select(p =>
        new InsertValue(p.Key, Profile.Fields.Single(f => f.Key == p.Key).Header, A1Formula.ColumnNumber(p.Value),
            p.Key switch { "payee" => "Synthetic vendor", "rfpNo" => rfp, "amount" => 125.50d, _ => null })).ToArray();

    private static async Task<string> CreateAsync(ExcelHost host, string fault = "")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GOALS.md"))) directory = directory.Parent;
        var root = Path.Combine(directory!.FullName, "fixtures", "excel", "t1-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "payables-synthetic.xlsx");
        var queue = (StaCommandQueue)typeof(ExcelHost).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        await queue.InvokeAsync(() => Author(host, path, fault));
        return path;
    }

    private static int Author(ExcelHost host, string path, string fault)
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
                var data = new object?[905, 16];
                foreach (var anchor in Profile.Layout!.SheetMatch.Anchors) data[2, A1Formula.ColumnNumber(anchor.Column) - 1] = anchor.Text;
                var headings = new[] { (4, "2024 PAYABLES (EMPLOYEE)"), (54, "2025 PAYABLES (CASH ADVANCE)"),
                    (61, "2023 PAYABLES (DOLLARS)"), (65, "2025 PAYABLES (LAST PAY)"), (70, "2024 PAYABLES (REFUND)"),
                    (74, "2025 PAYABLES (MEMBER COMPANY)"), (683, "2024 PAYABLES (INSURANCE)"),
                    (712, "2025 PAYABLES (MOBILE SUBSCRIPTION)"), (752, "2023 PAYABLES (EXTERNAL)") };
                var ends = new[] { 52, 59, 63, 68, 72, 197, 233, 293, 448, 510, 541, 556, 572, 592, 604, 643, 654, 674, 679, 710, 749, 893 };
                var starts = new[] { 5, 55, 62, 66, 71, 75, 198, 234, 294, 449, 511, 542, 557, 573, 593, 605, 644, 655, 675, 684, 713, 753 };
                for (var i = 0; i < ends.Length; i++)
                {
                    for (var row = starts[i]; row < ends[i]; row++)
                    {
                        if (i == 0 && row is 7 or 8 or 9 or 10) continue;
                        data[row - 1, 2] = "Synthetic payee"; data[row - 1, 4] = "'00001"; data[row - 1, 6] = 10d;
                    }
                    data[ends[i] - 1, 5] = "Sub-Total";
                }
                foreach (var (row, heading) in headings) data[row - 1, 1] = heading;
                data[894, 2] = "TOTAL EXTERNAL AS OF DEC 2025";
                data[896, 2] = "TOTAL PAYABLES AS OF DEC 2025";
                data[5, 0] = "Employee-status-0006"; data[5, 4] = "'STATUS-EMP-0006";
                data[9, 0] = "Reserved previous processing note";
                data[195, 0] = "Member-status-00196"; data[195, 4] = "'STATUS-MC-0196";
                data[197, 0] = "Member-status-00198"; data[197, 4] = "'STATUS-MC-0198";
                data[752, 0] = "External-status-00753"; data[752, 4] = "'STATUS-EXT-0753";
                data[900, 1] = "2024 INVENTORY"; data[901, 2] = "UNRELATED"; data[903, 5] = "Sub-Total";
                data[901, 1] = "Synthetic inventory item";
                data[901, 6] = 27d; data[902, 6] = 5d;
                data[900, 15] = "Synthetic inventory reference"; data[901, 15] = "INV-ROW-902";
                if (fault == "heading") data[682, 1] = "2024 PAYABLES (UNRECOGNIZED)";
                if (fault == "subtotal") data[199, 5] = "Sub-Total";
                if (fault == "missing-overall") data[896, 2] = null;
                if (fault == "extra-overall") data[898, 2] = "TOTAL PAYABLES AS OF JAN 2026";
                if (fault == "missing-external") data[894, 2] = null;
                if (fault == "extra-external") data[895, 2] = "TOTAL EXTERNAL AS OF JAN 2026";
                if (fault == "offband-merge") data[196, 15] = "Merged insertion boundary";
                Put(sheet, new(1, 1, 905, 16), r => r.Value2 = data);
                var financial = new object?[901, 7];
                for (var row = 5; row <= 905; row++)
                    for (var c = 7; c <= 13; c++) financial[row - 5, c - 7] = data[row - 1, c - 1];
                financial[2, 0] = "=1+1";
                financial[3, 3] = "=1/0";
                financial[58 - 5, 3] = "=G58";
                financial[196 - 5, 2] = "=G196*0.12"; // Artificial formula; no accounting rule is inferred.
                for (var i = 0; i < ends.Length; i++) financial[ends[i] - 5, 0] = $"=SUM(G{starts[i]}:G{ends[i] - 1})";
                financial[895 - 5, 0] = "=G893";
                financial[897 - 5, 0] = "=SUM(" + string.Join(",", ends.Select(row => "G" + row)) + ")";
                financial[904 - 5, 0] = "=SUM(G902:G903)";
                Put(sheet, new(5, 7, 901, 7), r => r.Formula2 = financial);
                Put(sheet, new(901, 16, 3, 1), r => r.Formula2 = new object[,] { { "Synthetic inventory reference" }, { "INV-ROW-902" }, { "=G903*2" } });
                if (fault == "merge-template") Put(sheet, new(196, 9, 1, 2), r => r.Merge());
                if (fault == "offband-merge") Put(sheet, new(197, 16, 2, 1), r => r.Merge());
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
