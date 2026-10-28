using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class SafeSaveWorkbookTests
{
    [TestMethod]
    public void MissingOriginalPathCannotBeRecreatedBySaveAs()
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reimbursement-synthetic.xlsx");
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-source-binding-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var source = Path.Combine(folder, "source.xlsx"); File.Copy(fixture, source);
            using var main = new WorkbookEngine().Open(source); File.Delete(source);
            Assert.AreEqual("ORIGINAL_DESTINATION", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(source, 0)).Code);
            Assert.IsFalse(File.Exists(source));
            using var snapshot = main.CreateSnapshot();
            Assert.AreEqual("WORKING_COPY_SAVE_UNSUPPORTED", Assert.ThrowsExactly<WorkbookException>(() => snapshot.SaveAsNew(Path.Combine(folder, "snapshot.xlsx"), 0)).Code);
        }
        finally { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
    [TestMethod]
    public void FailedAndUncertainCommitsRetainUnsavedState()
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "reimbursement-synthetic.xlsx");
        var hash = SHA256.HashData(File.ReadAllBytes(source));
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-save-failure-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        try
        {
            var engine = new WorkbookEngine(); using var main = engine.Open(source);
            main.Edit(new(main.Info.Worksheets[0].Id, 5, 1, "text", "Pending", 0));
            engine.CommitFaultsForTesting = new() { BeforeCommit = () => throw new IOException("injected") };
            Assert.AreEqual("SAVE_FAILED", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(Path.Combine(folder, "failed.xlsx"), 1)).Code);
            Assert.IsFalse(File.Exists(Path.Combine(folder, "failed.xlsx")));
            engine.CommitFaultsForTesting = new() { AfterCommit = () => throw new IOException("injected") };
            Assert.AreEqual("SAVE_UNCERTAIN", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(Path.Combine(folder, "uncertain.xlsx"), 1)).Code);
            Assert.IsTrue(File.Exists(Path.Combine(folder, "uncertain.xlsx")));
            Assert.AreEqual(1, main.Info.Revision); Assert.IsTrue(main.Info.IsDirty); Assert.IsNull(main.Info.LastExportedRevision);
            CollectionAssert.AreEqual(hash, SHA256.HashData(File.ReadAllBytes(source)));
        }
        finally { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
    [TestMethod]
    [DataRow("reimbursement")]
    [DataRow("laboratory")]
    public void ValidatedCandidateExportsMainAndPreservesSourceAndPendingCopies(string fixture)
    {
        var source = Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture + "-synthetic.xlsx");
        var before = SHA256.HashData(File.ReadAllBytes(source));
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-workbook-save-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "new.xlsx");
        var engine = new WorkbookEngine();
        using var main = engine.Open(source);
        var sheet = main.Info.Worksheets[0].Id;
        main.Edit(new(sheet, 5, 1, "text", "Pending main edit", 0));
        using var copy = main.CreateWorkingCopy(sheet, 1);
        copy.Edit(new(sheet, 5, 1, "text", "Pending copy edit", 0));
        try
        {
        var receipt = main.SaveAsNew(target, 1);
        Assert.AreEqual("saved", receipt.Status); Assert.IsFalse(receipt.Rebound); Assert.IsTrue(receipt.SourceStillDirty);
        Assert.AreEqual(1, receipt.SavedRevision); Assert.AreEqual(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(target))), receipt.DestinationSha256);
        using (var reopened = engine.Open(target))
        {
            Assert.AreEqual("Pending main edit", reopened.ReadViewport(reopened.Info.Worksheets[0].Id, 5, 1, 1, 1).Cells[0].Value);
            Assert.AreEqual(main.Info.Creator, reopened.Info.Creator); Assert.AreEqual(main.Info.Title, reopened.Info.Title);
            for (var i = 0; i < main.Info.Worksheets.Count; i++)
            {
                var a = main.ReadViewport(main.Info.Worksheets[i].Id, 1, 1, 20, 6);
                var b = reopened.ReadViewport(reopened.Info.Worksheets[i].Id, 1, 1, 20, 6);
                CollectionAssert.AreEqual(a.Cells.ToArray(), b.Cells.ToArray());
            }
        }
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(source)));
        Assert.IsTrue(main.Info.IsDirty);
        Assert.AreEqual(1, main.Info.Revision);
        Assert.AreEqual(1, main.Info.LastExportedRevision); Assert.AreEqual(receipt.DestinationSha256, main.Info.LastExportedSha256);
        var second = Path.Combine(folder, "second.xlsx"); var secondReceipt = main.SaveAsNew(second, 1);
        Assert.AreEqual(receipt.DestinationSha256, secondReceipt.DestinationSha256); CollectionAssert.AreEqual(File.ReadAllBytes(target), File.ReadAllBytes(second));
        Assert.IsTrue(copy.Info.IsDirty);
        Assert.AreEqual("WORKING_COPY_SAVE_UNSUPPORTED", Assert.ThrowsExactly<WorkbookException>(() => copy.SaveAsNew(target, 1)).Code);
        Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(target, 0)).Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => main.SaveAsNew(target, 1, cancelled.Token));
        Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(target, 1)).Code);
        engine.CandidateFaultForTesting = bytes => { bytes[0] ^= 1; return bytes; };
        Assert.AreEqual("CANDIDATE_REJECTED", Assert.ThrowsExactly<WorkbookException>(() => main.SaveAsNew(Path.Combine(folder, "invalid.xlsx"), 1)).Code);
        Assert.IsFalse(File.Exists(Path.Combine(folder, "invalid.xlsx")));
        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(source)));
        }
        finally { foreach (var file in Directory.GetFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
}
