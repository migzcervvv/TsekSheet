using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class SourceLinkedMappingTests
{
    [TestMethod]
    [DataRow("synthetic-payables", ".pdf")]
    [DataRow("synthetic-laboratory", ".pdf")]
    [DataRow("synthetic-payables", ".png")]
    [DataRow("synthetic-laboratory", ".png")]
    [DataRow("synthetic-payables", ".jpg")]
    [DataRow("synthetic-laboratory", ".jpg")]
    public void LiveSourcesMapIndependentlyWithImmutableTypedLiteralProvenance(string profileId, string extension)
    {
        using var f = new Fixture(profileId, extension);
        var state = f.State;
        var info = f.Session.Current;
        var cells = f.Cells;
        var first = f.Preview();
        var second = f.Preview(f.Destination(1));
        Assert.AreEqual("Artificial first", first.WorksheetName);
        Assert.AreEqual("Artificial shifted", second.WorksheetName);
        Assert.AreNotEqual(first.Destination.WorksheetId, second.Destination.WorksheetId);
        Assert.AreEqual(22, first.Sections[1].SubtotalRow);
        Assert.AreEqual(42, second.Sections[1].SubtotalRow);
        Assert.AreEqual(f.Review.Snapshot.Review.Version, first.Review);
        for (var index = 0; index < f.Profile.Fields.Count; index++)
        {
            var field = first.Fields[index];
            Assert.AreEqual(index + 1, field.Column);
            Assert.AreEqual(f.Profile.MaxColumns - index, second.Fields[index].Column);
            Assert.AreEqual(field.Value, second.Fields[index].Value);
            Assert.AreEqual(f.Profile.Fields[index].Header, field.Header);
            Assert.AreEqual(f.Values.GetValueOrDefault(field.Value.Key, ""), field.Value.LiteralText);
            Assert.AreEqual(f.Values.ContainsKey(field.Value.Key)
                ? new SourceEvidence(f.Page.Version.DocumentId, f.Page.Version.Sha256, 0, f.Page.PageNumber) : null,
                field.Value.Source);
        }
        Assert.AreEqual("  =SUM(A1:A2)  ", first.Fields[1].Value.LiteralText);
        Assert.AreEqual("=SUM(A1:A2)", first.Fields[1].Value.NormalizedText);
        Assert.AreEqual("text", first.Fields[1].Value.Kind);
        Assert.AreEqual(125.5000m, first.Fields.Single(v => v.Value.Kind == "decimal").Value.DecimalValue);
        Assert.AreEqual(new DateOnly(2024, 2, 29), first.Fields.Single(v => v.Value.Kind == "date").Value.DateValue);
        Assert.IsNull(first.Fields.Single(v => v.Value.Key == f.Profile.IdentifierFields[1]).Value.Source);
        Assert.IsFalse(first.CanApply);
        Assert.IsFalse(second.CanApply);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<VerifiedMappedField>)first.Fields).Clear());
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<MappingSection>)first.Sections).Clear());
        Assert.AreEqual(JsonSerializer.Serialize(first), JsonSerializer.Serialize(f.Preview()));
        Assert.IsFalse(JsonSerializer.Serialize(first).Contains(f.Root, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(info, f.Session.Current);
        Assert.AreEqual(cells, f.Cells);
        CollectionAssert.AreEqual(f.Original, File.ReadAllBytes(f.Path));
        using (File.Open(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [TestMethod]
    [DataRow("session", "INVALID_MAPPING_DESTINATION")]
    [DataRow("source", "INVALID_MAPPING_DESTINATION")]
    [DataRow("sheet", "UNKNOWN_WORKSHEET")]
    [DataRow("revision", "STALE_REVISION")]
    [DataRow("hash", "STALE_PROFILE")]
    [DataRow("profile", "UNKNOWN_PROFILE")]
    [DataRow("other-profile", "STALE_PROFILE")]
    [DataRow("empty-session", "INVALID_MAPPING_REQUEST")]
    [DataRow("empty-sheet", "INVALID_MAPPING_REQUEST")]
    [DataRow("negative-revision", "INVALID_MAPPING_REQUEST")]
    [DataRow("null", "INVALID_MAPPING_REQUEST")]
    public void WrongDestinationsCannotReadOrChangeEitherAuthority(string scenario, string code)
    {
        using var f = new Fixture();
        var target = f.Destination();
        var other = WorkflowProfiles.Get("synthetic-laboratory");
        target = scenario switch
        {
            "session" => target with { SessionId = Guid.NewGuid() },
            "source" => target with { SourceSha256 = new string('C', 64) },
            "sheet" => target with { WorksheetId = Guid.NewGuid() },
            "revision" => target with { Revision = 1 },
            "hash" => target with { ProfileHash = "stale" },
            "profile" => target with { ProfileId = "unknown" },
            "other-profile" => target with { ProfileId = other.Id, ProfileHash = WorkflowProfiles.Hash(other) },
            "empty-session" => target with { SessionId = Guid.Empty },
            "empty-sheet" => target with { WorksheetId = Guid.Empty },
            "negative-revision" => target with { Revision = -1 },
            _ => null!
        };
        var state = f.State;
        var info = f.Session.Current;
        var reads = f.Reader.Reads;
        Error(code, () => f.Review.PreviewMapping(f.Session, target, f.Review.Snapshot.Review.Version));
        Assert.AreEqual(0, f.Session.Reads);
        Assert.AreEqual(reads, f.Reader.Reads);
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(info, f.Session.Current);
    }

    [TestMethod]
    [DataRow("draft", "REVIEW_INCOMPLETE")]
    [DataRow("corrected", "REVIEW_INCOMPLETE")]
    [DataRow("retracted", "REVIEW_INCOMPLETE")]
    [DataRow("stale", "STALE_RECORD_REVIEW")]
    [DataRow("foreign", "STALE_RECORD_REVIEW")]
    [DataRow("forged-version", "STALE_RECORD_REVIEW")]
    [DataRow("null", "STALE_RECORD_REVIEW")]
    [DataRow("missing-required", "REVIEW_INCOMPLETE")]
    [DataRow("invalid-type", "INVALID_RECORD")]
    public void CachedObservationsCannotBypassLiveReviewOrFieldConstraints(string scenario, string code)
    {
        using var f = new Fixture(verified: scenario != "draft");
        var expected = f.Review.Snapshot.Review.Version;
        // Retaining typed or display observations never grants the caller authority.
        if (scenario != "draft") _ = f.Review.Project(expected);
        var field = f.Review.Snapshot.Review.Fields[0];
        if (scenario == "retracted") f.Review.Retract(field.Binding);
        if (scenario is "corrected" or "stale") f.Review.Correct(field.Binding, field.Value, f.Page);
        if (scenario is "missing-required" or "invalid-type")
        {
            var amount = f.Review.Snapshot.Review.Fields.Single(v => v.Key == f.Profile.ContextFields[0]);
            f.Review.Correct(amount.Binding, scenario == "missing-required" ? "" : "=1+2", f.Page);
            if (scenario == "invalid-type") Fixture.Verify(f.Review);
            else Error("REQUIRED_FIELD_MISSING", () => f.Review.Verify(
                f.Review.Snapshot.Review.Fields.Single(v => v.Key == amount.Key).Binding));
        }
        if (scenario != "stale") expected = f.Review.Snapshot.Review.Version;
        if (scenario == "foreign") expected = expected with { ReviewId = Guid.NewGuid() };
        if (scenario == "forged-version") expected = expected with { Revision = expected.Revision + 1 };
        if (scenario == "null") expected = null!;
        var state = f.State;
        var cells = f.Cells;
        Error(code, () => f.Review.PreviewMapping(f.Session, f.Destination(), expected));
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(cells, f.Cells);
        Assert.IsFalse(f.Session.Current.IsDirty);
        Assert.AreEqual(SourceLinkedReviewStatus.Active, f.Review.Snapshot.Status);
    }

    [TestMethod]
    [DataRow("headers", "AMBIGUOUS_HEADERS")]
    [DataRow("boundary", "UNSAFE_BOUNDARY")]
    [DataRow("formula-identity", "AMBIGUOUS_RECORD")]
    [DataRow("truncated", "ANALYSIS_UNSUPPORTED")]
    [DataRow("bounds", "ANALYSIS_LIMIT")]
    public void IncompatibleAnalysisCannotBecomeAWriteOrChangeReview(string scenario, string code)
    {
        using var f = new Fixture();
        var target = f.Destination();
        var cells = f.Session.Cells[target.WorksheetId];
        if (scenario == "headers") cells[(2, 1)] = cells[(2, 1)] with { Value = "Missing header" };
        if (scenario == "boundary") cells[(10, 1)] = cells[(10, 1)] with { Value = "Missing subtotal" };
        if (scenario == "formula-identity") cells[(6, 1)] = cells[(6, 1)] with { Formula = "A7" };
        if (scenario == "truncated") cells[(3, 1)] = cells[(3, 1)] with { Truncated = true };
        if (scenario == "bounds") f.Session.Current = f.Session.Current with
        { Worksheets = [f.Session.Current.Worksheets[0] with { LastRow = f.Profile.MaxRows + 1 }] };
        var state = f.State;
        var before = f.Cells;
        var info = f.Session.Current;
        Error(code, () => f.Preview(target));
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(before, f.Cells);
        Assert.AreEqual(info, f.Session.Current);
    }

    [TestMethod]
    [DataRow("changed", "SOURCE_INVALIDATED")]
    [DataRow("replaced", "SOURCE_INVALIDATED")]
    [DataRow("missing", "SOURCE_INVALIDATED")]
    [DataRow("locked", "SOURCE_INVALIDATED")]
    [DataRow("released", "SOURCE_NOT_REGISTERED")]
    [DataRow("registry-disposed", "SOURCE_SESSION_CLOSED")]
    [DataRow("review-disposed", "SOURCE_REVIEW_CLOSED")]
    public void FreshSourceChecksRejectUnavailableSourcesDespiteCachedVerifiedState(string scenario, string code)
    {
        using var f = new Fixture();
        var cached = f.Preview();
        var state = f.State;
        var info = f.Session.Current;
        var cells = f.Cells;
        var timestamp = File.GetLastWriteTimeUtc(f.Path);
        FileStream? held = null;
        try
        {
            if (scenario == "changed")
            {
                var changed = f.Original.ToArray(); changed[^1] = 77;
                File.WriteAllBytes(f.Path, changed); File.SetLastWriteTimeUtc(f.Path, timestamp);
            }
            if (scenario == "replaced")
            {
                // Hold the old identity alive while creating identical replacement bytes.
                File.Move(f.Path, f.Path + ".old");
                File.WriteAllBytes(f.Path, f.Original); File.SetLastWriteTimeUtc(f.Path, timestamp);
            }
            if (scenario == "missing") File.Move(f.Path, f.Path + ".old");
            if (scenario == "locked") held = File.Open(f.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            if (scenario == "released") f.Sources.Release(f.Page.Version);
            if (scenario == "registry-disposed") f.Sources.Dispose();
            if (scenario == "review-disposed") f.Review.Dispose();
            Error(code, () => f.Review.PreviewMapping(f.Session, cached.Destination, cached.Review));
        }
        finally { held?.Dispose(); }
        Assert.AreEqual(info, f.Session.Current);
        Assert.AreEqual(cells, f.Cells);
        if (scenario != "review-disposed")
        {
            Assert.AreEqual(state, f.State);
            Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, f.Review.Snapshot.Status);
            Error("SOURCE_REVIEW_INVALIDATED", () => f.Preview());
        }
        if (scenario == "changed")
        {
            File.WriteAllBytes(f.Path, f.Original);
            f.Sources.Register(f.Path);
            Error("SOURCE_REVIEW_INVALIDATED", () => f.Preview());
        }
    }

    [TestMethod]
    [DataRow("changed")]
    [DataRow("released")]
    [DataRow("registry-disposed")]
    [DataRow("review-disposed")]
    [DataRow("cancel")]
    public void SourceOrLifetimeChangesDuringWorksheetAnalysisRejectTheWholeCapture(string scenario)
    {
        using var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var state = f.State;
        var info = f.Session.Current;
        var cells = f.Cells;
        f.Session.AfterRead = () =>
        {
            f.Session.AfterRead = null;
            if (scenario == "changed")
            {
                var changed = f.Original.ToArray(); changed[^1] = 77; File.WriteAllBytes(f.Path, changed);
            }
            if (scenario == "released") f.Sources.Release(f.Page.Version);
            if (scenario == "registry-disposed") f.Sources.Dispose();
            if (scenario == "review-disposed") f.Review.Dispose();
            if (scenario == "cancel") cancellation.Cancel();
        };
        void Run() => f.Preview(token: cancellation.Token);
        if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Error(scenario switch
        {
            "changed" => "SOURCE_INVALIDATED", "released" => "SOURCE_NOT_REGISTERED",
            "registry-disposed" => "SOURCE_SESSION_CLOSED", _ => "SOURCE_REVIEW_CLOSED"
        }, Run);
        Assert.AreEqual(info, f.Session.Current);
        Assert.AreEqual(cells, f.Cells);
        if (scenario != "review-disposed") Assert.AreEqual(state, f.State);
        if (scenario == "cancel") Assert.IsFalse(f.Preview().CanApply);
    }

    [TestMethod]
    [DataRow("revision", 1, "STALE_REVISION")]
    [DataRow("revision", 2, "STALE_REVISION")]
    [DataRow("source", 2, "INVALID_MAPPING_DESTINATION")]
    [DataRow("session", 2, "INVALID_MAPPING_DESTINATION")]
    [DataRow("sheet", 2, "UNKNOWN_WORKSHEET")]
    [DataRow("cancel", 1, "cancel")]
    [DataRow("cancel", 2, "cancel")]
    [DataRow("released", 2, "SOURCE_NOT_REGISTERED")]
    [DataRow("registry-disposed", 2, "SOURCE_SESSION_CLOSED")]
    [DataRow("review-disposed", 2, "SOURCE_REVIEW_CLOSED")]
    public void DestinationIsRecheckedAfterSourceIoAndLateFailuresNeverPublish(string scenario, int atRead, string code)
    {
        using var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var state = f.State;
        var cells = f.Cells;
        var reads = 0;
        f.Reader.AfterRead = _ =>
        {
            if (++reads != atRead) return;
            if (scenario == "revision") f.Session.Current = f.Session.Current with { Revision = 1 };
            if (scenario == "source") f.Session.Current = f.Session.Current with { SourceSha256 = new string('C', 64) };
            if (scenario == "session") f.Session.Current = f.Session.Current with { SessionId = Guid.NewGuid() };
            if (scenario == "sheet") f.Session.Current = f.Session.Current with { Worksheets = [f.Session.Current.Worksheets[1]] };
            if (scenario == "cancel") cancellation.Cancel();
            if (scenario == "released") f.Sources.Release(f.Page.Version);
            if (scenario == "registry-disposed") f.Sources.Dispose();
            if (scenario == "review-disposed") f.Review.Dispose();
        };
        var target = f.Destination();
        void Run() => f.Preview(target, cancellation.Token);
        if (code == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else Error(code, Run);
        f.Reader.AfterRead = null;
        Assert.AreEqual(cells, f.Cells);
        if (scenario != "review-disposed") Assert.AreEqual(state, f.State);
        if (atRead == 1) Assert.AreEqual(0, f.Session.Reads);
        if (scenario == "cancel") Assert.IsFalse(f.Preview().CanApply);
        CollectionAssert.AreEqual(f.Original, File.ReadAllBytes(f.Path));
    }

    [TestMethod]
    [DataRow("released")]
    [DataRow("registry-disposed")]
    [DataRow("review-disposed")]
    [DataRow("cancel")]
    [DataRow("reentrant-correct")]
    public void FinalDestinationReadCannotHideLifetimeChangesOrReentrantCorrections(string scenario)
    {
        using var f = new Fixture();
        using var cancellation = new CancellationTokenSource();
        var state = f.State;
        // Arm only when the final post-projection source observation has completed.
        var reads = 0;
        f.Reader.AfterRead = _ =>
        {
            if (++reads != 2) return;
            f.Session.BeforeInfo = () =>
            {
                f.Session.BeforeInfo = null;
                if (scenario == "released") f.Sources.Release(f.Page.Version);
                if (scenario == "registry-disposed") f.Sources.Dispose();
                if (scenario == "review-disposed") f.Review.Dispose();
                if (scenario == "cancel") cancellation.Cancel();
                if (scenario == "reentrant-correct") Error("SOURCE_REVIEW_BUSY", () =>
                    f.Review.Correct(f.Review.Snapshot.Review.Fields[0].Binding, "Attempted substitution", f.Page));
            };
        };
        void Run() => f.Preview(token: cancellation.Token);
        if (scenario == "cancel") Assert.ThrowsExactly<OperationCanceledException>(Run);
        else if (scenario == "reentrant-correct") Assert.IsFalse(f.Preview().CanApply);
        else Error(scenario switch
        {
            "released" => "SOURCE_NOT_REGISTERED", "registry-disposed" => "SOURCE_SESSION_CLOSED", _ => "SOURCE_REVIEW_CLOSED"
        }, Run);
        if (scenario != "review-disposed") Assert.AreEqual(state, f.State);
        Assert.IsFalse(f.Session.Current.IsDirty);
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void CorrectionsRequireRenewedVerificationAndKeepOldPreviewHistorical(string profileId)
    {
        using var f = new Fixture(profileId);
        var cached = f.Preview();
        var old = cached.Fields[0].Value;
        var second = new RegisteredSourcePage(f.Sources.Register(f.Path).Version, 3);
        f.Review.Correct(f.Review.Snapshot.Review.Fields[0].Binding, "Corrected artificial entity", second);
        Error("STALE_RECORD_REVIEW", () => f.Review.PreviewMapping(f.Session, cached.Destination, cached.Review));
        Error("REVIEW_INCOMPLETE", () => f.Preview());
        Fixture.Verify(f.Review);
        var fresh = f.Preview();
        Assert.AreEqual(old, cached.Fields[0].Value);
        Assert.AreEqual("Corrected artificial entity", fresh.Fields[0].Value.LiteralText);
        Assert.AreEqual(1, fresh.Fields[0].Value.ValueRevision);
        Assert.AreEqual(second.Version.DocumentId, fresh.Fields[0].Value.Source!.DocumentId);
        Assert.AreEqual(3, fresh.Fields[0].Value.Source!.PageNumber);
        Assert.AreNotEqual(cached.Review, fresh.Review);
        Assert.IsFalse(fresh.CanApply);
        CollectionAssert.AreEqual(f.Original, File.ReadAllBytes(f.Path));
    }

    [TestMethod]
    public void ChangeAfterInitialSourceObservationIsCaughtAfterAnalysis()
    {
        using var f = new Fixture();
        var state = f.State;
        var reads = 0;
        f.Reader.AfterRead = _ =>
        {
            if (++reads != 1) return;
            var changed = f.Original.ToArray(); changed[^1] = 77; File.WriteAllBytes(f.Path, changed);
        };
        Error("SOURCE_INVALIDATED", () => f.Preview());
        Assert.IsTrue(f.Session.Reads > 0);
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, f.Review.Snapshot.Status);
    }

    [TestMethod]
    public void FinalSourceCheckCannotHideReleaseOfAnEarlierRegistration()
    {
        using var f = new Fixture();
        var second = new RegisteredSourcePage(f.Sources.Register(f.Path).Version, 3);
        var field = f.Review.Snapshot.Review.Fields[0];
        f.Review.Correct(field.Binding, field.Value, second);
        Fixture.Verify(f.Review);
        var state = f.State;
        var reads = 0;
        f.Reader.AfterRead = _ =>
        {
            // Two associations before analysis and two after it. Release the
            // association checked earlier, while the last read is completing.
            if (++reads == 4) f.Sources.Release(second.Version);
        };
        Error("SOURCE_NOT_REGISTERED", () => f.Preview());
        Assert.AreEqual(4, reads);
        Assert.AreEqual(state, f.State);
        Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, f.Review.Snapshot.Status);
    }

    [TestMethod]
    public void CancellationBeforeCaptureAndForeignProfileCannotReadSourcesOrWorksheet()
    {
        using var f = new Fixture();
        using var other = new Fixture("synthetic-laboratory");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        var reads = f.Reader.Reads;
        Assert.ThrowsExactly<OperationCanceledException>(() => f.Preview(token: cancellation.Token));
        Assert.AreEqual(0, f.Session.Reads);
        Assert.AreEqual(reads, f.Reader.Reads);
        var actual = other.Review.Snapshot.Review.Version;
        var forged = actual with { ProfileId = f.Profile.Id, ProfileHash = WorkflowProfiles.Hash(f.Profile) };
        Error("STALE_RECORD_REVIEW", () => other.Review.PreviewMapping(f.Session, f.Destination(), forged));
        Assert.AreEqual(0, f.Session.Reads);
        Error("STALE_PROFILE", () => other.Review.PreviewMapping(f.Session, f.Destination(), actual));
        Assert.AreEqual(0, f.Session.Reads);
    }

    [TestMethod]
    public async Task MappingSerializesWithCorrectionAndRejectsReentrantMapping()
    {
        using var f = new Fixture();
        var expected = f.Review.Snapshot.Review.Version;
        var field = f.Review.Snapshot.Review.Fields[0];
        var destination = f.Destination();
        using var entered = new ManualResetEventSlim();
        using var proceed = new ManualResetEventSlim();
        f.Session.AfterRead = () =>
        {
            f.Session.AfterRead = null;
            Error("SOURCE_REVIEW_BUSY", () => f.Preview());
            entered.Set();
            Assert.IsTrue(proceed.Wait(TimeSpan.FromSeconds(10)), "Artificial worksheet read release timed out.");
        };
        var mapping = Task.Run(() => f.Review.PreviewMapping(f.Session, destination, expected));
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(10)), "Artificial worksheet read entry timed out.");
            var correction = Task.Run(() => f.Review.Correct(field.Binding, "Corrected after mapping", f.Page));
            proceed.Set();
            var captured = await mapping;
            await correction;
            Assert.AreEqual(expected, captured.Review);
            Assert.AreEqual(field.Value, captured.Fields[0].Value.LiteralText);
            Error("STALE_RECORD_REVIEW", () => f.Review.PreviewMapping(f.Session, destination, captured.Review));
            Error("REVIEW_INCOMPLETE", () => f.Preview());
        }
        finally { proceed.Set(); }
        Assert.IsFalse(f.Session.Current.IsDirty);
    }

    private static void Error(string code, Action action)
    {
        var error = Assert.Throws<Exception>(action);
        var actual = error switch
        {
            FieldReviewException review => review.Code,
            DocumentSourceException source => source.Code,
            WorkbookException workbook => workbook.Code,
            _ => error.GetType().Name
        };
        Assert.AreEqual(code, actual);
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-source-mapping-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public byte[] Original { get; }
        public WorkflowProfile Profile { get; }
        public Dictionary<string, string> Values { get; }
        public ObservingReader Reader { get; } = new();
        public LocalDocumentSources Sources { get; }
        public SourceLinkedFieldReview Review { get; }
        public RegisteredSourcePage Page { get; }
        public Session Session { get; }
        public string State => JsonSerializer.Serialize(Review.Snapshot.Review);
        public string Cells => JsonSerializer.Serialize(Session.Cells.Values.Select(v => v.Values.ToArray()).ToArray());
        public Fixture(string profileId = "synthetic-payables", string extension = ".pdf", bool verified = true)
        {
            Directory.CreateDirectory(Root);
            Path = System.IO.Path.Combine(Root, "artificial" + extension);
            Original = new byte[512];
            var signature = extension switch
            {
                ".png" => new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 },
                ".jpg" => new byte[] { 255, 216, 255, 224, 0, 16, 74, 70 },
                _ => Encoding.ASCII.GetBytes("%PDF-1.7\n% artificial triage only\n")
            };
            signature.CopyTo(Original, 0); File.WriteAllBytes(Path, Original);
            Profile = WorkflowProfiles.Get(profileId);
            Session = new(Profile);
            Sources = new(Reader);
            Page = new(Sources.Register(Path).Version, extension == ".pdf" ? 2 : 1);
            Values = new()
            {
                [Profile.EntityField] = "  Artificial organization  ",
                [Profile.Fields[1].Key] = "  =SUM(A1:A2)  ",
                [Profile.IdentifierFields[0]] = "  NEW-ID  ",
                [Profile.ContextFields[0]] = "+00125.5000",
                [Profile.ContextFields[1]] = "2024-02-29"
            };
            Review = new(Sources, Profile, Values.Select(v => new SourceLinkedDraftField(v.Key, v.Value, Page)).ToArray());
            if (verified) Verify(Review);
        }
        public static void Verify(SourceLinkedFieldReview review)
        {
            foreach (var key in review.Snapshot.Review.Fields.Where(f => f.Source != null && f.Status != FieldReviewStatus.Verified).Select(f => f.Key))
                review.Verify(review.Snapshot.Review.Fields.Single(f => f.Key == key).Binding);
        }
        public MappingDestination Destination(int sheet = 0) => new(Session.Current.SessionId, Session.Current.Worksheets[sheet].Id,
            Session.Current.Revision, Session.Current.SourceSha256, Profile.Id, WorkflowProfiles.Hash(Profile));
        public VerifiedMappingPreview Preview(MappingDestination? destination = null, CancellationToken token = default) =>
            Review.PreviewMapping(Session, destination ?? Destination(), Review.Snapshot.Review.Version, token);
        public void Dispose()
        {
            Review.Dispose(); Sources.Dispose(); Session.Dispose();
            foreach (var file in Directory.GetFiles(Root)) File.Delete(file);
            Directory.Delete(Root);
        }
    }

    private sealed class ObservingReader : ILocalDocumentReader
    {
        public Action<string>? AfterRead;
        public int Reads;
        public DocumentObservation Read(string path, long maxBytes, CancellationToken token)
        {
            Reads++;
            var result = new WindowsDocumentReader().Read(path, maxBytes, token);
            AfterRead?.Invoke(path);
            return result;
        }
    }

    private sealed class Session : IWorkbookSession
    {
        public WorkbookInfo Current;
        public readonly Dictionary<Guid, Dictionary<(int Row, int Column), CellInfo>> Cells = [];
        public Action? AfterRead;
        public Action? BeforeInfo;
        public int Reads;
        public Session(WorkflowProfile p)
        {
            var first = WorkflowPlanningTests.Capture(p);
            var second = WorkflowPlanningTests.Capture(p, 20);
            var sheets = new[] { first.Sheet with { Name = "Artificial first" },
                second.Sheet with { Name = "Artificial shifted", Order = 2, LastColumn = p.MaxColumns } };
            Current = first.Info with { Worksheets = sheets };
            Cells[sheets[0].Id] = first.Cells;
            Cells[sheets[1].Id] = second.Cells.Values.ToDictionary(c => (c.Row, p.MaxColumns + 1 - c.Column),
                c => c with { Column = p.MaxColumns + 1 - c.Column });
        }
        public WorkbookInfo Info { get { BeforeInfo?.Invoke(); return Current; } }
        public CellViewport ReadViewport(Guid id, int row, int col, int rows, int columns)
        {
            Reads++;
            var result = new CellViewport(Current.SessionId, id, row, col, rows, columns,
                Cells[id].Values.Where(c => c.Row >= row && c.Row < row + rows && c.Column >= col && c.Column < col + columns).ToArray());
            AfterRead?.Invoke(); return result;
        }
        public WorkingCopyInfo? WorkingCopy => null;
        public IWorkbookSession CreateSnapshot(CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public IWorkbookSession CreateWorkingCopy(Guid id, int revision, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public WorkbookInfo Edit(CellEdit edit, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public SaveReceipt SaveAsNew(string path, int revision, CancellationToken token = default) => throw new AssertFailedException("Mapping must only read.");
        public void Dispose() { }
    }
}
