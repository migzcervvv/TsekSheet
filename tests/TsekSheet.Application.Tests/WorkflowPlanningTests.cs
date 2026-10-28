using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Tests;

[TestClass]
public sealed class WorkflowPlanningTests
{
    private static WorkflowProfile Profile => WorkflowProfiles.Get("synthetic-payables");
    private static WorkflowRecord Input(string vendor = "Other Tools Ltd", string id = "one", string rfp = "NEW", string invoice = "NEW-INV", string? section = null,
        string? selectedVendor = null, bool confirmNew = false) => new(id,
        new Dictionary<string, string> { ["vendor"] = vendor, ["rfp"] = rfp, ["invoice"] = invoice, ["amount"] = "200.00", ["particulars"] = "Artificial record" }, section, selectedVendor, confirmNew);

    internal static (WorkbookInfo Info, WorksheetInfo Sheet, Dictionary<(int Row, int Column), CellInfo> Cells) Capture(WorkflowProfile p, int offset = 0)
    {
        var sheet = new WorksheetInfo(Guid.NewGuid(), "Any selected sheet", 1, "Visible", 25 + offset, 6);
        var info = new WorkbookInfo(Guid.NewGuid(), null, "synthetic.xlsx", new string('A', 64), 0, false, true, "", "", [sheet], []);
        var cells = new Dictionary<(int, int), CellInfo>();
        for (var r = 1; r <= sheet.LastRow; r++) for (var c = 1; c <= p.MaxColumns; c++) Put(r, c, "");
        for (var c = 0; c < p.Fields.Count; c++) Put(2, c + 1, p.Fields[c].Header);
        Put(5, 1, p.Sections[0].Heading); Put(10, 1, p.Sections[0].Subtotal);
        Put(15 + offset, 1, p.Sections[1].Heading); Put(22 + offset, 1, p.Sections[1].Subtotal);
        Row(6, "Acme Trading Incorporated", "R1", "I1"); Row(7, "Other Tools Ltd", "R2", "I2"); Row(8, "Acme Trading Incorporated", "R3", "I3");
        Row(16 + offset, "Cross Vendor Ltd", "X1", "XI1"); Row(17 + offset, "Acme Trading Incorporated", "R4", "I4"); Row(19 + offset, "Cross Vendor Ltd", "X2", "XI2");
        Put(9, 1, "Synthetic note; not a vendor"); Put(25 + offset, 1, "Summary");
        return (info, sheet, cells);
        void Put(int r, int c, string value) => cells[(r,c)] = new(r,c,value,value == "" ? "Blank" : "Text",null,false,new("",0,false,false,""));
        void Row(int r, string vendor, string rfp, string invoice) { Put(r,1,vendor); Put(r,3,rfp); Put(r,4,invoice); Put(r,5,"100"); }
    }
    private static WorksheetAnalysis Analyze(int offset = 0) { var data = Capture(Profile, offset); return WorksheetSectionAnalyzer.Analyze(data.Info, data.Sheet, Profile, data.Cells); }
    private static WorkflowPlan Plan(params WorkflowRecord[] records) => WorkflowPlanning.Plan(Analyze(), Profile, records);

    [TestMethod]
    public void ProfilesAreVersionedStrictAndReusableBeyondPayables()
    {
        Assert.AreEqual(2, WorkflowProfiles.All.Count);
        foreach (var p in WorkflowProfiles.All)
        {
            var data = Capture(p); var analysis = WorksheetSectionAnalyzer.Analyze(data.Info, data.Sheet, p, data.Cells);
            var values = new Dictionary<string,string> { [p.EntityField] = "New Organization", [p.IdentifierFields[0]] = "TEST-1",
                [p.Fields.Single(f => f.Kind == "decimal").Key] = "123.50" };
            var plan = WorkflowPlanning.Plan(analysis,p,[new("one",values)]);
            Assert.AreEqual(p.FallbackSectionId,plan.Records[0].SectionId); Assert.IsTrue(plan.ReadyForReview); Assert.IsFalse(plan.CanApply);
        }
    }

