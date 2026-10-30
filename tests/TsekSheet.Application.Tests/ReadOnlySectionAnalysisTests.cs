using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class ReadOnlySectionAnalysisTests
{
    private static ReadOnlyRowMarker Marker(string text) => new(text, [1]);
    private static ReadOnlySectionProfile Profile() => new("artificial-ledger", 1, 1, 32, [1, 3, 4, 5, 6],
        [new("vendor", "Entity"), new("rfp", "Request"), new("invoice", "Invoice"), new("amount", "Amount")], "vendor", ["rfp", "invoice"], ["amount"],
        [new("north", "North", null, Marker("North ledger"), new("marker", Marker("North total"))),
         new("tools", "Tools", "north", Marker("Tools"), new("marker", Marker("Tools total"))),
         new("services", "Services", "north", Marker("Services"), new("marker", Marker("Services total"))),
         new("south", "South", null, Marker("South ledger"), new("marker", Marker("South total")))],
        [Marker("Overview")], []);
    private static InspectionRegion Region(Action<Dictionary<(int, int), CellInfo>>? edit = null)
    {
        var cells = new Dictionary<(int, int), CellInfo>();
        for (int r = 1; r <= 32; r++) foreach (var c in new[] { 1, 3, 4, 5, 6 }) cells[(r, c)] = new(r, c, "", "Blank", null, false, new("", -1, false, false, ""));
        void Text(int r, int c, string v) => cells[(r, c)] = new(r, c, v, "Text", null, false, new("General", 0, false, false, ""));
        void Formula(int r, string f) => cells[(r, 6)] = new(r, 6, "99", "Number", f, false, new("0.00", 2, false, false, ""), new("99", "99.00", "Number", "Supported subset", "", "Stored formula; cached value is unverified."));
        foreach (var r in new[] { 1, 9 }) { Text(r, 3, "Entity"); Text(r, 4, "Request"); Text(r, 5, "Invoice"); Text(r, 6, "Amount"); }
        foreach (var (r, s) in new[] { (2, "North ledger"), (3, "Tools"), (8, "Tools total"), (10, "Services"), (18, "Services total"), (20, "North total"), (22, "South ledger"), (30, "South total"), (31, "Overview") }) Text(r, 1, s);
        void Record(int r, string entity, string id) { Text(r, 3, entity); Text(r, 4, id); Text(r, 6, "10"); }
        Record(4, "Alpha Ltd", "SYN-A"); Record(5, "", "SYN-B"); Record(6, "Alpha Ltd", "SYN-A");
        Record(11, "Alpha Ltd", "SYN-C"); Record(12, "Alpha, Ltd", "SYN-D"); Record(23, "Beta Ltd", "SYN-E"); Text(7, 3, "Entity-only note");
        Formula(8, "SUM(F4:F7)"); Formula(18, "SUM(F11:F17)"); Formula(20, "F8+F18"); Formula(30, "SUM(F23:F29)"); Formula(31, "F20+F30");
        edit?.Invoke(cells);
        return new(Guid.NewGuid(), Guid.NewGuid(), 0, new string('A', 64), 1, 32, [1, 3, 4, 5, 6], cells.Values.ToArray(),
            new("A1:XFD32", "Observed sparse extent", cells.Count, 40, 5, 0, 32, 16384, true, []), [5], 32000);
    }
    [TestMethod]
    public void NestedSectionsRepeatedHeadersAndMissingVendorAreExplainableAndNeverActionable()
    {
        var r = ReadOnlySectionAnalysis.Analyze(Region(), Profile());
        Assert.IsTrue(r.HierarchyVerified); Assert.IsTrue(r.Coverage.RecordCoverageComplete); Assert.IsFalse(r.IdentitiesVerified); Assert.IsFalse(r.CanInsert);
        Assert.IsTrue(r.Sections.All(s => !s.CanInsert && s.Status.StartsWith("Provisional"))); Assert.IsTrue(r.Vendors.All(v => !v.SectionCoverageComplete));
        CollectionAssert.AreEqual(new[] { 1, 9 }, r.HeaderRows.ToArray()); Assert.AreEqual(3, r.FieldColumns["vendor"]);
        Assert.AreEqual("tools", r.Records.Single(v => v.Row == 4).SectionId); Assert.AreEqual("services", r.Records.Single(v => v.Row == 11).SectionId);
        var missing = r.Records.Single(v => v.Row == 5); Assert.IsNull(missing.Entity); Assert.IsTrue(missing.Hidden); Assert.AreEqual("Missing literal vendor identity", missing.Classification);
        CollectionAssert.AreEqual(new[] { 7 }, r.EntityOnlyRows.ToArray()); Assert.HasCount(1, r.PotentialDuplicates); Assert.HasCount(1, r.SimilarNames);
        CollectionAssert.AreEqual(new[] { "services", "tools" }, r.Vendors.Single(v => v.Entity == "ALPHA LTD").SectionIds.ToArray());
        Assert.IsTrue(r.SummaryDependencies.All(d => !d.Complete)); Assert.IsTrue(r.SummaryDependencies.Any(d => d.Row == 31 && d.LocalReferences.Contains("F20")));
    }
    [TestMethod]
    public void RecordsOutsideConfiguredRegionsInvalidateCoverageInsteadOfNarrowingSearch()
    {
        var region = Region(c => { c[(21, 3)] = c[(4, 3)] with { Row = 21 }; c[(21, 4)] = c[(4, 4)] with { Row = 21 }; });
        var r = ReadOnlySectionAnalysis.Analyze(region, Profile());
        Assert.IsFalse(r.HierarchyVerified); Assert.IsFalse(r.Coverage.RecordCoverageComplete); CollectionAssert.AreEqual(new[] { 21 }, r.UnassignedRows.ToArray());
    }
    [TestMethod]
    public void IntermediateSumIsNotSelectedWhenMultipleExactIntentCandidatesExist()
    {
        var p = Profile(); p = p with { Sections = p.Sections.Select(s => s.Id == "north" ? s with { End = new("contiguous-sum", FormulaField: "amount") } : s).ToArray() };
        var region = Region(c => { c[(8, 6)] = c[(8, 6)] with { Formula = "SUM(F3:F7)" }; c[(20, 6)] = c[(20, 6)] with { Formula = "SUM(F3:F19)" }; });
        var r = ReadOnlySectionAnalysis.Analyze(region, p); var root = r.Sections.Single(s => s.Id == "north");
        CollectionAssert.AreEqual(new[] { 8, 20 }, root.SubtotalCandidates.ToArray()); Assert.IsNull(root.SubtotalRow); Assert.IsFalse(r.HierarchyVerified);
    }
    [TestMethod]
    public void AUniqueIntermediateSumStillFailsBecauseLaterRecordsAreUnassigned()
    {
        var p = Profile(); p = p with { Sections = [p.Sections[0] with { End = new("contiguous-sum", FormulaField: "amount") }, p.Sections[3]] };
        var region = Region(c => c[(8, 6)] = c[(8, 6)] with { Formula = "SUM(F3:F7)" });
        var r = ReadOnlySectionAnalysis.Analyze(region, p);
        Assert.AreEqual(8, r.Sections[0].SubtotalRow); Assert.IsFalse(r.HierarchyVerified); Assert.IsTrue(r.UnassignedRows.Contains(11));
    }
    [TestMethod]
    public void DuplicateSubtotalMarkersAreAmbiguous()
    {
        var r = ReadOnlySectionAnalysis.Analyze(Region(c => c[(19, 1)] = c[(20, 1)] with { Row = 19 }), Profile());
        Assert.IsFalse(r.HierarchyVerified); Assert.IsTrue(r.Diagnostics.Any(d => d.Code == "AMBIGUOUS_BOUNDARY"));
    }
    [TestMethod]
    public void OverlapAndIncorrectParentContainmentAreNotCertified()
    {
        var p = Profile(); p = p with { Sections = p.Sections.Select(s => s.Id == "services" ? s with { End = new("marker", Marker("South total")) } : s).ToArray() };
        var r = ReadOnlySectionAnalysis.Analyze(Region(), p);
        Assert.IsFalse(r.HierarchyVerified); Assert.IsTrue(r.Diagnostics.Any(d => d.Code == "INVALID_NESTING")); Assert.IsTrue(r.Diagnostics.Any(d => d.Code == "OVERLAPPING_REGIONS"));
    }
    [TestMethod]
    public void RepeatedShiftedHeadersAndRecordLikeExclusionsAreRejected()
    {
        var p = Profile();
        var repeated = ReadOnlySectionAnalysis.Analyze(Region(), p with { AllowRepeatedHeaders = false }); Assert.IsFalse(repeated.HierarchyVerified);
        var shifted = ReadOnlySectionAnalysis.Analyze(Region(c => { var a = c[(9, 3)]; c[(9, 3)] = c[(9, 4)] with { Column = 3 }; c[(9, 4)] = a with { Column = 4 }; }), p);
        Assert.IsTrue(shifted.Diagnostics.Any(d => d.Code == "SHIFTED_HEADER")); Assert.IsFalse(shifted.HierarchyVerified);
        var marker = ReadOnlySectionAnalysis.Analyze(Region(c => c[(8, 4)] = c[(4, 4)] with { Row = 8 }), p);
        Assert.IsTrue(marker.Diagnostics.Any(d => d.Code == "MARKER_RESEMBLES_RECORD")); Assert.IsFalse(marker.Coverage.RecordCoverageComplete);
    }
    [TestMethod]
    public void FormulaOrTruncatedIdentitiesNeverUseCachesAsVendorNames()
    {
        foreach (bool formula in new[] { true, false })
        {
            var r = ReadOnlySectionAnalysis.Analyze(Region(c => c[(4, 3)] = c[(4, 3)] with { Formula = formula ? "C3" : null, Truncated = !formula }), Profile());
            Assert.IsNull(r.Records.Single(v => v.Row == 4).Entity); Assert.IsFalse(r.IdentitiesVerified);
        }
    }
    [TestMethod]
    public void IncompleteCellOrRowCoverageCannotPass()
    {
        var region = Region();
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.Analyze(region with { Cells = region.Cells.Skip(1).ToArray() }, Profile()));
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.Analyze(region with { Cells = region.Cells.Skip(1).Append(region.Cells[1]).ToArray() }, Profile()));
        var report = ReadOnlySectionAnalysis.Analyze(region with { Scan = region.Scan with { LastRow = 100 } }, Profile());
        Assert.IsFalse(report.Coverage.EntireWorksheetRowsCaptured); Assert.IsFalse(report.HierarchyVerified); Assert.IsFalse(report.IdentitiesVerified);
        var styledTail = ReadOnlySectionAnalysis.Analyze(region with { Scan = region.Scan with { LastRowElement = 100 } }, Profile());
        Assert.IsFalse(styledTail.Coverage.EntireWorksheetRowsCaptured); Assert.AreEqual(100,styledTail.Coverage.ObservedLastRow);
    }
    [TestMethod]
    public void ProfileCyclesResourceLimitsAndCancellationReject()
    {
        var p = Profile();
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.Validate(p with { EndRow = 2001 }));
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.Validate(p with { Sections = p.Sections.Select(s => s.Id == "north" ? s with { ParentId = "tools" } : s).ToArray() }));
        using var c = new CancellationTokenSource(); c.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => ReadOnlySectionAnalysis.Analyze(Region(), p, c.Token));
    }
    [TestMethod]
    public void HeaderAliasesAreExplicitAndHeaderLikeRowsNeverBecomeVendors()
    {
        var p = Profile(); var region = Region(c => c[(9, 3)] = c[(9, 3)] with { Value = "Payee" });
        var incomplete = ReadOnlySectionAnalysis.Analyze(region, p);
        Assert.IsFalse(incomplete.HierarchyVerified); Assert.IsTrue(incomplete.Diagnostics.Any(d => d.Code == "INCOMPLETE_HEADER")); Assert.IsFalse(incomplete.Vendors.Any(v => v.Entity == "PAYEE"));
        p = p with { Fields = p.Fields.Select(f => f.Key == "vendor" ? f with { HeaderAliases = ["Payee"] } : f).ToArray() };
        var r = ReadOnlySectionAnalysis.Analyze(region, p); CollectionAssert.AreEqual(new[] { 1, 9 }, r.HeaderRows.ToArray()); Assert.IsTrue(r.HierarchyVerified);
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.Validate(p with { Fields = p.Fields.Select(f => f.Key == "rfp" ? f with { HeaderAliases = ["Payee"] } : f).ToArray() }));
    }
    [TestMethod]
    public void MarkerIdentityConflictIsRetainedAsUnresolvedRecordAndCannotHideMissingVendor()
    {
        var r = ReadOnlySectionAnalysis.Analyze(Region(c => c[(8, 4)] = c[(5, 4)] with { Row = 8 }), Profile());
        var conflict = r.Records.Single(v => v.Row == 8); Assert.IsTrue(conflict.MarkerConflict); Assert.IsNull(conflict.Entity);
        Assert.AreEqual("Missing literal vendor identity", conflict.Classification); Assert.IsFalse(r.Coverage.RecordCoverageComplete);
    }
    [TestMethod]
    public void UnconfiguredFormulaSummaryOutsideSectionsRemainsUnassigned()
    {
        var region = Region(c => c[(32, 6)] = c[(31, 6)] with { Row = 32 });
        var r = ReadOnlySectionAnalysis.Analyze(region, Profile()); Assert.IsFalse(r.HierarchyVerified); Assert.IsTrue(r.UnassignedRows.Contains(32));
        Assert.IsTrue(r.Diagnostics.Any(d => d.Code == "UNCLASSIFIED_SUMMARY"));
    }
    [TestMethod]
    public void ProfileJsonIsBoundedStrictAndNotAnEditableWorkflowProfile()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(Profile(), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        var parsed = ReadOnlySectionAnalysis.ParseProfile(json); Assert.AreEqual(ReadOnlySectionAnalysis.Fingerprint(Profile()), ReadOnlySectionAnalysis.Fingerprint(parsed));
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.ParseProfile(json[..^1] + ",\"unknown\":true}"));
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.ParseProfile(json[..^1] + ",\"id\":\"duplicate\"}"));
        Assert.ThrowsExactly<WorkbookException>(() => ReadOnlySectionAnalysis.ParseProfile(new string(' ',65537)));
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Parse(json));
    }
    [TestMethod]
    public void ReadWrapperChecksSessionSheetRevisionAndSourceBinding()
    {
        var region=Region();using var fake=new InspectionStub(region);
        var valid=ReadOnlySectionAnalysis.Read(fake,region.WorksheetId,0,Profile());Assert.IsFalse(valid.CanInsert);
        Assert.ThrowsExactly<WorkbookException>(()=>ReadOnlySectionAnalysis.Read(fake,region.WorksheetId,1,Profile()));
        Assert.ThrowsExactly<WorkbookException>(()=>ReadOnlySectionAnalysis.Read(fake,Guid.NewGuid(),0,Profile()));
        fake.WrongCapture=true;Assert.ThrowsExactly<WorkbookException>(()=>ReadOnlySectionAnalysis.Read(fake,region.WorksheetId,0,Profile()));
        fake.WrongCapture=false;fake.ChangeSource=true;Assert.ThrowsExactly<WorkbookException>(()=>ReadOnlySectionAnalysis.Read(fake,region.WorksheetId,0,Profile()));
    }
    [TestMethod]
    public void ReviewIdentityIsSupplementalRetractableAndReusesSingleCapture()
    {
        var region = Region(); using var source = new InspectionStub(region); var review = new SectionReview(source, region.WorksheetId, 0, Profile());
        var initial = review.Snapshot; var decision = new SectionReviewDecision(Guid.NewGuid(), "identity", 5, null, null, 4, 3, "Artificial source evidence checked individually");
        var updated = review.Decide(new(initial.Binding, decision));
        Assert.AreEqual(1, initial.Before.UncertainIdentities); Assert.AreEqual(0, updated.After.UncertainIdentities);
        Assert.AreEqual("Reviewer-confirmed identity", updated.Report.Records.Single(r => r.Row == 5).Classification);
        Assert.AreEqual("", region.Cells.Single(c => c.Row == 5 && c.Column == 3).Value);
        Assert.AreEqual(1, source.Captures); Assert.IsFalse(updated.CanInsert); Assert.IsFalse(updated.PreservationVerified);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { syntheticCaptureMilliseconds = updated.CaptureMilliseconds, syntheticDecisionReanalysisMilliseconds = updated.ReanalysisMilliseconds, captureCount = source.Captures }));
        var retracted = review.Retract(new(updated.Binding, decision.Id)); Assert.AreEqual(1, retracted.After.UncertainIdentities);
        Assert.AreEqual(1, source.Captures); Assert.AreEqual(2, retracted.Binding.ReviewRevision);
        Assert.ThrowsExactly<WorkbookException>(() => review.Decide(new(retracted.Binding, decision)));
    }
    [TestMethod]
    public void ReviewRejectsForgedIdentityTypedVendorAndUncapturedEvidence()
    {
        var region = Region(); using var source = new InspectionStub(region); var review = new SectionReview(source, region.WorksheetId, 0, Profile());
        foreach (var d in new[] {
            new SectionReviewDecision(Guid.NewGuid(),"identity",5,null,"Arbitrary entity",4,3,"Artificial reason"),
            new SectionReviewDecision(Guid.NewGuid(),"identity",5,null,null,4,4,"Wrong evidence role"),
            new SectionReviewDecision(Guid.NewGuid(),"identity",5,null,null,33,3,"Outside capture"),
            new SectionReviewDecision(Guid.NewGuid(),"identity",4,null,null,6,3,"Literal source cannot be replaced"),
            new SectionReviewDecision(Guid.NewGuid(),"header",4,null,"vendor",4,3,"A record is not a header") })
            Assert.ThrowsExactly<WorkbookException>(() => review.Decide(new(review.Snapshot.Binding,d)));
        Assert.AreEqual(0,review.Snapshot.Binding.ReviewRevision);
    }
    [TestMethod]
    public void ReviewBindingsRejectEveryStaleOrTamperedDimensionAndReplay()
    {
        var region=Region();using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());var b=review.Snapshot.Binding;
        var decision=new SectionReviewDecision(Guid.NewGuid(),"missing",5,null,null,null,null,"Needs source documentation");
        foreach(var wrong in new[]{b with {ReviewId=Guid.NewGuid()},b with {SessionId=Guid.NewGuid()},b with {WorksheetId=Guid.NewGuid()},b with {WorksheetOrder=2},b with {CaptureRevision=1},b with {SourceSha256=new string('B',64)},b with {ProfileVersion=2},b with {ProfileSha256=new string('B',64)},b with {ReviewRevision=1}})
            Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(wrong,decision)));
        var r=review.Decide(new(b,decision));Assert.IsTrue(r.VendorIdentitiesReviewed);Assert.AreEqual(1,r.After.UncertainIdentities);Assert.IsFalse(r.Report.IdentitiesVerified);
        Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(b,decision)));Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(r.Binding,decision)));
        Assert.ThrowsExactly<WorkbookException>(()=>review.Retract(new(b,decision.Id)));
    }
    [TestMethod]
    public void ReviewedNonRecordRetainsOriginalRowsAndDuplicateWarnings()
    {
        var region=Region();using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());var initial=review.Snapshot;
        var r=review.Decide(new(initial.Binding,new(Guid.NewGuid(),"non-record",6,null,null,null,null,"Artificial duplicate-looking summary reviewed")));
        CollectionAssert.AreEqual(initial.OriginalRecordRows.ToArray(),r.OriginalRecordRows.ToArray());Assert.AreEqual(initial.Before.RecordShapedRows,r.After.RecordShapedRows);
        Assert.HasCount(1,r.Report.PotentialDuplicates);Assert.AreEqual(initial.DuplicateKeys[0],r.DuplicateKeys[0]);Assert.IsFalse(r.CanInsert);
        var ack=new SectionReviewDecision(Guid.NewGuid(),"duplicate",4,null,r.DuplicateKeys[0],null,null,"Warning inspected; uniqueness not asserted");
        r=review.Decide(new(r.Binding,ack));Assert.HasCount(1,r.Report.PotentialDuplicates);r=review.Retract(new(r.Binding,ack.Id));Assert.HasCount(1,r.Report.PotentialDuplicates);
    }
    [TestMethod]
    public void HeaderVariantRequiresExplicitLiteralRoleEvidenceAndCanBeRetracted()
    {
        var region=Region(c=>c[(9,3)]=c[(9,3)] with {Value="Payee"});using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());
        Assert.AreEqual(1,review.Snapshot.Report.HeaderRows.Count);
        var d=new SectionReviewDecision(Guid.NewGuid(),"header",9,null,"vendor",9,3,"Artificial repeated variant checked");var r=review.Decide(new(review.Snapshot.Binding,d));
        CollectionAssert.AreEqual(new[]{1,9},r.Report.HeaderRows.ToArray());Assert.IsTrue(r.Report.HierarchyVerified);
        r=review.Retract(new(r.Binding,d.Id));Assert.AreEqual(1,r.Report.HeaderRows.Count);Assert.IsFalse(r.HierarchyVerified);
    }
    [TestMethod]
    public void ReviewedBoundaryChoosesActualCandidatesAndDoesNotProveFormulaPreservation()
    {
        var p=Profile();p=p with {Sections=[p.Sections[0] with {End=new("contiguous-sum",FormulaField:"amount")},p.Sections[3]]};
        var region=Region(c=>{c[(8,6)]=c[(8,6)] with {Formula="SUM(F3:F7)"};c[(20,6)]=c[(20,6)] with {Formula="SUM(F3:F19)"};});using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,p);
        Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(review.Snapshot.Binding,new(Guid.NewGuid(),"boundary",19,"north",null,null,null,"Not an actual candidate"))));
        var d=new SectionReviewDecision(Guid.NewGuid(),"boundary",20,"north",null,null,null,"Final boundary asserted from source");var r=review.Decide(new(review.Snapshot.Binding,d));Assert.AreEqual(20,r.Report.Sections[0].SubtotalRow);
        r=review.Decide(new(r.Binding,new(Guid.NewGuid(),"intermediate",8,null,null,null,null,"Intermediate aggregation only")));Assert.AreEqual(1,r.After.IntermediateSubtotals);Assert.IsFalse(r.CanInsert);Assert.IsFalse(r.PreservationVerified);
        Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(r.Binding,new(Guid.NewGuid(),"intermediate",20,null,null,null,null,"Conflicting final subtotal"))));
        r=review.Retract(new(r.Binding,d.Id));Assert.IsNull(r.Report.Sections[0].SubtotalRow);Assert.IsFalse(r.HierarchyVerified);
    }
    [TestMethod]
    public void ParentCyclesIncorrectContainmentAndOverlapCannotBeApproved()
    {
        var region=Region();using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());
        foreach(var d in new[]{new SectionReviewDecision(Guid.NewGuid(),"parent",2,"north","tools",null,null,"Cycle"),new SectionReviewDecision(Guid.NewGuid(),"parent",3,"tools","south",null,null,"Outside parent"),new SectionReviewDecision(Guid.NewGuid(),"parent",3,"tools",null,null,null,"Overlapping roots")})
            Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(review.Snapshot.Binding,d)));
        Assert.AreEqual(0,review.Snapshot.Binding.ReviewRevision);Assert.IsFalse(review.Snapshot.CanInsert);
    }
    [TestMethod]
    public void SummaryAndInvestigationRetainUnresolvedDependenciesAndCoverageAccounting()
    {
        var region=Region(c=>c[(32,6)]=c[(31,6)] with {Row=32});using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());
        var initial=review.Snapshot;Assert.AreEqual(1,initial.After.UnassignedMeaningfulRows);
        var r=review.Decide(new(initial.Binding,new(Guid.NewGuid(),"summary",32,null,null,null,null,"Artificial summary classified")));
        Assert.AreEqual(0,r.After.UnassignedMeaningfulRows);Assert.AreEqual(initial.Before.RecordShapedRows,r.After.RecordShapedRows);Assert.IsTrue(r.After.UnresolvedDependencies>=initial.Before.UnresolvedDependencies);
        r=review.Decide(new(r.Binding,new(Guid.NewGuid(),"investigate",32,null,null,null,null,"Aggregation intent still needs investigation")));
        Assert.IsFalse(r.HierarchyVerified);Assert.IsFalse(r.RecordCoverageReviewed);Assert.IsFalse(r.CanInsert);
    }
    [TestMethod]
    public void CancellationBudgetsAndChangedSourceDoNotCommitAssertions()
    {
        var region=Region();using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());using var cts=new CancellationTokenSource();cts.Cancel();
        var d=new SectionReviewDecision(Guid.NewGuid(),"missing",5,null,null,null,null,"Synthetic reason");
        Assert.ThrowsExactly<OperationCanceledException>(()=>review.Decide(new(review.Snapshot.Binding,d),cts.Token));Assert.AreEqual(0,review.Snapshot.Binding.ReviewRevision);
        Assert.ThrowsExactly<WorkbookException>(()=>review.Decide(new(review.Snapshot.Binding,d with {Reason=new string('r',513)})));
        Assert.ThrowsExactly<WorkbookException>(()=>new SectionReview(source,region.WorksheetId,0,Profile() with {EndRow=2001}));
        source.ForceChanged=true;Assert.ThrowsExactly<WorkbookException>(()=>review.ValidateCurrent(source));
    }
    [TestMethod]
    public void ReviewedConfigurationAndCoverageRequireExplicitHeaderSectionAndParentAssertions()
    {
        var region=Region();using var source=new InspectionStub(region);var review=new SectionReview(source,region.WorksheetId,0,Profile());
        Assert.IsFalse(review.Snapshot.ConfigurationReviewed);Assert.IsFalse(review.Snapshot.RecordCoverageReviewed);
        foreach(var s in review.Snapshot.Report.Sections.ToArray())
        {
            review.Decide(new(review.Snapshot.Binding,new(Guid.NewGuid(),"heading",s.HeadingRow!.Value,s.Id,null,s.HeadingRow,1,"Artificial configured heading explicitly checked")));
            review.Decide(new(review.Snapshot.Binding,new(Guid.NewGuid(),"boundary",s.SubtotalRow!.Value,s.Id,null,null,null,"Artificial configured end explicitly checked")));
            if(s.ParentId!=null)review.Decide(new(review.Snapshot.Binding,new(Guid.NewGuid(),"parent",s.HeadingRow.Value,s.Id,s.ParentId,null,null,"Artificial parent containment checked")));
        }
        Assert.IsFalse(review.Snapshot.ConfigurationReviewed);
        foreach(var row in new[]{1,9})review.Decide(new(review.Snapshot.Binding,new(Guid.NewGuid(),"header",row,null,"vendor",row,3,"Artificial complete repeated header checked")));
        Assert.IsTrue(review.Snapshot.ConfigurationReviewed);Assert.IsTrue(review.Snapshot.RecordCoverageReviewed);Assert.IsFalse(review.Snapshot.VendorIdentitiesReviewed);
        Assert.IsTrue(review.Snapshot.HierarchyVerified);Assert.IsFalse(review.Snapshot.Report.IdentitiesVerified);Assert.IsFalse(review.Snapshot.CanInsert);
    }
    private sealed class InspectionStub(InspectionRegion region):IWorkbookSession,IInspectionAnalysisSession
    {
        public bool WrongCapture,ChangeSource,ForceChanged;
        public int Captures;
        private bool changed;
        public WorkbookInfo Info=>new(region.SessionId,null,"synthetic.xlsx",changed||ForceChanged?new string('B',64):region.SourceSha256,0,false,false,"","",
          [new(region.WorksheetId,"Artificial",1,"Visible",0,0)],[],Compatibility:new(1,1,1,1,0,0,false,[],[],"Unmeasured"));
        public InspectionRegion CaptureInspectionRegion(InspectionRegionRequest request,CancellationToken token=default){Captures++;token.ThrowIfCancellationRequested();if(ChangeSource)changed=true;return WrongCapture?region with {SessionId=Guid.NewGuid()}:region;}
        public WorkingCopyInfo? WorkingCopy=>null;
        public CellViewport ReadViewport(Guid id,int r,int c,int rows,int cols)=>throw new NotSupportedException();
        public IWorkbookSession CreateSnapshot(CancellationToken t=default)=>throw new NotSupportedException();
        public IWorkbookSession CreateWorkingCopy(Guid id,int rev,CancellationToken t=default)=>throw new NotSupportedException();
        public WorkbookInfo Edit(CellEdit edit,CancellationToken t=default)=>throw new NotSupportedException();
        public SaveReceipt SaveAsNew(string path,int rev,CancellationToken t=default)=>throw new NotSupportedException();
        public void Dispose(){}
    }
}
