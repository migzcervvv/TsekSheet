using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class SourceLinkedFieldReviewTests
{
    private string _root = null!;
    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "TsekSheet-linked-review-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }
    [TestCleanup]
    public void Cleanup()
    {
        // Direct artificial files only; no recursive cleanup or user originals.
        foreach (var file in Directory.GetFiles(_root)) File.Delete(file);
        Directory.Delete(_root);
    }
    private string Source(string extension = ".pdf")
    {
        var header = extension switch
        {
            ".png" => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
            ".jpg" => new byte[] { 255, 216, 255, 224, 0, 16, 74, 70 },
            _ => Encoding.ASCII.GetBytes("%PDF-1.7\n% artificial triage only\n")
        };
        var bytes = new byte[512];
        header.CopyTo(bytes, 0);
        var path = Path.Combine(_root, Guid.NewGuid().ToString("N") + extension);
        File.WriteAllBytes(path, bytes);
        return path;
    }
    private static Dictionary<string, string> Values(WorkflowProfile p) => new()
    {
        [p.EntityField] = "  Artificial <script>organization</script>  ",
        [p.IdentifierFields[0]] = "  =SUM(A1:A2)  ",
        [p.ContextFields[0]] = "+00125.5000",
        [p.ContextFields[1]] = "2024-02-29"
    };
    private static SourceLinkedDraftField[] Drafts(WorkflowProfile p, RegisteredSourcePage source) =>
        Values(p).Select(v => new SourceLinkedDraftField(v.Key, v.Value, source)).ToArray();
    private static FieldReviewBinding Binding(SourceLinkedFieldReview review, string key) =>
        review.Snapshot.Review.Fields.Single(f => f.Key == key).Binding;
    private static void VerifyAll(SourceLinkedFieldReview review)
    {
        foreach (var key in review.Snapshot.Review.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
            review.Verify(Binding(review, key));
    }
    private static string State(SourceLinkedFieldReview review) => JsonSerializer.Serialize(review.Snapshot.Review);
    private static void ReviewError(string code, Action action)
    {
        var error = Assert.ThrowsExactly<FieldReviewException>(action);
        Assert.AreEqual(code, error.Code);
        Assert.IsNull(error.InnerException);
    }
    private static void SourceError(string code, Action action)
    {
        var error = Assert.ThrowsExactly<DocumentSourceException>(action);
        Assert.AreEqual(code, error.Code);
        Assert.IsNull(error.InnerException);
    }
    private static void Run(SourceLinkedFieldReview review, WorkflowProfile p, RegisteredSourcePage source,
        string operation, CancellationToken token = default)
    {
        var binding = Binding(review, p.EntityField);
        switch (operation)
        {
            case "verify": review.Verify(binding, token); break;
            case "correct": review.Correct(binding, "Corrected artificial entity", source, token); break;
            case "retract": review.Retract(binding, token); break;
            case "validate": review.Validate(binding.Review, token); break;
            case "project": review.Project(binding.Review, token); break;
            default: Assert.Fail("Unknown artificial operation."); break;
        }
    }

    [TestMethod]
    [DataRow("synthetic-payables", ".pdf")]
    [DataRow("synthetic-laboratory", ".pdf")]
    [DataRow("synthetic-payables", ".png")]
    [DataRow("synthetic-laboratory", ".png")]
    [DataRow("synthetic-payables", ".jpg")]
    [DataRow("synthetic-laboratory", ".jpg")]
    public void LiveReviewRequiresExplicitVerificationAndRetainsTypedLiteralProvenance(string profileId, string extension)
    {
        var p = WorkflowProfiles.Get(profileId);
        var path = Source(extension);
        var original = File.ReadAllBytes(path);
        using var sources = new LocalDocumentSources();
        var registration = sources.Register(path);
        var page = new RegisteredSourcePage(registration.Version, extension == ".pdf" ? 10000 : 1);
        var drafts = Drafts(p, page);
        using var review = new SourceLinkedFieldReview(sources, p, drafts);
        drafts[0] = drafts[0] with { Value = "Changed caller array" };
        Assert.IsTrue(review.Snapshot.Review.Fields.Where(f => f.Source != null).All(f => f.Status == FieldReviewStatus.Draft));
        Assert.IsFalse(review.Validate(review.Snapshot.Review.Version).IsValid);
        ReviewError("REVIEW_INCOMPLETE", () => review.Project(review.Snapshot.Review.Version));
        VerifyAll(review);
        var before = State(review);
        var typed = review.Project(review.Snapshot.Review.Version);
        Assert.IsTrue(review.Validate(typed.Version).IsValid);
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(p.Id, typed.Version.ProfileId);
        Assert.AreEqual(WorkflowProfiles.Hash(p), typed.Version.ProfileHash);
        foreach (var field in typed.Fields)
        {
            Assert.AreEqual(Values(p).GetValueOrDefault(field.Key, ""), field.LiteralText);
            if (field.Source == null) Assert.AreEqual("", field.LiteralText);
            else Assert.AreEqual(new SourceEvidence(registration.Version.DocumentId, registration.Version.Sha256, 0, page.PageNumber), field.Source);
        }
        Assert.AreEqual(125.5000m, typed.Fields.Single(f => f.Kind == "decimal").DecimalValue);
        Assert.AreEqual(new DateOnly(2024, 2, 29), typed.Fields.Single(f => f.Kind == "date").DateValue);
        Assert.AreEqual("=SUM(A1:A2)", typed.Fields.Single(f => f.Key == p.IdentifierFields[0]).NormalizedText);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<TypedReviewField>)typed.Fields).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<FieldReviewSnapshot>)review.Snapshot.Review.Fields).Clear());
        Assert.IsFalse(JsonSerializer.Serialize(new { review.Snapshot, typed }).Contains(_root, StringComparison.OrdinalIgnoreCase));
        review.Dispose();
        // Review disposal does not dispose the shared registration or write files.
        sources.IssueEvidence(registration.Version, page.PageNumber);
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void CorrectionsEvenToSameValueOrAnotherSourceAndRetractionsRequireRenewedVerification(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var sources = new LocalDocumentSources();
        var first = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        var second = new RegisteredSourcePage(sources.Register(Source()).Version, 2);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, first));
        VerifyAll(review);
        var cached = review.Project(review.Snapshot.Review.Version);
        foreach (var page in new[] { first, second })
        {
            var old = Binding(review, p.EntityField);
            var value = review.Snapshot.Review.Fields.Single(f => f.Key == p.EntityField).Value;
            review.Correct(old, value, page);
            var corrected = review.Snapshot.Review.Fields.Single(f => f.Key == p.EntityField);
            Assert.AreEqual(FieldReviewStatus.Corrected, corrected.Status);
            Assert.AreEqual(old.ValueRevision + 1, corrected.ValueRevision);
            Assert.AreEqual(page.Version.DocumentId, corrected.Source!.DocumentId);
            ReviewError("STALE_RECORD_REVIEW", () => review.Project(cached.Version));
            ReviewError("REVIEW_INCOMPLETE", () => review.Project(review.Snapshot.Review.Version));
            ReviewError("STALE_RECORD_REVIEW", () => review.Verify(old));
            review.Verify(corrected.Binding);
        }
        review.Retract(Binding(review, p.EntityField));
        Assert.IsFalse(review.Validate(review.Snapshot.Review.Version).IsValid);
        ReviewError("REVIEW_INCOMPLETE", () => review.Project(review.Snapshot.Review.Version));
        review.Verify(Binding(review, p.EntityField));
        Assert.IsNotNull(review.Project(review.Snapshot.Review.Version));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void ForeignStaleAndForgedCommandsCannotChangeTheOwnedReview(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var sources = new LocalDocumentSources();
        using var foreignSources = new LocalDocumentSources();
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        using var other = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        var binding = Binding(review, p.EntityField);
        var before = State(review);
        foreach (var wrong in new[]
        {
            Binding(other, p.EntityField), binding with { Review = binding.Review with { Revision = 99 } },
            binding with { Review = binding.Review with { ProfileHash = new string('A', 64) } },
            binding with { Review = binding.Review with { ProfileId = "foreign" } }
        }) ReviewError("STALE_RECORD_REVIEW", () => review.Verify(wrong));
        ReviewError("STALE_RECORD_REVIEW", () => review.Verify(null!));
        ReviewError("STALE_FIELD_REVIEW", () => review.Verify(binding with { ValueRevision = 1 }));
        ReviewError("STALE_FIELD_REVIEW", () => review.Verify(binding with { Source = binding.Source! with { PageNumber = 2 } }));
        ReviewError("UNKNOWN_REVIEW_FIELD", () => review.Verify(binding with { Key = "unknown" }));
        SourceError("SOURCE_NOT_REGISTERED", () => review.Correct(binding, "new", new(foreignSources.Register(Source()).Version, 1)));
        SourceError("STALE_SOURCE_VERSION", () => review.Correct(binding, "new", page with { Version = page.Version with { Revision = 1 } }));
        SourceError("STALE_SOURCE_VERSION", () => review.Correct(binding, "new", page with { Version = page.Version with { Sha256 = new string('0', 64) } }));
        SourceError("INVALID_SOURCE_PAGE", () => review.Correct(binding, "new", page with { PageNumber = 0 }));
        ReviewError("INVALID_REVIEW_SOURCE", () => review.Correct(binding, "new", null!));
        ReviewError("INVALID_REVIEW_VALUE", () => review.Correct(binding, new string('x', 1000), page));
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(SourceLinkedReviewStatus.Active, review.Snapshot.Status);
        review.Verify(binding);
        before = State(review);
        ReviewError("FIELD_ALREADY_VERIFIED", () => review.Verify(Binding(review, p.EntityField)));
        Assert.AreEqual(before, State(review));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "verify", "edit")]
    [DataRow("synthetic-laboratory", "project", "edit")]
    [DataRow("synthetic-payables", "correct", "replace")]
    [DataRow("synthetic-laboratory", "validate", "replace")]
    [DataRow("synthetic-payables", "retract", "missing")]
    [DataRow("synthetic-laboratory", "project", "missing")]
    [DataRow("synthetic-payables", "verify", "locked")]
    [DataRow("synthetic-laboratory", "project", "locked")]
    [DataRow("synthetic-payables", "correct", "release")]
    [DataRow("synthetic-laboratory", "project", "release")]
    [DataRow("synthetic-payables", "validate", "dispose")]
    [DataRow("synthetic-laboratory", "project", "dispose")]
    public void UnavailableOrChangedRetainedSourcesInvalidateWithoutCommittingReview(string profileId, string operation, string failure)
    {
        var p = WorkflowProfiles.Get(profileId);
        var path = Source();
        var original = File.ReadAllBytes(path);
        var timestamp = File.GetLastWriteTimeUtc(path);
        using var sources = new LocalDocumentSources();
        var page = new RegisteredSourcePage(sources.Register(path).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        if (operation != "verify") VerifyAll(review);
        var before = State(review);
        FileStream? held = null;
        try
        {
            switch (failure)
            {
                case "edit":
                    var changed = original.ToArray(); changed[^1] = 42;
                    File.WriteAllBytes(path, changed); File.SetLastWriteTimeUtc(path, timestamp); break;
                case "replace":
                    File.Move(path, path + ".old"); File.WriteAllBytes(path, original);
                    File.SetLastWriteTimeUtc(path, timestamp); break;
                case "missing": File.Move(path, path + ".old"); break;
                case "locked": held = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None); break;
                case "release": sources.Release(page.Version); break;
                case "dispose": sources.Dispose(); break;
            }
            var error = Assert.ThrowsExactly<DocumentSourceException>(() => Run(review, p, page, operation));
            Assert.IsFalse(error.Message.Contains(_root, StringComparison.OrdinalIgnoreCase));
            Assert.IsNull(error.InnerException);
            Assert.AreEqual(before, State(review));
            Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, review.Snapshot.Status);
        }
        finally { held?.Dispose(); }
        // Restoration or a new registration cannot revive this exact review.
        if (failure == "missing") File.Move(path + ".old", path);
        if (failure == "replace") { File.Delete(path); File.Move(path + ".old", path); }
        if (failure == "edit") { File.WriteAllBytes(path, original); File.SetLastWriteTimeUtc(path, timestamp); }
        ReviewError("SOURCE_REVIEW_INVALIDATED", () => review.Project(review.Snapshot.Review.Version));
        if (failure != "dispose") sources.Register(path);
        ReviewError("SOURCE_REVIEW_INVALIDATED", () => Run(review, p, page, operation));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [TestMethod]
    [DataRow("verify", 1)]
    [DataRow("verify", 2)]
    [DataRow("correct", 3)]
    [DataRow("retract", 2)]
    [DataRow("validate", 2)]
    [DataRow("project", 2)]
    public void CancellationAtPreOrFinalReadRollsBackStagedStateAndPermitsRetry(string operation, int cancelAt)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var path = Source();
        var original = File.ReadAllBytes(path);
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(path).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        if (operation != "verify") VerifyAll(review);
        var before = State(review);
        using var cancellation = new CancellationTokenSource();
        var reads = 0;
        reader.AfterRead = _ => { if (++reads == cancelAt) cancellation.Cancel(); };
        Assert.ThrowsExactly<OperationCanceledException>(() => Run(review, p, page, operation, cancellation.Token));
        Assert.AreEqual(cancelAt, reads);
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(SourceLinkedReviewStatus.Active, review.Snapshot.Status);
        reader.AfterRead = null;
        Run(review, p, page, operation);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [TestMethod]
    [DataRow("verify", "release", 2)]
    [DataRow("correct", "release", 3)]
    [DataRow("project", "dispose", 2)]
    [DataRow("retract", "dispose", 2)]
    public void ReleaseOrRegistryDisposalDuringFinalReadCannotCommitOrReturnTypedData(string operation, string failure, int failAt)
    {
        var p = WorkflowProfiles.Get("synthetic-laboratory");
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        if (operation != "verify") VerifyAll(review);
        var before = State(review);
        var reads = 0;
        reader.AfterRead = _ =>
        {
            if (++reads != failAt) return;
            if (failure == "release") sources.Release(page.Version); else sources.Dispose();
        };
        SourceError(failure == "release" ? "SOURCE_NOT_REGISTERED" : "SOURCE_SESSION_CLOSED",
            () => Run(review, p, page, operation));
        Assert.AreEqual(failAt, reads);
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, review.Snapshot.Status);
    }

    [TestMethod]
    [DataRow("verify")]
    [DataRow("correct")]
    [DataRow("project")]
    public void SourceEditBetweenPreAndFinalCheckCannotCommit(string operation)
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var path = Source();
        var original = File.ReadAllBytes(path);
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(path).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        if (operation != "verify") VerifyAll(review);
        var before = State(review);
        var reads = 0;
        reader.AfterRead = _ =>
        {
            if (++reads != 1) return;
            var bytes = original.ToArray(); bytes[^1] = 55; File.WriteAllBytes(path, bytes);
        };
        SourceError("SOURCE_INVALIDATED", () => Run(review, p, page, operation));
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, review.Snapshot.Status);
        File.WriteAllBytes(path, original);
        ReviewError("SOURCE_REVIEW_INVALIDATED", () => review.Project(review.Snapshot.Review.Version));
    }

    [TestMethod]
    public void FinalReadChecksEarlierRegistrationsAndEveryFieldSource()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var first = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        var second = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        var drafts = Drafts(p, first);
        drafts[^1] = drafts[^1] with { Source = second };
        using var review = new SourceLinkedFieldReview(sources, p, drafts);
        VerifyAll(review);
        var before = State(review);
        var reads = 0;
        reader.AfterRead = _ => { if (++reads == 4) sources.Release(first.Version); };
        SourceError("SOURCE_NOT_REGISTERED", () => review.Project(review.Snapshot.Review.Version));
        Assert.AreEqual(4, reads);
        Assert.AreEqual(before, State(review));
        Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, review.Snapshot.Status);
    }

    [TestMethod]
    public void MissingOptionalFieldsNeedNoSourceButSuppliedBlankFieldsMustBeVerified()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var sources = new LocalDocumentSources();
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        var drafts = Drafts(p, page).Append(new(p.Fields[1].Key, "", page)).ToArray();
        using var review = new SourceLinkedFieldReview(sources, p, drafts);
        foreach (var field in review.Snapshot.Review.Fields.Where(f => f.Source != null && f.Key != p.Fields[1].Key))
            review.Verify(Binding(review, field.Key));
        ReviewError("REVIEW_INCOMPLETE", () => review.Project(review.Snapshot.Review.Version));
        review.Verify(Binding(review, p.Fields[1].Key));
        Assert.IsNotNull(review.Project(review.Snapshot.Review.Version));
        using var empty = new SourceLinkedFieldReview(sources, p, []);
        ReviewError("SOURCE_REQUIRED", () => empty.Verify(Binding(empty, p.EntityField)));
        Assert.IsFalse(empty.Validate(empty.Snapshot.Review.Version).IsValid);
        sources.Dispose();
        SourceError("SOURCE_SESSION_CLOSED", () => empty.Validate(empty.Snapshot.Review.Version));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void VerifiedInvalidTypesCannotProjectAndDoNotCommitState(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var sources = new LocalDocumentSources();
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        VerifyAll(review);
        foreach (var value in new[] { "=1+2", "1,000", "-1" })
        {
            review.Correct(Binding(review, p.ContextFields[0]), value, page);
            review.Verify(Binding(review, p.ContextFields[0]));
            var before = State(review);
            var error = Assert.ThrowsExactly<WorkbookException>(() => review.Project(review.Snapshot.Review.Version));
            Assert.AreEqual("INVALID_RECORD", error.Code);
            Assert.IsFalse(error.Message.Contains(value, StringComparison.Ordinal));
            Assert.AreEqual(before, State(review));
        }
    }

    [TestMethod]
    public void ConstructionRequiresExactLiveRegistryPagesAndBoundedConfiguredDrafts()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        using var sources = new LocalDocumentSources();
        using var other = new LocalDocumentSources();
        var path = Source(".png");
        var page = new RegisteredSourcePage(sources.Register(path).Version, 1);
        SourceError("SOURCE_NOT_REGISTERED", () => new SourceLinkedFieldReview(other, p, Drafts(p, page)));
        SourceError("STALE_SOURCE_VERSION", () => new SourceLinkedFieldReview(sources, p, Drafts(p, page with { Version = page.Version with { Revision = 1 } })));
        SourceError("INVALID_SOURCE_PAGE", () => new SourceLinkedFieldReview(sources, p, Drafts(p, page with { PageNumber = 2 })));
        ReviewError("INVALID_REVIEW_INPUT", () => new SourceLinkedFieldReview(sources, p, null!));
        ReviewError("INVALID_REVIEW_INPUT", () => new SourceLinkedFieldReview(sources, p, [null!]));
        var draft = Drafts(p, page)[0];
        ReviewError("INVALID_REVIEW_INPUT", () => new SourceLinkedFieldReview(sources, p, [draft, draft]));
        ReviewError("INVALID_REVIEW_INPUT", () => new SourceLinkedFieldReview(sources, p, Enumerable.Repeat(draft, p.Fields.Count + 1).ToArray()));
        ReviewError("UNKNOWN_REVIEW_FIELD", () => new SourceLinkedFieldReview(sources, p, [draft with { Key = "foreign" }]));
        ReviewError("INVALID_REVIEW_SOURCE", () => new SourceLinkedFieldReview(sources, p, [draft with { Source = null! }]));
        ReviewError("INVALID_REVIEW_VALUE", () => new SourceLinkedFieldReview(sources, p, [draft with { Value = "\u0001" }]));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => new SourceLinkedFieldReview(sources, p, Drafts(p, page), cancellation.Token));
        sources.Release(page.Version);
        SourceError("SOURCE_NOT_REGISTERED", () => new SourceLinkedFieldReview(sources, p, Drafts(p, page)));
        sources.Dispose();
        SourceError("SOURCE_SESSION_CLOSED", () => new SourceLinkedFieldReview(sources, p, []));
    }

    [TestMethod]
    public void ConstructionRechecksPreviouslyIssuedEvidenceAndHandlesLateCancellation()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var path = Source();
        var original = File.ReadAllBytes(path);
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(path).Version, 1);
        var reads = 0;
        reader.AfterRead = _ =>
        {
            if (++reads != 4) return;
            var changed = original.ToArray(); changed[^1] = 77; File.WriteAllBytes(path, changed);
        };
        SourceError("SOURCE_INVALIDATED", () => new SourceLinkedFieldReview(sources, p, Drafts(p, page)));
        reader.AfterRead = null;
        File.WriteAllBytes(path, original);
        page = new(sources.Register(path).Version, 1);
        using var cancellation = new CancellationTokenSource();
        reads = 0;
        reader.AfterRead = _ => { if (++reads == 5) cancellation.Cancel(); };
        Assert.ThrowsExactly<OperationCanceledException>(() => new SourceLinkedFieldReview(sources, p, Drafts(p, page), cancellation.Token));
        reader.AfterRead = null;
        using var retry = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
    }

    [TestMethod]
    [DataRow("verify")]
    [DataRow("project")]
    public void ReviewDisposalDuringFinalReadCannotPublishAndLeavesSharedSourcesOpen(string operation)
    {
        var p = WorkflowProfiles.Get("synthetic-laboratory");
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        if (operation != "verify") VerifyAll(review);
        var reads = 0;
        reader.AfterRead = _ => { if (++reads == 2) review.Dispose(); };
        ReviewError("SOURCE_REVIEW_CLOSED", () => Run(review, p, page, operation));
        reader.AfterRead = null;
        review.Dispose();
        ReviewError("SOURCE_REVIEW_CLOSED", () => _ = review.Snapshot);
        sources.IssueEvidence(page.Version, 1);
    }

    [TestMethod]
    public async Task CommandsSerializeAndReentrantCommandsCannotBypassStaging()
    {
        var p = WorkflowProfiles.Get("synthetic-payables");
        var reader = new ObservingReader();
        using var sources = new LocalDocumentSources(reader);
        var page = new RegisteredSourcePage(sources.Register(Source()).Version, 1);
        using var review = new SourceLinkedFieldReview(sources, p, Drafts(p, page));
        var binding = Binding(review, p.EntityField);
        var before = State(review);
        reader.AfterRead = _ =>
        {
            ReviewError("SOURCE_REVIEW_BUSY", () => review.Verify(binding));
            Assert.AreEqual(before, State(review));
        };
        review.Verify(binding);
        reader.AfterRead = null;
        review.Retract(Binding(review, p.EntityField));
        binding = Binding(review, p.EntityField);
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        var reads = 0;
        reader.AfterRead = _ =>
        {
            if (Interlocked.Increment(ref reads) != 1) return;
            entered.Set();
            Assert.IsTrue(proceed.Wait(TimeSpan.FromSeconds(10)), "Artificial source read release timed out.");
        };
        var first = Task.Run(() => review.Verify(binding));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)), "Artificial source read entry timed out.");
            var second = Task.Run(() => ReviewError("STALE_RECORD_REVIEW", () => review.Verify(binding)));
            proceed.Set();
            await Task.WhenAll(first, second);
        }
        finally { proceed.Set(); }
        Assert.AreEqual(FieldReviewStatus.Verified, review.Snapshot.Review.Fields.Single(f => f.Key == p.EntityField).Status);
    }

    private sealed class ObservingReader : ILocalDocumentReader
    {
        public Action<string>? AfterRead { get; set; }
        public DocumentObservation Read(string selectedPath, long maxBytes, CancellationToken token)
        {
            var observation = new WindowsDocumentReader().Read(selectedPath, maxBytes, token);
            AfterRead?.Invoke(selectedPath);
            return observation;
        }
    }
}
