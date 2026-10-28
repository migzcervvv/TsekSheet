using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
[DoNotParallelize]
public sealed class NewFileCommitTests
{
    private static readonly byte[] Bytes = "synthetic candidate"u8.ToArray();
    private static readonly string Hash = Convert.ToHexString(SHA256.HashData(Bytes));
    private sealed class Folder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-new-save-" + Guid.NewGuid().ToString("N"));
        public Folder() => Directory.CreateDirectory(Path);
        public string Target => System.IO.Path.Combine(Path, "new.xlsx");
        public void Dispose() { foreach (var file in Directory.GetFiles(Path)) File.Delete(file); Directory.Delete(Path); }
    }

    [TestMethod]
    public void RepeatedCommitsPreserveBytesAndCleanStage()
    {
        using var folder = new Folder();
        for (var i = 0; i < 100; i++)
        {
            var target = Path.Combine(folder.Path, $"export-{i}.xlsx");
            NewFileCommit.Commit(target, Bytes, Hash, default);
            CollectionAssert.AreEqual(Bytes, File.ReadAllBytes(target));
        }
        Assert.IsEmpty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [TestMethod]
    public void RepeatedConcurrentCreationRejectsWithoutOverwrite()
    {
        using var folder = new Folder();
        for (var i = 0; i < 100; i++)
        {
            var target = Path.Combine(folder.Path, $"race-{i}.xlsx");
            var faults = new CommitFaults { BeforeCommit = () => Task.Run(() => File.WriteAllText(target, "external writer")).GetAwaiter().GetResult() };
            Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(target, Bytes, Hash, default, faults)).Code);
            Assert.AreEqual("external writer", File.ReadAllText(target));
        }
        Assert.IsEmpty(Directory.GetFiles(folder.Path, "*.tmp"));
    }

    [TestMethod]
    public void SimultaneousCreateNewAndCommitHaveExactlyOneWinner()
    {
        using var folder = new Folder();
        for (var i = 0; i < 100; i++)
        {
            var target = Path.Combine(folder.Path, $"simultaneous-{i}.xlsx");
            using var start = new ManualResetEventSlim();
            var other = Task.Run(() =>
            {
                start.Wait();
                try { using var file = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None); file.Write("external"u8); file.Flush(true); return true; }
                catch (IOException) { return false; }
            });
            var saved = false;
            try { NewFileCommit.Commit(target, Bytes, Hash, default, new() { BeforeCommit = start.Set }); saved = true; }
            catch (WorkbookException ex) { Assert.AreEqual("DESTINATION_EXISTS", ex.Code); }
            finally { start.Set(); }
            var external = other.GetAwaiter().GetResult();
            Assert.AreNotEqual(saved, external, "Exactly one create-new operation must win.");
            CollectionAssert.AreEqual(saved ? Bytes : "external"u8.ToArray(), File.ReadAllBytes(target));
        }
    }

    [TestMethod]
    public void ProtectedStageAndAncestorsCannotBeTamperedWith()
    {
        using var folder = new Folder();
        var faults = new CommitFaults { AfterStage = stage =>
        {
            Assert.ThrowsExactly<IOException>(() => File.WriteAllText(stage, "tamper"));
            Assert.ThrowsExactly<IOException>(() => File.Move(stage, stage + "-moved"));
            Assert.ThrowsExactly<IOException>(() => File.Delete(stage));
            Assert.ThrowsExactly<IOException>(() => Directory.Move(folder.Path, folder.Path + "-moved"));
        }};
        NewFileCommit.Commit(folder.Target, Bytes, Hash, default, faults);
        CollectionAssert.AreEqual(Bytes, File.ReadAllBytes(folder.Target));
    }

    [TestMethod]
    public void ExistingAndLockedDestinationIsNeverReplaced()
    {
        using var folder = new Folder(); File.WriteAllText(folder.Target, "existing");
        using (var locked = new FileStream(folder.Target, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default)).Code);
        Assert.AreEqual("existing", File.ReadAllText(folder.Target));
    }

    [TestMethod]
    public void DestinationCreatedAndLockedAtCommitBoundaryIsPreserved()
    {
        using var folder = new Folder(); FileStream? other = null;
        try
        {
            var faults = new CommitFaults { BeforeCommit = () => { other = new FileStream(folder.Target, FileMode.CreateNew, FileAccess.Write, FileShare.None); other.Write("external"u8); other.Flush(true); } };
            Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default, faults)).Code);
        }
        finally { other?.Dispose(); }
        Assert.AreEqual("external", File.ReadAllText(folder.Target));
    }

    [TestMethod]
    public void OtherProcessCreationAtBoundaryRejectsWithoutOverwrite()
    {
        using var folder = new Folder();
        for (var i = 0; i < 20; i++)
        {
            var target = Path.Combine(folder.Path, $"process-{i}.xlsx");
            var faults = new CommitFaults { BeforeCommit = () =>
            {
                var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", "/d /c echo external>\"" + target + "\"") { UseShellExecute = false, CreateNoWindow = true };
                using var child = System.Diagnostics.Process.Start(start)!;
                Assert.IsTrue(child.WaitForExit(10000)); Assert.AreEqual(0, child.ExitCode);
            }};
            Assert.AreEqual("DESTINATION_EXISTS", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(target, Bytes, Hash, default, faults)).Code);
            Assert.AreEqual("external", File.ReadAllText(target).Trim());
        }
    }

    [TestMethod]
    public void JunctionDestinationIsRejected()
    {
        using var folder = new Folder();
        var real = Path.Combine(folder.Path, "real"); var link = Path.Combine(folder.Path, "link"); Directory.CreateDirectory(real);
        try
        {
            var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{real}\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using var child = System.Diagnostics.Process.Start(start)!; Assert.IsTrue(child.WaitForExit(10000)); Assert.AreEqual(0, child.ExitCode);
            Assert.AreEqual("UNSUPPORTED_DESTINATION", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(Path.Combine(link, "new.xlsx"), Bytes, Hash, default)).Code);
            Assert.IsEmpty(Directory.GetFiles(real));
        }
        finally { if (Directory.Exists(link)) Directory.Delete(link); Directory.Delete(real); }
    }

    [TestMethod]
    public void DirectoryPermissionDenialDoesNotWriteDestination()
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException();
        using var folder = new Folder(); var directory = new DirectoryInfo(folder.Path);
        var original = System.IO.FileSystemAclExtensions.GetAccessControl(directory);
        var denied = System.IO.FileSystemAclExtensions.GetAccessControl(directory);
        var rule = new System.Security.AccessControl.FileSystemAccessRule(System.Security.Principal.WindowsIdentity.GetCurrent().User!, System.Security.AccessControl.FileSystemRights.CreateFiles, System.Security.AccessControl.AccessControlType.Deny);
        denied.AddAccessRule(rule);
        try
        {
            System.IO.FileSystemAclExtensions.SetAccessControl(directory, denied);
            Assert.AreEqual(5, Assert.ThrowsExactly<System.ComponentModel.Win32Exception>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default)).NativeErrorCode);
            Assert.IsFalse(File.Exists(folder.Target)); Assert.IsEmpty(Directory.GetFiles(folder.Path));
        }
        finally { System.IO.FileSystemAclExtensions.SetAccessControl(directory, original); }
    }

    [TestMethod]
    [DataRow("stage")][DataRow("validation")][DataRow("commit")][DataRow("cancel")]
    public void PrecommitFailuresDoNotCreateDestination(string phase)
    {
        using var folder = new Folder(); using var cancellation = new CancellationTokenSource();
        var faults = new CommitFaults
        {
            AfterStage = phase == "stage" ? _ => throw new UnauthorizedAccessException("injected") : null,
            BeforeCommit = phase == "commit" ? () => throw new IOException("injected") : phase == "cancel" ? cancellation.Cancel : null
        };
        if (phase == "cancel") Assert.ThrowsExactly<OperationCanceledException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, cancellation.Token, faults));
        else if (phase == "validation") Assert.AreEqual("CANDIDATE_REJECTED", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(folder.Target, Bytes, new string('0', 64), default, faults)).Code);
        else if (phase == "stage") Assert.ThrowsExactly<UnauthorizedAccessException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default, faults));
        else Assert.ThrowsExactly<IOException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default, faults));
        Assert.IsFalse(File.Exists(folder.Target)); Assert.IsEmpty(Directory.GetFiles(folder.Path));
    }

    [TestMethod]
    public void PostcommitFailureIsUncertainAndRetainsDestination()
    {
        using var folder = new Folder();
        Assert.AreEqual("SAVE_UNCERTAIN", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default, new() { AfterCommit = () => throw new IOException("injected") })).Code);
        CollectionAssert.AreEqual(Bytes, File.ReadAllBytes(folder.Target));
    }

    [TestMethod]
    public void CancellationAfterRenameReportsActualCommit()
    {
        using var folder = new Folder(); using var cancellation = new CancellationTokenSource();
        NewFileCommit.Commit(folder.Target, Bytes, Hash, cancellation.Token, new() { AfterCommit = cancellation.Cancel });
        CollectionAssert.AreEqual(Bytes, File.ReadAllBytes(folder.Target));
    }

    [TestMethod]
    public void CleanupFailureRetainsOnlyOwnedStage()
    {
        using var folder = new Folder();
        Assert.AreEqual("TEMP_CLEANUP_FAILED", Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(folder.Target, Bytes, Hash, default, new() { BeforeCommit = () => throw new IOException("injected"), CleanupFailure = true })).Code);
        Assert.IsFalse(File.Exists(folder.Target)); Assert.HasCount(1, Directory.GetFiles(folder.Path, ".tseksheet-*.tmp"));
    }

    [TestMethod]
    [DataRow(@"\\server\share\new.xlsx")][DataRow(@"\\?\C:\new.xlsx")][DataRow(@"C:\new.xlsm")][DataRow(@"C:\folder\..\new.xlsx")][DataRow(@"C:\CON.xlsx")][DataRow(@"C:\new.xlsx:stream")]
    public void UnsupportedPathsRejectedBeforeWrites(string path) => Assert.ThrowsExactly<WorkbookException>(() => NewFileCommit.Commit(path, Bytes, Hash, default));
}