    [TestMethod]
    [DataRow("version")] [DataRow("unknown")] [DataRow("apply")] [DataRow("alias")] [DataRow("field")] [DataRow("bounds")] [DataRow("policy")] [DataRow("review")]
    public void InvalidProfileConfigurationsAreRejected(string scenario)
    {
        var json = JsonNode.Parse(JsonSerializer.Serialize(Profile, new JsonSerializerOptions(JsonSerializerDefaults.Web)))!;
        switch (scenario)
        {
            case "version": json["schemaVersion"] = 2; break;
            case "unknown": json["script"] = "unsupported"; break;
            case "apply": json["insertionPolicy"] = "enabled"; break;
            case "alias": json["aliases"]!.AsArray().Add(new JsonObject { ["alias"] = "Acme Trading", ["canonical"] = "Different legal entity" }); break;
            case "field": json["fields"]![1]!["key"] = "vendor"; break;
            case "bounds": json["maxRows"] = 1000000; break;
            case "policy": json["duplicatePolicy"] = "discard"; break;
            case "review": json["requireReview"] = false; break;
        }
        Assert.AreEqual("INVALID_PROFILE", Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Parse(json.ToJsonString())).Code);
    }

    [TestMethod]
    public void DuplicateJsonAndAliasChainsAreRejected()
    {
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Parse("{\"schemaVersion\":1,\"schemaVersion\":1}"));
        var profile = Profile with { Aliases = [new("a", "b"), new("b", "c")] };
        Assert.ThrowsExactly<WorkbookException>(() => WorkflowProfiles.Validate(profile));
    }

    [TestMethod]
    public void EntireWorksheetMatchingKeepsAllSectionsAndIgnoresNotes()
    {
        var plan = Plan(Input()); var record = plan.Records.Single();
        Assert.AreEqual("internal", record.SectionId); Assert.AreEqual(8, record.BaseInsertionRow); Assert.IsFalse(plan.CanApply);
        Assert.AreEqual(6, Analyze().Sections.Sum(s => s.Records.Count));
        Assert.AreEqual(4, plan.RequiredConfirmations.Count); Assert.IsTrue(plan.IntegrityConcerns.Count > 0);
        Assert.AreEqual(1, record.Fields.Single(f => f.Key == "vendor").Column);
    }

    [TestMethod]
    public void MultipleSectionsRequireExplicitValidatedChoice()
    {
        var first = Plan(Input("Acme Trading Incorporated"));
        Assert.IsFalse(first.ReadyForReview); Assert.IsTrue(first.Records[0].RequiresSectionChoice); Assert.AreEqual(2, first.Records[0].Matches.Count);
        Assert.IsNull(first.Records[0].BaseInsertionRow);
        var chosen = Plan(Input("Acme Trading Incorporated",section:"external"));
        Assert.AreEqual(18,chosen.Records[0].BaseInsertionRow);
        Assert.ThrowsExactly<WorkbookException>(() => Plan(Input("Other Tools Ltd",section:"external")));
    }

    [TestMethod]
    public void MissingVendorFallsBackBeforeSubtotalAndTracksMovedBoundaries()
    {
        Assert.AreEqual(22,Plan(Input("Novel Company")).Records[0].BaseInsertionRow);
        var shifted = WorkflowPlanning.Plan(Analyze(20),Profile,[Input("Novel Company")]);
        Assert.AreEqual(42, shifted.Records[0].BaseInsertionRow); Assert.AreEqual(45,shifted.Records[0].AffectedThroughRow);
    }

    [TestMethod]
    public void NormalizationPreservesLegalNamesAndAliasesRemainExplicit()
    {
        Assert.AreEqual(8, Plan(Input("  other   tools LTD  ")).Records[0].BaseInsertionRow);
        Assert.AreNotEqual(WorkflowProfiles.Entity("Acme Ltd",Profile),WorkflowProfiles.Entity("Acme Inc",Profile));
        var alias = Plan(Input("Acme Trading")); Assert.AreEqual("configured alias",alias.Records[0].MatchReason); Assert.IsTrue(alias.Records[0].RequiresSectionChoice);
        var similar = Plan(Input("Other Tools Limited")); Assert.IsTrue(similar.Records[0].RequiresVendorChoice); Assert.IsNull(similar.Records[0].SectionId);
        var accepted = Plan(Input("Other Tools Limited",selectedVendor:"Other Tools Ltd")); Assert.AreEqual(8,accepted.Records[0].BaseInsertionRow);
        var newEntity = Plan(Input("Other Tools Limited",confirmNew:true)); Assert.AreEqual(22,newEntity.Records[0].BaseInsertionRow);
        Assert.ThrowsExactly<WorkbookException>(() => Plan(Input("Other Tools Limited",selectedVendor:"Not a candidate")));
    }

    [TestMethod]
    [DataRow("R2","different")] [DataRow("different","I2")]
    public void DuplicateIdentifiersAreExplainedWithoutDiscardingRecords(string rfp,string invoice)
    {
        var plan = Plan(Input(rfp:rfp,invoice:invoice));
        Assert.AreEqual(1,plan.Records.Count); Assert.AreEqual(1,plan.Records[0].Duplicates.Count);
        Assert.AreEqual(7,plan.Records[0].Duplicates[0].ExistingRow);
        StringAssert.Contains(plan.Records[0].Duplicates[0].Reason,"amount");
    }

    [TestMethod]
    public void BatchOrderingAndDuplicateWarningsRemainDeterministic()
    {
        var plan = Plan(Input(id:"one"),Input(id:"two"),Input("Cross Vendor Ltd",id:"three",rfp:"THREE",invoice:"THREE-I"),Input("Novel Company",id:"four"));
        CollectionAssert.AreEqual(new int?[] {8,9,22,25},plan.Records.Select(r=>r.ProposedRow).ToArray());
        Assert.IsTrue(plan.Records[0].Duplicates.Any(d=>d.BatchRecordId=="two")); Assert.IsTrue(plan.Records[1].Duplicates.Any(d=>d.BatchRecordId=="one"));
        Assert.IsFalse(plan.CanApply);
    }

    [TestMethod]
    [DataRow("heading")] [DataRow("subtotal")] [DataRow("headers")] [DataRow("outside")] [DataRow("formula")] [DataRow("no-id")]
    [DataRow("heading-record")] [DataRow("subtotal-record")]
    public void AmbiguousOrUnsafeSectionsAreRejected(string scenario)
    {
        var d=Capture(Profile);
        void Set(int r,int c,string value,string? formula=null) => d.Cells[(r,c)] = d.Cells[(r,c)] with { Value=value,Kind="Text",Formula=formula };
        switch(scenario)
        {
            case "heading": Set(12,1,Profile.Sections[0].Heading); break;
            case "subtotal": Set(10,1,""); break;
            case "headers": for(var c=0;c<Profile.Fields.Count;c++)Set(3,c+1,Profile.Fields[c].Header); break;
            case "outside": Set(24,1,"Outside Vendor"); Set(24,3,"UNCLASSIFIED"); break;
            case "formula": Set(6,1,"Cached name","A1"); break;
            case "no-id": Set(6,3,""); Set(6,4,""); break;
            case "heading-record": Set(5,3,"AMBIGUOUS-RFP"); break;
            case "subtotal-record": Set(10,4,"AMBIGUOUS-INVOICE"); break;
        }
        Assert.ThrowsExactly<WorkbookException>(()=>WorksheetSectionAnalyzer.Analyze(d.Info,d.Sheet,Profile,d.Cells));
    }

    [TestMethod]
    public void MissingCaptureLimitsProfileMismatchAndCancellationRejectPlanning()
    {
        var d=Capture(Profile); d.Cells.Remove((4,2));
        Assert.ThrowsExactly<WorkbookException>(()=>WorksheetSectionAnalyzer.Analyze(d.Info,d.Sheet,Profile,d.Cells));
        var tooLarge=d.Sheet with {LastRow=3000};
        Assert.ThrowsExactly<WorkbookException>(()=>WorksheetSectionAnalyzer.Analyze(d.Info with {Worksheets=[tooLarge]},tooLarge,Profile,d.Cells));
        Assert.ThrowsExactly<WorkbookException>(()=>WorkflowPlanning.Plan(Analyze() with {ProfileHash="wrong"},Profile,[Input()]));
        using var cts=new CancellationTokenSource(); cts.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(()=>WorkflowPlanning.Plan(Analyze(),Profile,[Input()],cts.Token));
    }

    [TestMethod]
    [DataRow("unknown")] [DataRow("required")] [DataRow("number")] [DataRow("date")] [DataRow("identifier")]
    public void FieldValidationRejectsUnsupportedValues(string scenario)
    {
        var input=Input(); var values=input.Values.ToDictionary();
        switch(scenario) {case "unknown":values["arbitrary"]="x";break;case "required":values["vendor"]="";break;case "number":values["amount"]="1,23";break;case "date":values["date"]="tomorrow";break;case "identifier":values["rfp"]="";values["invoice"]="";break;}
        Assert.ThrowsExactly<WorkbookException>(()=>Plan(input with {Values=values}));
    }
}
