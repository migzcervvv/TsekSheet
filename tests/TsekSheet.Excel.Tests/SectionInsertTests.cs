using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Text.Json;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using TsekSheet.Application.Workflows;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class SectionInsertTests
{
    public TestContext TestContext { get; set; } = null!;
    [TestInitialize]
    public void ReleaseOnly()
    {
#if DEBUG
        Assert.Fail("Excel integration tests require Release.");
#endif
    }

    [TestMethod]
    public async Task BoundTableUsesItsOwnBodyAndApprovedFormulaColumn()
    {
        var fixture = await ExcelFixture.CreateAsync(sections: true, boundTable: true);
        await using var scope = new ExcelTestScope();
        var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        var profile = WorkflowProfiles.Get("synthetic-payables");
        var section = profile.ExcelSections!.Single(s => s.Id == "table");
        var document = new DocumentType(new("synthetic-receipt", 1), profile.Fields.Select(f => new DocumentField(f.Key, f.Header, f.Kind, f.Required, f.MaxLength)).ToArray(), ["receipt"], true);
        var headers = profile.Fields.Select(f => new HeaderAnchor(section.ColumnMap[f.Key], f.Header)).Append(new("G", "Derived")).ToArray();
        var target = new WorkbookTarget("table", "Records", "Table", "table", "Records", section.ColumnMap, ["G"],
            new(2, headers), new("A", "Subtotal"), ["vendor", "amount"]);
        var binding = new WorkbookBinding(1, "synthetic-table", 1, document.Identity,
            "fixture.xlsx", ["Table"], "Synthetic_Table_Identity", [target]);
        var preview = await host.PreviewBoundInsertAsync(book.Id, binding, document, target.Id, Values(profile, section, 0));
        Assert.AreEqual(5, preview.InsertRow);
        await host.InsertAsync(preview.Token);
        var after = (await host.CaptureAsync(book.Id)).Sheets.Single(s => s.Name == "Table");
        Assert.AreEqual("A2:G6", after.Tables.Single().Address);
        Assert.AreEqual("=E5*2", ExcelLayoutMatching.Formula(after.Data, 5, 7));
        Assert.AreEqual("=SUM(E3:E5)", ExcelLayoutMatching.Formula(after.Data, 6, 5));
        Assert.AreEqual(77d, ExcelLayoutMatching.Value(after.Data, 3, 11));
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
            // Managed preview DTOs only; do not add a COM read or alter the write path.
            var diagnostics = id == "table" ? TablePreservationDiagnostics.Remember(host, plan.Token) : null;
            SectionInsertResult result;
            try { result = await host.InsertAsync(plan.Token); }
            catch (PreservationException error)
            {
                Console.Error.WriteLine($"Section {id}, iteration {i}: {string.Join("; ", error.Differences)}");
                if (diagnostics is not null)
                {
                    try { await diagnostics.WriteAsync(host, plan, i, error.Differences, TestContext); }
                    catch (Exception diagnosticError) { Console.Error.WriteLine($"Failure attachment unavailable: {diagnosticError}"); }
                }
                throw;
            }
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
    public async Task DeliberateTableRangeDifferenceWritesFailureSnapshotWithExpectedAndActual()
    {
        var fixture = await ExcelFixture.CreateAsync(sections: true);
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        var profile = WorkflowProfiles.Get("synthetic-payables");
        var section = profile.ExcelSections!.Single(s => s.Id == "table");
        var plan = await host.PreviewInsertAsync(book.Id, section, Values(profile, section, 0));
        var diagnostics = TablePreservationDiagnostics.Remember(host, plan.Token);
        Assert.IsNotNull(diagnostics);
        await host.InsertAsync(plan.Token);
        var before = await host.CaptureAsync(book.Id);

        // Deliberate corruption of a disposable synthetic fixture, on the host's STA.
        var queue = (StaCommandQueue)typeof(ExcelHost).GetField("queue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        await queue.InvokeAsync(() => EnlargeFixtureTable(host, fixture.Path));
        GC.Collect(); GC.WaitForPendingFinalizers();
        GC.Collect(); GC.WaitForPendingFinalizers();
        var after = await host.CaptureAsync(book.Id);
        // No intended changes: the synthetic resize must be detected independently
        // of any input/formula cells allowed during a normal insertion.
        var differences = PreservationCheck.Compare(before, after, string.Empty, new(1, 1, 1, 1), new object?[1, 1]);
        CollectionAssert.Contains(differences.ToArray(), "Table: worksheet structures changed.");

        var path = await diagnostics.WriteAsync(host, plan, 0, differences, TestContext);
        Assert.IsTrue(File.Exists(path), "Failure snapshot must be retained and attached.");
        using var snapshot = JsonDocument.Parse(File.ReadAllText(path));
        var difference = snapshot.RootElement.GetProperty("TableRangeDifferences").EnumerateArray().Single();
        Assert.AreEqual("Table", difference.GetProperty("Worksheet").GetString());
        Assert.AreEqual("A2:G6", difference.GetProperty("Range").GetString());
        Assert.AreEqual("ListObject[Records].Range", difference.GetProperty("Property").GetString());
        Assert.AreEqual("A2:G6", difference.GetProperty("Expected").GetString());
        Assert.AreEqual("A2:G7", difference.GetProperty("Actual").GetString());
        CollectionAssert.Contains(snapshot.RootElement.GetProperty("Differences").EnumerateArray().Select(d => d.GetString()).ToArray(),
            "Table: worksheet structures changed.");
    }

    private static int EnlargeFixtureTable(ExcelHost host, string path)
    {
        var application = (Interop.Application)typeof(ExcelHost).GetField("application", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
        Interop.Workbooks? books = null; Interop.Workbook? book = null; Interop.Sheets? sheets = null;
        Interop.Worksheet? sheet = null; Interop.ListObjects? tables = null; Interop.ListObject? table = null; Interop.Range? range = null;
        try
        {
            books = application.Workbooks; book = books[Path.GetFileName(path)];
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFullPath(path), Path.GetFullPath(book.FullName)))
                throw new InvalidOperationException("The open workbook is not the requested synthetic fixture.");
            sheets = book.Worksheets;
            sheet = (Interop.Worksheet)(object)sheets["Table"]; tables = sheet.ListObjects; table = tables["Records"];
            range = sheet.Range["A2:G7"]; table.Resize(range);
            return 0;
        }
        finally
        {
            ComLifetime.Release(range); ComLifetime.Release(table); ComLifetime.Release(tables);
            ComLifetime.Release(sheet); ComLifetime.Release(sheets); ComLifetime.Release(book); ComLifetime.Release(books);
        }
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
