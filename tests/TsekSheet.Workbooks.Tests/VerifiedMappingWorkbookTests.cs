using System.Security.Cryptography;
using System.Text.Json;
using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Verification;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class VerifiedMappingWorkbookTests
{
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static FieldReviewSession Review(WorkflowProfile p, string entity = "  Artificial new entity  ", string identifier = "SYN-NEW")
    {
        var source = new SourceEvidence(Guid.NewGuid(), new string('B', 64), 0, 1);
        var review = new FieldReviewSession(p,
        [new(p.EntityField, entity, source), new(p.IdentifierFields[0], identifier, source),
            new(p.Fields[1].Key, " =SUM(A1:A2) ", source), new(p.ContextFields[0], "123.4500", source)]);
        foreach (var key in review.Snapshot.Fields.Where(f => f.Source != null).Select(f => f.Key))
            review.Verify(review.Snapshot.Fields.Single(f => f.Key == key).Binding);
        return review;
    }
    private static MappingDestination Destination(IWorkbookSession session, Guid sheet, WorkflowProfile p) =>
        new(session.Info.SessionId, sheet, session.Info.Revision, session.Info.SourceSha256, p.Id, WorkflowProfiles.Hash(p));
    private static CellInfo[] Capture(IWorkbookSession session, WorksheetInfo sheet)
    {
        var cells = new List<CellInfo>();
        for (var row = 1; row <= sheet.LastRow; row += 20)
        for (var col = 1; col <= sheet.LastColumn; col += 10)
            cells.AddRange(session.ReadViewport(sheet.Id, row, col, Math.Min(20, sheet.LastRow - row + 1),
                Math.Min(10, sheet.LastColumn - col + 1)).Cells);
        return cells.ToArray();
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void VerifiedMappingOnRealIndependentWorksheetsAndCopiesPreservesEveryCellAndSource(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p);
        var hash = Hash(fixture.Path);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        {
            Assert.IsFalse(session.Info.EditingAllowed, "Mapping reads a workbook without write authority.");
            var review = Review(p);
            var reviewBefore = JsonSerializer.Serialize(review.Snapshot);
            var sessionBefore = session.Info;
            var baseline = sessionBefore.Worksheets.ToDictionary(s => s.Id, s => Capture(session, s));
            Assert.IsTrue(baseline.Values.SelectMany(c => c).Any(c => c.Formula != null));
            Assert.IsTrue(baseline.Values.SelectMany(c => c).Any(c => c.Format.Bold));
            Assert.IsTrue(baseline.Values.SelectMany(c => c).Any(c => c.Format.NumberFormat == "#,##0.0000"));
            var previews = new List<VerifiedMappingPreview>();
            for (var sheetIndex = 0; sheetIndex < 2; sheetIndex++)
            {
                var sheet = sessionBefore.Worksheets[sheetIndex];
                var target = Destination(session, sheet.Id, p);
                var main = VerifiedMapping.Preview(session, target, review, review.Snapshot.Version);
                previews.Add(main);
                Assert.AreEqual(sheet.Name, main.WorksheetName);
                Assert.AreEqual(sheetIndex == 0 ? 22 : 42, main.Sections[1].SubtotalRow);
                Assert.AreEqual(sheetIndex == 0 ? 1 : 6, main.Fields[0].Column);
                Assert.AreEqual(" =SUM(A1:A2) ", main.Fields[1].Value.LiteralText);
                Assert.AreEqual("text", main.Fields[1].Value.Kind);
                Assert.IsFalse(main.CanApply);

                using var copy = session.CreateSnapshot();
                var copyTarget = Destination(copy, sheet.Id, p);
                if (sheetIndex == 1)
                {
                    // A deliberately stale destination binding; snapshots stay read-only.
                    copyTarget = copyTarget with { Revision = copyTarget.Revision + 1 };
                    Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() =>
                        VerifiedMapping.Preview(copy, copyTarget, review, review.Snapshot.Version)).Code);
                    copyTarget = Destination(copy, sheet.Id, p);
                }
                var copyBefore = copy.Info;
                var copyCells = copy.Info.Worksheets.ToDictionary(s => s.Id, s => Capture(copy, s));
                var independent = VerifiedMapping.Preview(copy, copyTarget, review, review.Snapshot.Version);
                Assert.AreNotEqual(main.Destination.SessionId, independent.Destination.SessionId);
                Assert.AreEqual(main.Review, independent.Review);
                CollectionAssert.AreEqual(main.Fields.ToArray(), independent.Fields.ToArray());
                CollectionAssert.AreEqual(main.Sections.ToArray(), independent.Sections.ToArray());
                Assert.AreEqual(copyBefore, copy.Info);
                foreach (var context in copy.Info.Worksheets)
                    CollectionAssert.AreEqual(copyCells[context.Id], Capture(copy, context));
                Assert.IsFalse(copy.Info.IsDirty);
            }
            Assert.AreNotEqual(previews[0].Destination.WorksheetId, previews[1].Destination.WorksheetId);
            Assert.AreNotEqual(previews[0].Fields[0].Column, previews[1].Fields[0].Column);
            var incompatible = Destination(session, sessionBefore.Worksheets[2].Id, p);
            Assert.AreEqual("AMBIGUOUS_HEADERS", Assert.ThrowsExactly<WorkbookException>(() =>
                VerifiedMapping.Preview(session, incompatible, review, review.Snapshot.Version)).Code);
            Assert.AreEqual(sessionBefore, session.Info);
            Assert.AreEqual(reviewBefore, JsonSerializer.Serialize(review.Snapshot));
            foreach (var sheet in session.Info.Worksheets)
                CollectionAssert.AreEqual(baseline[sheet.Id], Capture(session, sheet));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void VerifiedPlacementsPreserveIndependentWorksheetsDirtyCopiesWarningsAndSource(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p);
        var hash = Hash(fixture.Path);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        {
            var initial = session.Info;
            var baseline = initial.Worksheets.ToDictionary(s => s.Id, s => Capture(session, s));
            var review = Review(p, "Artificial existing entity", "SYN-EXISTING");
            var reviewBefore = JsonSerializer.Serialize(review.Snapshot);
            foreach (var sheetIndex in new[] { 0, 1 })
            {
                var sheet = initial.Worksheets[sheetIndex];
                var destination = Destination(session, sheet.Id, p);
                var ambiguous = VerifiedPlacement.Preview(session, destination, review, review.Snapshot.Version);
                Assert.IsFalse(ambiguous.Proposal.ReadyForReview);
                Assert.IsTrue(ambiguous.Proposal.Records[0].RequiresSectionChoice);
                Assert.AreEqual(2, ambiguous.Proposal.Records[0].Duplicates.Count);
                Assert.IsNull(ambiguous.Proposal.Records[0].ProposedRow);
                var choice = new PlacementChoices(p.Sections[1].Id);
                var main = VerifiedPlacement.Preview(session, destination, review, review.Snapshot.Version, choice);
                Assert.AreEqual(sheetIndex == 0 ? 17 : 37, main.Proposal.Records[0].ProposedRow);
                Assert.AreEqual(sheetIndex == 0 ? 22 : 42, main.Mapping.Sections[1].SubtotalRow);
                Assert.AreEqual(sheetIndex == 0 ? 1 : 6, main.Mapping.Fields[0].Column);
                Assert.AreEqual(" =SUM(A1:A2) ", main.Mapping.Fields[1].Value.LiteralText);
                Assert.AreEqual(123.4500m, main.Mapping.Fields[4].Value.DecimalValue);
                Assert.IsFalse(main.CanApply);
                Assert.IsFalse(main.Proposal.CanApply);
                using var copy = session.CreateSnapshot();
                var copyTarget = Destination(copy, sheet.Id, p);
                if (sheetIndex == 1)
                {
                    copyTarget = copyTarget with { Revision = copyTarget.Revision + 1 };
                    Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() =>
                        VerifiedPlacement.Preview(copy, copyTarget, review, review.Snapshot.Version, choice)).Code);
                    copyTarget = Destination(copy, sheet.Id, p);
                }
                var copyBefore = copy.Info;
                var copyCells = copyBefore.Worksheets.ToDictionary(s => s.Id, s => Capture(copy, s));
                var independent = VerifiedPlacement.Preview(copy, copyTarget, review, review.Snapshot.Version, choice);
                Assert.AreNotEqual(main.Mapping.Destination.SessionId, independent.Mapping.Destination.SessionId);
                Assert.AreEqual(main.Mapping.Review, independent.Mapping.Review);
                CollectionAssert.AreEqual(main.Mapping.Fields.ToArray(), independent.Mapping.Fields.ToArray());
                CollectionAssert.AreEqual(main.Proposal.Records[0].Duplicates.ToArray(), independent.Proposal.Records[0].Duplicates.ToArray());
                Assert.AreEqual(main.Proposal.Records[0].ProposedRow, independent.Proposal.Records[0].ProposedRow);
                Assert.AreEqual("INVALID_MAPPING_DESTINATION", Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.Preview(copy, destination, review, review.Snapshot.Version, choice)).Code);
                Assert.AreEqual("INVALID_SECTION_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.Preview(copy, copyTarget, review, review.Snapshot.Version, new("foreign"))).Code);
                Assert.IsNull(typeof(IWorkbookSession).GetMethod("InsertWorkflowRows"));
                Assert.AreEqual(copyBefore, copy.Info);
                Assert.IsFalse(copy.Info.IsDirty);
                foreach (var context in copy.Info.Worksheets)
                    CollectionAssert.AreEqual(copyCells[context.Id], Capture(copy, context));
            }
            Assert.AreEqual(initial, session.Info);
            Assert.AreEqual(reviewBefore, JsonSerializer.Serialize(review.Snapshot));
            foreach (var sheet in initial.Worksheets)
                CollectionAssert.AreEqual(baseline[sheet.Id], Capture(session, sheet));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void BoundedVerifiedPlacementsPreserveIndependentSheetsCopiesAndAllSourceContent(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p);
        var hash = Hash(fixture.Path);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        {
            var initial = session.Info;
            var baseline = initial.Worksheets.ToDictionary(s => s.Id, s => Capture(session, s));
            var reviews = new[] { Review(p, "Artificial existing entity", "SYN-EXISTING"),
                Review(p, "Artificial existing entity", "SYN-EXISTING"), Review(p, "Novel Organization") };
            var inputs = reviews.Select((r, i) => new VerifiedPlacementInput(r, r.Snapshot.Version,
                i < 2 ? new(p.Sections[1 - i].Id) : null)).ToArray();
            var reviewBefore = reviews.Select(r => JsonSerializer.Serialize(r.Snapshot)).ToArray();
            foreach (var sheetIndex in new[] { 0, 1 })
            {
                var sheet = initial.Worksheets[sheetIndex];
                var destination = Destination(session, sheet.Id, p);
                var main = VerifiedPlacement.PreviewMany(session, destination, inputs);
                CollectionAssert.AreEqual(new int?[] { sheetIndex == 0 ? 18 : 38, 7, sheetIndex == 0 ? 24 : 44 },
                    main.Proposal.Records.Select(r => r.ProposedRow).ToArray());
                Assert.IsTrue(main.Proposal.ReadyForReview);
                Assert.IsFalse(main.CanApply);
                Assert.IsFalse(main.Proposal.CanApply);
                foreach (var index in new[] { 0, 1 })
                {
                    var warnings = main.Proposal.Records[index].Duplicates;
                    Assert.AreEqual(3, warnings.Count);
                    Assert.AreEqual(inputs[1 - index].ExpectedReview.ReviewId.ToString("N"), warnings.Single(d => d.BatchRecordId != null).BatchRecordId);
                    Assert.AreEqual(2, warnings.Count(d => d.ExistingRow != null));
                }
                foreach (var mapping in main.Mappings)
                {
                    Assert.AreEqual(sheetIndex == 0 ? 1 : 6, mapping.Fields[0].Column);
                    Assert.AreEqual(sheetIndex == 0 ? 22 : 42, mapping.Sections[1].SubtotalRow);
                    Assert.AreEqual(" =SUM(A1:A2) ", mapping.Fields[1].Value.LiteralText);
                    Assert.AreEqual(123.4500m, mapping.Fields[4].Value.DecimalValue);
                    Assert.IsFalse(mapping.CanApply);
                }
                using var copy = session.CreateSnapshot();
                var copyTarget = Destination(copy, sheet.Id, p);
                if (sheetIndex == 1)
                {
                    copyTarget = copyTarget with { Revision = copyTarget.Revision + 1 };
                    Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() =>
                        VerifiedPlacement.PreviewMany(copy, copyTarget, inputs)).Code);
                    copyTarget = Destination(copy, sheet.Id, p);
                }
                var copyBefore = copy.Info;
                var copyCells = copyBefore.Worksheets.ToDictionary(s => s.Id, s => Capture(copy, s));
                var independent = VerifiedPlacement.PreviewMany(copy, copyTarget, inputs);
                Assert.AreNotEqual(main.Mappings[0].Destination.SessionId, independent.Mappings[0].Destination.SessionId);
                for (var index = 0; index < inputs.Length; index++)
                {
                    Assert.AreEqual(main.Mappings[index].Review, independent.Mappings[index].Review);
                    CollectionAssert.AreEqual(main.Mappings[index].Fields.ToArray(), independent.Mappings[index].Fields.ToArray());
                    CollectionAssert.AreEqual(main.Proposal.Records[index].Duplicates.ToArray(), independent.Proposal.Records[index].Duplicates.ToArray());
                    Assert.AreEqual(main.Proposal.Records[index].ProposedRow, independent.Proposal.Records[index].ProposedRow);
                }
                var twenty = Enumerable.Range(0, 20).Select(i => Review(p, "Artificial existing entity", $"MAX-{i}")).Select(r =>
                    new VerifiedPlacementInput(r, r.Snapshot.Version, new(p.Sections[1].Id))).ToArray();
                var maximum = VerifiedPlacement.PreviewMany(copy, copyTarget, twenty);
                CollectionAssert.AreEqual(Enumerable.Range(sheetIndex == 0 ? 17 : 37, 20).Select(i => (int?)i).ToArray(),
                    maximum.Proposal.Records.Select(r => r.ProposedRow).ToArray());
                Assert.IsTrue(maximum.Proposal.Records.All(r => r.Duplicates.Count == 0));
                Assert.AreEqual("INVALID_MAPPING_DESTINATION", Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.PreviewMany(copy, destination, inputs)).Code);
                var repeated = new[] { inputs[0], inputs[0] };
                Assert.AreEqual("INVALID_RECORD", Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.PreviewMany(copy, copyTarget, repeated)).Code);
                var invalid = inputs.ToArray();
                invalid[2] = inputs[2] with { ExpectedReview = inputs[0].ExpectedReview };
                Assert.AreEqual("STALE_RECORD_REVIEW", Assert.ThrowsExactly<FieldReviewException>(() =>
                    VerifiedPlacement.PreviewMany(copy, copyTarget, invalid)).Code);
                invalid[2] = inputs[2] with { Choices = new("foreign") };
                Assert.AreEqual("INVALID_SECTION_CHOICE", Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.PreviewMany(copy, copyTarget, invalid)).Code);
                Assert.IsNull(typeof(IWorkbookSession).GetMethod("InsertWorkflowRows"));
                Assert.AreEqual(copyBefore, copy.Info);
                Assert.IsFalse(copy.Info.IsDirty);
                foreach (var context in copy.Info.Worksheets)
                    CollectionAssert.AreEqual(copyCells[context.Id], Capture(copy, context));
            }
            Assert.AreEqual(initial, session.Info);
            CollectionAssert.AreEqual(reviewBefore, reviews.Select(r => JsonSerializer.Serialize(r.Snapshot)).ToArray());
            foreach (var sheet in initial.Worksheets)
                CollectionAssert.AreEqual(baseline[sheet.Id], Capture(session, sheet));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables", "missing-subtotal", "UNSAFE_BOUNDARY")]
    [DataRow("synthetic-laboratory", "missing-subtotal", "UNSAFE_BOUNDARY")]
    [DataRow("synthetic-payables", "duplicate-headers", "AMBIGUOUS_HEADERS")]
    [DataRow("synthetic-laboratory", "duplicate-headers", "AMBIGUOUS_HEADERS")]
    public void IncompatibleRealWorksheetRejectsOnMainAndCopyWithoutChangingAnything(string profileId, string scenario, string code)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p, scenario);
        var hash = Hash(fixture.Path);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        using (var copy = session.CreateSnapshot())
        {
            var review = Review(p);
            var reviewBefore = JsonSerializer.Serialize(review.Snapshot);
            foreach (var target in new[] { session, copy })
            {
                var before = target.Info;
                var cells = before.Worksheets.ToDictionary(s => s.Id, s => Capture(target, s));
                Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedMapping.Preview(target, Destination(target, before.Worksheets[0].Id, p), review, review.Snapshot.Version)).Code);
                Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.Preview(target, Destination(target, before.Worksheets[0].Id, p), review, review.Snapshot.Version)).Code);
                var other = Review(p);
                Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() =>
                    VerifiedPlacement.PreviewMany(target, Destination(target, before.Worksheets[0].Id, p),
                        [new(review, review.Snapshot.Version), new(other, other.Snapshot.Version)])).Code);
                Assert.AreEqual(before, target.Info);
                Assert.IsFalse(target.Info.IsDirty);
                foreach (var sheet in target.Info.Worksheets)
                    CollectionAssert.AreEqual(cells[sheet.Id], Capture(target, sheet));
            }
            Assert.IsFalse(copy.Info.IsDirty);
            Assert.AreEqual(reviewBefore, JsonSerializer.Serialize(review.Snapshot));
        }
        Assert.AreEqual(hash, Hash(fixture.Path));
    }

    [TestMethod]
    [DataRow("synthetic-payables")]
    [DataRow("synthetic-laboratory")]
    public void SourceLinkedMappingPreservesIndependentSheetsDirtyCopiesAndOriginalFiles(string profileId)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p);
        using var document = new MappingDocument();
        var workbookHash = Hash(fixture.Path);
        var documentHash = Hash(document.Path);
        using var sources = new LocalDocumentSources();
        var page = new RegisteredSourcePage(sources.Register(document.Path).Version, 2);
        using var review = LiveReview(p, sources, page);
        var beforeReview = JsonSerializer.Serialize(review.Snapshot);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        {
            var before = session.Info;
            var baseline = before.Worksheets.ToDictionary(s => s.Id, s => Capture(session, s));
            Assert.IsTrue(baseline.Values.SelectMany(v => v).Any(v => v.Formula != null));
            Assert.IsTrue(baseline.Values.SelectMany(v => v).Any(v => v.Format.Bold));
            Assert.IsTrue(baseline.Values.SelectMany(v => v).Any(v => v.Format.NumberFormat == "#,##0.0000"));
            for (var index = 0; index < 2; index++)
            {
                var sheet = before.Worksheets[index];
                var destination = Destination(session, sheet.Id, p);
                var version = review.Snapshot.Review.Version;
                var main = review.PreviewMapping(session, destination, version);
                var placement = review.PreviewPlacement(session, destination, version, new(p.Sections[1].Id, true));
                Assert.IsFalse(placement.CanApply); Assert.IsFalse(placement.Proposal.CanApply);
                Assert.IsNotNull(placement.Proposal.Records[0].ProposedRow);
                Assert.AreEqual(sheet.Name, main.WorksheetName);
                Assert.AreEqual(index == 0 ? 22 : 42, main.Sections[1].SubtotalRow);
                Assert.AreEqual(index == 0 ? 1 : 6, main.Fields[0].Column);
                Assert.AreEqual(" =SUM(A1:A2) ", main.Fields[1].Value.LiteralText);
                Assert.AreEqual(123.4500m, main.Fields[4].Value.DecimalValue);
                Assert.AreEqual(page.Version.DocumentId, main.Fields[0].Value.Source!.DocumentId);
                Assert.AreEqual(page.Version.Sha256, main.Fields[0].Value.Source!.DocumentSha256);
                Assert.AreEqual(2, main.Fields[0].Value.Source!.PageNumber);
                Assert.IsFalse(main.CanApply);
                using var copy = session.CreateSnapshot();
                var target = Destination(copy, sheet.Id, p);
                if (index == 1)
                {
                    target = target with { Revision = target.Revision + 1 };
                    Assert.AreEqual("STALE_REVISION", Assert.ThrowsExactly<WorkbookException>(() =>
                        review.PreviewMapping(copy, target, version)).Code);
                    target = Destination(copy, sheet.Id, p);
                }
                var copyBefore = copy.Info;
                var copyCells = copyBefore.Worksheets.ToDictionary(s => s.Id, s => Capture(copy, s));
                var independent = review.PreviewMapping(copy, target, version);
                var copyPlacement = review.PreviewPlacement(copy, target, version, new(p.Sections[1].Id, true));
                Assert.AreEqual(placement.Proposal.Records[0].ProposedRow, copyPlacement.Proposal.Records[0].ProposedRow);
                Assert.IsFalse(copyPlacement.CanApply);
                Assert.AreNotEqual(main.Destination.SessionId, independent.Destination.SessionId);
                Assert.AreEqual(main.Review, independent.Review);
                CollectionAssert.AreEqual(main.Fields.ToArray(), independent.Fields.ToArray());
                CollectionAssert.AreEqual(main.Sections.ToArray(), independent.Sections.ToArray());
                Assert.IsFalse(independent.CanApply);
                Assert.AreEqual("INVALID_MAPPING_DESTINATION", Assert.ThrowsExactly<WorkbookException>(() =>
                    review.PreviewMapping(copy, destination, version)).Code);
                Assert.AreEqual(copyBefore, copy.Info);
                foreach (var context in copyBefore.Worksheets)
                    CollectionAssert.AreEqual(copyCells[context.Id], Capture(copy, context));
                Assert.IsFalse(copy.Info.IsDirty);
            }
            sources.Release(page.Version);
            Assert.AreEqual("SOURCE_NOT_REGISTERED", Assert.ThrowsExactly<DocumentSourceException>(() =>
                review.PreviewMapping(session, Destination(session, before.Worksheets[0].Id, p), review.Snapshot.Review.Version)).Code);
            Assert.AreEqual(SourceLinkedReviewStatus.Invalidated, review.Snapshot.Status);
            Assert.AreEqual(beforeReview, JsonSerializer.Serialize(review.Snapshot with { Status = SourceLinkedReviewStatus.Active }));
            Assert.AreEqual(before, session.Info);
            foreach (var sheet in before.Worksheets)
                CollectionAssert.AreEqual(baseline[sheet.Id], Capture(session, sheet));
        }
        Assert.AreEqual(workbookHash, Hash(fixture.Path));
        Assert.AreEqual(documentHash, Hash(document.Path));
        using (File.Open(document.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
    }

    [TestMethod]
    [DataRow("synthetic-payables", "missing-subtotal", "UNSAFE_BOUNDARY")]
    [DataRow("synthetic-laboratory", "missing-subtotal", "UNSAFE_BOUNDARY")]
    [DataRow("synthetic-payables", "duplicate-headers", "AMBIGUOUS_HEADERS")]
    [DataRow("synthetic-laboratory", "duplicate-headers", "AMBIGUOUS_HEADERS")]
    public void SourceLinkedMappingRejectsIncompatibleWorkbooksWithoutMutation(string profileId, string scenario, string code)
    {
        var p = WorkflowProfiles.Get(profileId);
        using var fixture = new MappingFixture(p, scenario);
        using var document = new MappingDocument();
        var workbookHash = Hash(fixture.Path);
        var documentHash = Hash(document.Path);
        using var sources = new LocalDocumentSources();
        using var review = LiveReview(p, sources, new(sources.Register(document.Path).Version, 1));
        var beforeReview = JsonSerializer.Serialize(review.Snapshot);
        using (var session = new WorkbookEngine().Open(fixture.Path))
        using (var copy = session.CreateSnapshot())
        {
            foreach (var target in new[] { session, copy })
            {
                var before = target.Info;
                var cells = before.Worksheets.ToDictionary(s => s.Id, s => Capture(target, s));
                Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() => review.PreviewMapping(
                    target, Destination(target, before.Worksheets[0].Id, p), review.Snapshot.Review.Version)).Code);
                Assert.AreEqual(before, target.Info);
                foreach (var sheet in before.Worksheets)
                    CollectionAssert.AreEqual(cells[sheet.Id], Capture(target, sheet));
            }
            Assert.IsFalse(copy.Info.IsDirty);
        }
        Assert.AreEqual(beforeReview, JsonSerializer.Serialize(review.Snapshot));
        Assert.AreEqual(workbookHash, Hash(fixture.Path));
        Assert.AreEqual(documentHash, Hash(document.Path));
    }

    private static SourceLinkedFieldReview LiveReview(WorkflowProfile p, LocalDocumentSources sources, RegisteredSourcePage page)
    {
        var review = new SourceLinkedFieldReview(sources, p,
            [new(p.EntityField, "Artificial new entity", page), new(p.Fields[1].Key, " =SUM(A1:A2) ", page),
                new(p.IdentifierFields[0], "SYN-NEW", page), new(p.ContextFields[0], "123.4500", page)]);
        foreach (var key in review.Snapshot.Review.Fields.Where(f => f.Source != null).Select(f => f.Key))
            review.Verify(review.Snapshot.Review.Fields.Single(f => f.Key == key).Binding);
        return review;
    }

    private sealed class MappingDocument : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-artificial-mapping-" + Guid.NewGuid().ToString("N") + ".pdf");
        public MappingDocument() => File.WriteAllText(Path, "%PDF-1.7\n% Artificial signature-bearing bytes only; no parsed pages.\n");
        public void Dispose() => File.Delete(Path);
    }

    private sealed class MappingFixture : IDisposable
    {
        private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-mapping-" + Guid.NewGuid().ToString("N"));
        public string Path { get; }
        public MappingFixture(WorkflowProfile p, string scenario = "supported")
        {
            Directory.CreateDirectory(folder);
            Path = System.IO.Path.Combine(folder, "artificial-mapping.xlsx");
            using var workbook = new XLWorkbook();
            for (var index = 0; index < 2; index++)
            {
                var sheet = workbook.AddWorksheet(index == 0 ? "Artificial first" : "Artificial shifted");
                var offset = index * 20;
                int Column(int field) => index == 0 ? field + 1 : p.Fields.Count - field;
                for (var field = 0; field < p.Fields.Count; field++)
                {
                    sheet.Cell(2, Column(field)).Value = p.Fields[field].Header;
                    if (scenario == "duplicate-headers") sheet.Cell(3, Column(field)).Value = p.Fields[field].Header;
                }
                if (scenario != "duplicate-headers") sheet.Cell(3, Column(1)).Value = "Artificial independent note baseline";
                sheet.Row(2).Style.Font.Bold = true;
                sheet.Cell(5, Column(0)).Value = p.Sections[0].Heading;
                sheet.Cell(10, Column(0)).Value = p.Sections[0].Subtotal;
                sheet.Cell(15 + offset, Column(0)).Value = p.Sections[1].Heading;
                if (scenario != "missing-subtotal") sheet.Cell(22 + offset, Column(0)).Value = p.Sections[1].Subtotal;
                foreach (var row in new[] { 6, 16 + offset })
                {
                    sheet.Cell(row, Column(0)).Value = "Artificial existing entity";
                    sheet.Cell(row, Column(2)).Value = "SYN-EXISTING";
                    sheet.Cell(row, Column(4)).Value = 125.5m;
                    sheet.Cell(row, Column(4)).Style.NumberFormat.Format = "#,##0.0000";
                    sheet.Cell(row, Column(4)).Style.Fill.BackgroundColor = XLColor.LightBlue;
                }
                sheet.Row(16 + offset).Hide();
                var amount = XLHelper.GetColumnLetterFromNumber(Column(4));
                sheet.Cell(10, Column(4)).FormulaA1 = $"SUM({amount}6:{amount}9)";
                sheet.Cell(22 + offset, Column(4)).FormulaA1 = $"SUM({amount}{16 + offset}:{amount}{21 + offset})";
                sheet.Range(25 + offset, 1, 25 + offset, 6).Merge().FirstCell().Value = "Artificial lower footer";
                sheet.Cell(26 + offset, Column(4)).FormulaA1 = $"{amount}10+{amount}{22 + offset}";
            }
            var context = workbook.AddWorksheet("Artificial context");
            context.Cell(1, 1).Value = "Unselected artificial context";
            context.Cell(2, 1).FormulaA1 = "'Artificial first'!E10";
            context.Visibility = XLWorksheetVisibility.Hidden;
            using var output = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            workbook.SaveAs(output, new ClosedXML.Excel.SaveOptions { EvaluateFormulasBeforeSaving = false, GenerateCalculationChain = false });
        }
        public void Dispose() { File.Delete(Path); Directory.Delete(folder); }
    }
}
