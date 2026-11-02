using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class LocalDocumentSourcesTests
{
    private string _root = null!;
    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "TsekSheet-source-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    [TestCleanup]
    public void Cleanup()
    {
        // Only direct artificial files owned by this test; never recurse.
        foreach (var file in Directory.GetFiles(_root)) File.Delete(file);
        Directory.Delete(_root);
    }

    private string Source(string extension = ".pdf", int size = 512)
    {
        var header = extension.ToLowerInvariant() switch
        {
            ".png" => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            ".jpg" or ".jpeg" => new byte[] { 255, 216, 255, 224, 0, 16, 74, 70 },
            _ => Encoding.ASCII.GetBytes("%PDF-1.7\n% artificial fixture only\n")
        };
        var bytes = new byte[size];
        header.AsSpan(0, Math.Min(size, header.Length)).CopyTo(bytes);
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(path, bytes);
        return path;
    }
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static DocumentSourceException Reject(string code, Action action)
    {
        var error = Assert.ThrowsExactly<DocumentSourceException>(action);
        Assert.AreEqual(code, error.Code);
        Assert.IsNull(error.InnerException);
        return error;
    }

    [TestMethod]
    [DataRow(".pdf", LocalDocumentKind.Pdf)]
    [DataRow(".PDF", LocalDocumentKind.Pdf)]
    [DataRow(".png", LocalDocumentKind.Png)]
    [DataRow(".jpg", LocalDocumentKind.Jpeg)]
    [DataRow(".jpeg", LocalDocumentKind.Jpeg)]
    public void RegistersAndRechecksArtificialBytesWithoutChangingSources(string extension, LocalDocumentKind kind)
    {
        var path = Source(extension);
        var original = File.ReadAllBytes(path);
        using var sources = new LocalDocumentSources();
        var snapshot = sources.Register(path);
        Assert.AreEqual(kind, snapshot.Kind);
        Assert.AreEqual(original.Length, snapshot.ByteLength);
        Assert.AreNotEqual(Guid.Empty, snapshot.Version.DocumentId);
        Assert.AreEqual(0, snapshot.Version.Revision);
        Assert.AreEqual(Hash(path), snapshot.Version.Sha256);
        Assert.AreEqual(LocalDocumentStatus.Registered, snapshot.Status);
        var evidence = sources.IssueEvidence(snapshot.Version, 1);
        Assert.AreEqual(snapshot.Version.DocumentId, evidence.DocumentId);
        Assert.AreEqual(snapshot.Version.Sha256, evidence.DocumentSha256);
        Assert.AreEqual(0, evidence.DocumentRevision);
        Assert.AreEqual(1, evidence.PageNumber);
        Assert.AreEqual(evidence, sources.IssueEvidence(snapshot.Version, 1));
        Assert.AreEqual(snapshot, sources.Snapshot(snapshot.Version.DocumentId));
        var json = JsonSerializer.Serialize(new { snapshot, evidence });
        Assert.IsFalse(json.Contains(_root, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(json.Contains(Path.GetFileName(path), StringComparison.OrdinalIgnoreCase));
        sources.Release(snapshot.Version);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        // No source handles survive registration, revalidation or release.
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    [TestMethod]
    [DataRow("content")]
    [DataRow("length")]
    [DataRow("identical-replacement")]
    [DataRow("missing")]
    [DataRow("timestamp")]
    [DataRow("signature")]
    public void SourceChangesInvalidateWithoutRestoringOldAuthority(string change)
    {
        var path = Source();
        var original = File.ReadAllBytes(path);
        var writeTime = File.GetLastWriteTimeUtc(path);
        using var sources = new LocalDocumentSources();
        var before = sources.Register(path);
        switch (change)
        {
            case "content":
                var edited = original.ToArray(); edited[^1] = 42;
                File.WriteAllBytes(path, edited); File.SetLastWriteTimeUtc(path, writeTime); break;
            case "length": File.WriteAllBytes(path, original.Concat(new byte[] { 1 }).ToArray()); break;
            case "identical-replacement":
                var creation = File.GetCreationTimeUtc(path);
                File.Move(path, path + ".old");
                File.WriteAllBytes(path, original);
                File.SetCreationTimeUtc(path, creation);
                File.SetLastWriteTimeUtc(path, writeTime);
                Assert.AreEqual(before.Version.Sha256, Hash(path)); break;
            case "missing": File.Delete(path); break;
            case "timestamp": File.SetLastWriteTimeUtc(path, writeTime.AddMinutes(1)); break;
            case "signature": File.WriteAllBytes(path, new byte[original.Length]); break;
        }
        Reject("SOURCE_INVALIDATED", () => sources.IssueEvidence(before.Version, 1));
        var invalid = sources.Snapshot(before.Version.DocumentId);
        Assert.AreEqual(1, invalid.Version.Revision);
        Assert.AreEqual(LocalDocumentStatus.Invalidated, invalid.Status);
        Assert.AreEqual(before.Version.Sha256, invalid.Version.Sha256);
        Reject("STALE_SOURCE_VERSION", () => sources.IssueEvidence(before.Version, 1));
        File.WriteAllBytes(path, original);
        Reject("SOURCE_INVALIDATED", () => sources.IssueEvidence(invalid.Version, 1));
        Reject("STALE_SOURCE_VERSION", () => sources.Release(before.Version));
        sources.Release(invalid.Version);
        var fresh = sources.Register(path);
        Assert.AreNotEqual(before.Version.DocumentId, fresh.Version.DocumentId);
        sources.IssueEvidence(fresh.Version, 1);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void RegistryAndVersionOwnershipCannotBeSubstituted()
    {
        var path = Source();
        using var first = new LocalDocumentSources();
        using var second = new LocalDocumentSources();
        var a = first.Register(path);
        var b = second.Register(path);
        Reject("SOURCE_NOT_REGISTERED", () => second.IssueEvidence(a.Version, 1));
        Reject("SOURCE_NOT_REGISTERED", () => first.Release(b.Version));
        Reject("SOURCE_NOT_REGISTERED", () => first.IssueEvidence(null!, 1));
        Reject("STALE_SOURCE_VERSION", () => first.IssueEvidence(a.Version with { Revision = 1 }, 1));
        Reject("STALE_SOURCE_VERSION", () => first.IssueEvidence(a.Version with { Sha256 = new string('A', 64) }, 1));
        Assert.AreEqual(a, first.Snapshot(a.Version.DocumentId));
        first.Release(a.Version);
        Reject("SOURCE_NOT_REGISTERED", () => first.IssueEvidence(a.Version, 1));
        Reject("SOURCE_NOT_REGISTERED", () => first.Snapshot(a.Version.DocumentId));
        Assert.AreEqual(b, second.Snapshot(b.Version.DocumentId));
    }

    [TestMethod]
    [DataRow(".png", 2)]
    [DataRow(".jpg", 2)]
    [DataRow(".pdf", 0)]
    [DataRow(".pdf", -1)]
    [DataRow(".pdf", 10001)]
    public void PageAssociationBoundsDoNotChangeTheSource(string extension, int page)
    {
        using var sources = new LocalDocumentSources();
        var snapshot = sources.Register(Source(extension));
        Reject("INVALID_SOURCE_PAGE", () => sources.IssueEvidence(snapshot.Version, page));
        Assert.AreEqual(snapshot, sources.Snapshot(snapshot.Version.DocumentId));
    }

    [TestMethod]
    public void PdfPageAssociationIsNotAClaimOfParsedPageCount()
    {
        using var sources = new LocalDocumentSources();
        var snapshot = sources.Register(Source());
        Assert.AreEqual(10000, sources.IssueEvidence(snapshot.Version, 10000).PageNumber);
    }

    [TestMethod]
    [DataRow("relative.pdf")]
    [DataRow("\\\\server\\share\\private.pdf")]
    [DataRow("\\\\?\\C:\\private.pdf")]
    [DataRow("C:\\private.pdf:stream")]
    [DataRow("C:\\..\\private.pdf")]
    [DataRow("C:\\CON.pdf")]
    [DataRow("C:\\folder.\\private.pdf")]
    [DataRow("C:/private.pdf")]
    [DataRow("")]
    [DataRow(null)]
    public void RejectsUnsafeLocationsWithoutLeakingPaths(string? path)
    {
        using var sources = new LocalDocumentSources();
        var error = Reject("UNSUPPORTED_SOURCE", () => sources.Register(path!));
        if (!string.IsNullOrEmpty(path)) Assert.IsFalse(error.ToString().Contains(path, StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    [DataRow(".txt", 512, "UNSUPPORTED_SOURCE_TYPE")]
    [DataRow(".pdf", 0, "SOURCE_SIZE_LIMIT")]
    [DataRow(".png", 7, "SOURCE_SIZE_LIMIT")]
    [DataRow(".pdf", 1025, "SOURCE_SIZE_LIMIT")]
    public void UnsupportedOrOversizedInputsDoNotConsumeRegistrationSlots(string extension, int size, string code)
    {
        using var sources = new LocalDocumentSources(new(1, 1024));
        var path = Source(extension, size);
        var original = File.ReadAllBytes(path);
        Reject(code, () => sources.Register(path));
        sources.Register(Source());
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    [DataRow(".pdf")]
    [DataRow(".png")]
    [DataRow(".jpg")]
    public void ExtensionAloneDoesNotIdentifySupportedContent(string extension)
    {
        var path = Source(extension);
        File.WriteAllBytes(path, new byte[512]);
        using var sources = new LocalDocumentSources();
        Reject("UNSUPPORTED_SOURCE_CONTENT", () => sources.Register(path));
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
    }

    [TestMethod]
    public void MissingAndLockedSelectionsHaveSanitizedErrorsAndNoLeakedHandles()
    {
        using var sources = new LocalDocumentSources(new(1));
        var path = Source();
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var error = Reject("SOURCE_UNAVAILABLE", () => sources.Register(path));
            Assert.IsFalse(error.ToString().Contains(_root, StringComparison.OrdinalIgnoreCase));
        }
        var missing = Path.Combine(_root, "private-missing.pdf");
        var missingError = Reject("SOURCE_UNAVAILABLE", () => sources.Register(missing));
        Assert.IsFalse(missingError.ToString().Contains("private-missing"));
        var snapshot = sources.Register(path);
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Reject("SOURCE_INVALIDATED", () => sources.IssueEvidence(snapshot.Version, 1));
        Assert.AreEqual(LocalDocumentStatus.Invalidated, sources.Snapshot(snapshot.Version.DocumentId).Status);
    }

    [TestMethod]
    public void ExactByteAndRegistrationLimitsReleaseCapacity()
    {
        using var sources = new LocalDocumentSources(new(2, 1024));
        var path = Source(size: 1024);
        var a = sources.Register(path);
        var b = sources.Register(path);
        Assert.AreNotEqual(a.Version.DocumentId, b.Version.DocumentId);
        sources.IssueEvidence(a.Version, 1);
        Reject("SOURCE_LIMIT", () => sources.Register(path));
        sources.Release(a.Version);
        sources.Register(path);
        Reject("SOURCE_LIMIT", () => sources.Register(path));
    }

    [TestMethod]
    [DataRow(0, 1024L)]
    [DataRow(21, 1024L)]
    [DataRow(1, 7L)]
    [DataRow(1, 33554433L)]
    public void LimitsCannotExceedSupportedBounds(int count, long bytes) =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new LocalDocumentSources(new(count, bytes)));

    [TestMethod]
    public void CancelledReadsRetainNoRegistrationOrFileLocks()
    {
        var path = Source(size: 128 * 1024);
        var original = File.ReadAllBytes(path);
        using var cancellation = new CancellationTokenSource();
        var chunks = 0;
        var reader = new WindowsDocumentReader { AfterChunk = _ => { chunks++; cancellation.Cancel(); } };
        using var sources = new LocalDocumentSources(reader, new(1));
        Assert.ThrowsExactly<OperationCanceledException>(() => sources.Register(path, cancellation.Token));
        Assert.AreEqual(1, chunks);
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        var fresh = sources.Register(path);
        Assert.AreEqual(0, fresh.Version.Revision);
        using var preCancelled = new CancellationTokenSource(); preCancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => sources.IssueEvidence(fresh.Version, 1, preCancelled.Token));
        Assert.AreEqual(fresh, sources.Snapshot(fresh.Version.DocumentId));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void RevalidationCancellationIsRetryableAndBlocksEvidenceAfterReaderReturns()
    {
        var path = Source(size: 128 * 1024);
        using var cancellation = new CancellationTokenSource();
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var snapshot = sources.Register(path);
        reader.AfterRead = () => cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => sources.IssueEvidence(snapshot.Version, 1, cancellation.Token));
        Assert.AreEqual(snapshot, sources.Snapshot(snapshot.Version.DocumentId));
        reader.AfterRead = null;
        sources.IssueEvidence(snapshot.Version, 1);
    }

    [TestMethod]
    public void CancellationMidRevalidationRetainsTheVersionAndReleasesHandles()
    {
        var path = Source(size: 128 * 1024);
        var original = File.ReadAllBytes(path);
        using var cancellation = new CancellationTokenSource();
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var snapshot = sources.Register(path);
        reader.AfterChunk = _ => cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => sources.IssueEvidence(snapshot.Version, 1, cancellation.Token));
        Assert.AreEqual(snapshot, sources.Snapshot(snapshot.Version.DocumentId));
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        reader.AfterChunk = null;
        sources.IssueEvidence(snapshot.Version, 1);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void JunctionSourcesAreRejected()
    {
        var path = Source();
        var original = File.ReadAllBytes(path);
        var link = Path.Combine(_root, "redirected");
        // Same bounded, local junction mechanism as NewFileCommitTests. Paths
        // are generated test-owned names, never document-provided shell text.
        var start = new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/d /c mklink /J \"{link}\" \"{_root}\"")
        { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        try
        {
            using var process = System.Diagnostics.Process.Start(start)!;
            if (!process.WaitForExit(10000))
            {
                process.Kill();
                Assert.Fail("Artificial junction setup timed out.");
            }
            Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
            Assert.IsTrue(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
            using var sources = new LocalDocumentSources();
            Reject("UNSUPPORTED_SOURCE", () => sources.Register(Path.Combine(link, Path.GetFileName(path))));
            CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        }
        finally
        {
            // Remove only this verified junction; never recurse into its target.
            if (Directory.Exists(link))
            {
                Assert.IsTrue(File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint));
                Directory.Delete(link);
            }
        }
    }

    [TestMethod]
    public void FailedRevalidationRetainsCapacityUntilExplicitRelease()
    {
        var path = Source();
        using var sources = new LocalDocumentSources(new(1));
        var snapshot = sources.Register(path);
        File.Delete(path);
        Reject("SOURCE_INVALIDATED", () => sources.IssueEvidence(snapshot.Version, 1));
        var other = Source();
        Reject("SOURCE_LIMIT", () => sources.Register(other));
        sources.Release(sources.Snapshot(snapshot.Version.DocumentId).Version);
        sources.Register(other);
    }

    [TestMethod]
    public void ReadHandlesExcludeWritesAndRenamesOnlyDuringObservation()
    {
        var path = Source(size: 128 * 1024);
        var original = File.ReadAllBytes(path);
        var calls = 0;
        var reader = new WindowsDocumentReader
        {
            AfterChunk = _ =>
            {
                calls++;
                Assert.ThrowsExactly<IOException>(() => File.Open(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite).Dispose());
                Assert.ThrowsExactly<IOException>(() => File.Move(path, path + ".moved"));
                Assert.ThrowsExactly<IOException>(() => Directory.Move(_root, _root + "-moved"));
            }
        };
        using var sources = new LocalDocumentSources(reader);
        var snapshot = sources.Register(path);
        sources.IssueEvidence(snapshot.Version, 1);
        Assert.IsTrue(calls >= 4);
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    public void ReleaseOrDisposalDuringFinalReadCannotReturnEvidence()
    {
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var snapshot = sources.Register(Source());
        reader.AfterRead = () => sources.Release(snapshot.Version);
        Reject("SOURCE_NOT_REGISTERED", () => sources.IssueEvidence(snapshot.Version, 1));
        reader.AfterRead = null;
        snapshot = sources.Register(Source());
        reader.AfterRead = sources.Dispose;
        Reject("SOURCE_SESSION_CLOSED", () => sources.IssueEvidence(snapshot.Version, 1));
    }

    [TestMethod]
    public void DisposeIsIdempotentAndClosesEveryPublicOperation()
    {
        var path = Source();
        var sources = new LocalDocumentSources();
        var snapshot = sources.Register(path);
        sources.Dispose(); sources.Dispose();
        Reject("SOURCE_SESSION_CLOSED", () => sources.Register(path));
        Reject("SOURCE_SESSION_CLOSED", () => sources.IssueEvidence(snapshot.Version, 1));
        Reject("SOURCE_SESSION_CLOSED", () => sources.Release(snapshot.Version));
        Reject("SOURCE_SESSION_CLOSED", () => sources.Snapshot(snapshot.Version.DocumentId));
        using var exclusive = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    }

    private sealed class ObservingReader : ILocalDocumentReader
    {
        public Action? AfterRead { get; set; }
        public Action<int>? AfterChunk { get; set; }
        public DocumentObservation Read(string selectedPath, long maxBytes, CancellationToken token)
        {
            var result = new WindowsDocumentReader { AfterChunk = AfterChunk }.Read(selectedPath, maxBytes, token);
            AfterRead?.Invoke();
            return result;
        }
    }
}
