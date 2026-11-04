using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;

namespace TsekSheet.Excel.Tests;

[TestClass, TestCategory("Excel"), DoNotParallelize]
public sealed class ExcelIntegrationTests
{
    [TestInitialize]
    public void RequireRelease()
    {
#if DEBUG
        Assert.Fail("Excel integration tests must run in Release configuration.");
#endif
    }

    private static object?[,] TenValues()
    {
        var values = new object?[10, 1];
        for (var i = 0; i < 10; i++) values[i, 0] = 101d + i;
        return values;
    }

    [TestMethod]
    public async Task TenValuesPreserveEverySheetStructureAndRecalculateDependentFormulas()
    {
        var fixture = await ExcelFixture.CreateAsync();
        var original = Hash(fixture.Path);
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        try
        {
            var list = await host.ListWorkbooksAsync();
            Assert.AreEqual(book.Id, list.Single().Id);
            Assert.AreEqual(book.FullPath, list.Single().FullPath);
            CollectionAssert.AreEqual(new[] { "Entry", "Other" }, book.Sheets.ToArray());
            await host.ActivateAsync(book.Id, "Entry", new(3, 2, 10, 1));
            var used = await host.UsedRangeAsync(book.Id, "Entry");
            Assert.IsTrue(used.Contains(15, 3));
            var result = await host.WriteAsync(book.Id, "Entry", new(3, 2, 10, 1), TenValues());
            Assert.AreEqual(0, PreservationCheck.Compare(result.Before, result.After, "Entry", new(3, 2, 10, 1), TenValues()).Count);
            var left = result.After.Sheets.Single(s => s.Name == "Entry");
            Assert.AreEqual(1, left.Tables.Count);
            Assert.AreEqual(10L, left.ValidationCells);
            Assert.AreEqual(1, left.ConditionalFormats);
            Assert.AreEqual(1, left.MergedAreas.Count);
            Assert.AreEqual(1, result.After.Names.Count);
            var derived = await host.ReadAsync(book.Id, "Entry", new(3, 3, 10, 1));
            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual((101d + i) * 2, derived.Values[i, 0]);
                Assert.AreEqual($"=B{i + 3}*2", derived.Formulas[i, 0]);
                Assert.AreEqual("0.000", derived.NumberFormats[i, 0]);
            }
            Assert.AreEqual(original, Hash(fixture.Path), "The original must remain unsaved.");
            Assert.AreEqual(original, Hash(result.Backup.Path));
            Assert.AreEqual(original, result.Backup.Sha256);
            await host.RestoreHighlightAsync(result.Highlight.Id);
            var nextHighlight = await host.HighlightAsync(book.Id, "Entry", new(3, 2, 10, 1), 0xAAFFFF);
            CollectionAssert.AreEqual(result.Highlight.PreviousFills.ToArray(), nextHighlight.PreviousFills.ToArray(), "All original fills, including theme/tint, must be restored.");
            await host.RestoreHighlightAsync(nextHighlight.Id);
            var second = await host.WriteAsync(book.Id, "Entry", new(3, 2, 10, 1), TenValues());
            Assert.AreEqual(result.Backup, second.Backup, "Only one verified backup per session/workbook.");
            Assert.AreEqual(1, Directory.GetFiles(fixture.Root, "*.tseksheet-*.xlsx").Length);
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.CloseAsync(book.Id));
        }
        finally { await host.CloseAsync(book.Id, discardChanges: true); }
    }

    [TestMethod]
    public async Task FormulaTargetsAreRejectedAndLeadingEqualsIsStoredAsLiteralText()
    {
        var fixture = await ExcelFixture.CreateAsync();
        await using var scope = new ExcelTestScope(); var host = scope.Host;
        var book = await host.OpenAsync(fixture.Path);
        try
        {
            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => host.WriteAsync(book.Id, "Entry", new(3, 3, 1, 1), new object?[,] { { 99d } }));
            Assert.AreEqual(0, Directory.GetFiles(fixture.Root, "*.tseksheet-*.xlsx").Length);
            var result = await host.WriteAsync(book.Id, "Other", new(1, 2, 1, 1), new object?[,] { { "=1+1" } });
            var value = await host.ReadAsync(book.Id, "Other", new(1, 2, 1, 1));
            Assert.AreEqual("=1+1", value.Values[0, 0]);
            Assert.IsNull(value.Formulas[0, 0]);
            Assert.AreEqual(0, PreservationCheck.Compare(result.Before, result.After, "Other", new(1, 2, 1, 1), new object?[,] { { "=1+1" } }).Count);
        }
        finally { await host.CloseAsync(book.Id, discardChanges: true); }
    }

    [TestMethod]
    public async Task ThirtyConsecutiveLifecyclesReleaseTheirOwnedExcelProcess()
    {
        var fixture = await ExcelFixture.CreateAsync();
        await ExcelTestScope.RetireSharedHostAsync();
        Assert.IsTrue(await OwnedExcelProcesses.WaitForPendingExitsAsync(), "A previous owned instance is alive 120 seconds after Quit.");
        try
        {
            for (var i = 0; i < 30; i++)
            {
                ExcelFixture.MeasurementCycle = i + 1;
                await using var scope = new ExcelTestScope(ownsHost: true); var host = scope.Host;
                var info = await host.StartAsync();
                try
                {
                    var book = await host.OpenAsync(fixture.Path);
                    try { await host.WriteAsync(book.Id, "Entry", new(3, 2, 10, 1), TenValues()); }
                    finally { await host.CloseAsync(book.Id, discardChanges: true); }
                }
                finally
                {
                    await scope.DisposeAsync();
                }
                Assert.IsTrue(await OwnedExcelProcesses.WaitForExitAsync(info.ProcessId), $"Owned Excel PID={info.ProcessId} is alive 120 seconds after Quit.");
            }
        }
        finally { ExcelFixture.MeasurementCycle = 0; }
    }

    [TestMethod]
    public async Task ExistingUserExcelAndItsOpenWorkbookRemainUntouched()
    {
        var fixture = await ExcelFixture.CreateAsync();
        var ownFixture = await ExcelFixture.CreateAsync();
        // This independent host supplies the pre-existing instance; the adapter under test
        // must neither attach to it nor close its open synthetic workbook.
        await using var userScope = new ExcelTestScope(); var user = userScope.Host;
        var userBook = await user.OpenAsync(fixture.Path);
        var userInfo = await user.StartAsync();
        var before = await user.CaptureAsync(userBook.Id);
        try
        {
            await using var scope = new ExcelTestScope(ownsHost: true); var host = scope.Host;
            var ownInfo = await host.StartAsync();
            Assert.AreNotEqual(userInfo.ProcessId, ownInfo.ProcessId);
            try
            {
                Assert.AreEqual(0, (await host.ListWorkbooksAsync()).Count);
                var book = await host.OpenAsync(ownFixture.Path);
                try { await host.WriteAsync(book.Id, "Entry", new(3, 2, 10, 1), TenValues()); }
                finally { await host.CloseAsync(book.Id, discardChanges: true); }
            }
            finally { await scope.DisposeAsync(); }
            using var process = Process.GetProcessById(userInfo.ProcessId);
            Assert.IsFalse(process.HasExited);
            Assert.AreEqual(userBook.Id, (await user.ListWorkbooksAsync()).Single().Id);
            var after = await user.CaptureAsync(userBook.Id);
            Assert.AreEqual(0, PreservationCheck.Compare(before, after, "Entry", new(3, 2, 10, 1), OriginalValues()).Count);
        }
        // Excel can mark a freshly opened workbook dirty through recalculation alone.
        // Cleanup explicitly discards this test-owned synthetic instance after the assertions.
        finally { await user.CloseAsync(userBook.Id, discardChanges: true); }
    }

    private static object?[,] OriginalValues()
    {
        var values = new object?[10, 1];
        for (var i = 0; i < 10; i++) values[i, 0] = i + 1d;
        return values;
    }

    private static string Hash(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(input));
    }

}
