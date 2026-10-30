using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks.Tests;

public sealed partial class WorkbookInspectionTests
{
    private string MixedStyles(string definitions, string formats, string bases = "") =>
        $"<styleSheet xmlns=\"{Ns}\"><numFmts>{definitions}</numFmts>{(bases.Length == 0 ? "" : "<cellStyleXfs>" + bases + "</cellStyleXfs>")}<cellXfs>{formats}</cellXfs></styleSheet>";
    private static string Def(int id, string code) => new System.Xml.Linq.XElement("numFmt", new System.Xml.Linq.XAttribute("numFmtId", id), new System.Xml.Linq.XAttribute("formatCode", code)).ToString();
    private IReadOnlyList<CellInfo> FormatCells(string styles, string raw = "12.5")
    {
        var path = InterpretedPackage($"<row r=\"1\"><c r=\"A1\" s=\"0\"><v>{raw}</v></c><c r=\"B1\" s=\"1\"><v>{raw}</v></c></row>", stylesheet: styles);
        var before = Hash(path); using var session = new WorkbookInspection().Open(path);
        var result = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 2).Cells;
        Assert.AreEqual(before, Hash(path)); return result;
    }
    [TestMethod]
    public void ExplicitLowId43DoesNotPoisonUnrelatedCustomFormat()
    {
        var cells = FormatCells(MixedStyles(Def(43, "0.00") + Def(164, "0.00"), "<xf numFmtId=\"43\"/><xf numFmtId=\"164\"/>"));
        Assert.AreEqual("12.5", cells[0].Interpretation!.DisplayValue); Assert.AreEqual("Unsupported low-ID definition", cells[0].Interpretation!.Classification);
        Assert.AreEqual(43, cells[0].Format.NumberFormatId); Assert.AreEqual("0.00", cells[0].Format.NumberFormat); Assert.IsNull(cells[0].Format.EffectiveNumberFormatId);
        Assert.AreEqual("12.50", cells[1].Interpretation!.DisplayValue); Assert.AreEqual("Supported subset", cells[1].Interpretation!.Confidence);
    }
    [TestMethod, DataRow(2, "0.00", true), DataRow(4, "#,##0.00", true), DataRow(2, "0.000", false), DataRow(14, "0.00", false)]
    public void FixedBuiltInAgreementIsExplicitAndCollisionIsLocalized(int id, string code, bool supported)
    {
        var cells = FormatCells(MixedStyles(Def(id, code), $"<xf numFmtId=\"{id}\"/><xf numFmtId=\"2\"/>"));
        Assert.AreEqual(supported ? "Supported subset" : "Unsupported", cells[0].Interpretation!.Confidence);
        Assert.AreEqual(supported ? "12.50" : "12.5", cells[0].Interpretation!.DisplayValue);
        // A conflicting definition for ID 2 also affects the second cell using that same ID.
        if (id != 2 || supported) Assert.AreEqual("12.50", cells[1].Interpretation!.DisplayValue);
        else Assert.AreEqual("12.5", cells[1].Interpretation!.DisplayValue);
    }
    [TestMethod, DataRow("0.00"), DataRow("0.000")]
    public void DuplicateDefinitionsRejectOnlyReferencingStyles(string second)
    {
        var cells = FormatCells(MixedStyles(Def(164, "0.00") + Def(164, second), "<xf numFmtId=\"164\"/><xf numFmtId=\"2\"/>"));
        Assert.AreEqual("Ambiguous format", cells[0].Interpretation!.Classification); Assert.AreEqual("12.5", cells[0].Interpretation!.DisplayValue);
        Assert.AreEqual("12.50", cells[1].Interpretation!.DisplayValue);
    }
    [TestMethod, DataRow(""), DataRow(" applyNumberFormat=\"0\""), DataRow(" applyNumberFormat=\"1\"")]
    public void MatchingCellAndBaseNumberFormatCanBeResolvedWithoutGuessing(string flag)
    {
        var cells = FormatCells(MixedStyles(Def(164, "0.00"), $"<xf numFmtId=\"164\" xfId=\"1\"{flag}/><xf numFmtId=\"2\"/>", "<xf numFmtId=\"0\"/><xf numFmtId=\"164\"/>"));
        Assert.AreEqual("12.50", cells[0].Interpretation!.DisplayValue); Assert.AreEqual(164, cells[0].Format.EffectiveNumberFormatId);
        StringAssert.Contains(cells[0].Format.FormatResolution!, flag.Contains("1") ? "Explicit" : "agree");
    }
    [TestMethod, DataRow(""), DataRow(" applyNumberFormat=\"0\"")]
    public void ConflictingInheritanceStaysRawButUnrelatedStyleWorks(string flag)
    {
        var cells = FormatCells(MixedStyles(Def(164, "0.00"), $"<xf numFmtId=\"164\" xfId=\"1\"{flag}/><xf numFmtId=\"2\"/>", "<xf numFmtId=\"0\"/><xf numFmtId=\"0\"/>"));
        Assert.AreEqual("Unresolved inheritance", cells[0].Interpretation!.Classification); Assert.AreEqual("12.5", cells[0].Interpretation!.DisplayValue);
        Assert.AreEqual("12.50", cells[1].Interpretation!.DisplayValue);
    }
    [TestMethod]
    public void ExplicitNumberFormatOverrideRequiresValidParentReference()
    {
        var supported = FormatCells(MixedStyles(Def(164, "0.00"), "<xf numFmtId=\"164\" xfId=\"1\" applyNumberFormat=\"1\"/><xf numFmtId=\"2\"/>", "<xf numFmtId=\"0\"/><xf numFmtId=\"0\"/>"));
        Assert.AreEqual("12.50", supported[0].Interpretation!.DisplayValue);
        var missing = FormatCells(MixedStyles(Def(164, "0.00"), "<xf numFmtId=\"164\" xfId=\"9\" applyNumberFormat=\"1\"/><xf numFmtId=\"2\"/>"));
        Assert.AreEqual("Unresolved inheritance", missing[0].Interpretation!.Classification); Assert.AreEqual("12.50", missing[1].Interpretation!.DisplayValue);
    }
    [TestMethod]
    public void MissingInvalidAndOverlongDefinitionsAreLocalWhenTheirIdIsKnown()
    {
        foreach (var definitions in new[] { "", Def(164, ""), Def(164, new string('0', 257)) })
        {
            var c = FormatCells(MixedStyles(definitions, "<xf numFmtId=\"164\"/><xf numFmtId=\"2\"/>"));
            Assert.AreEqual("Unsupported", c[0].Interpretation!.Confidence); Assert.AreEqual("12.50", c[1].Interpretation!.DisplayValue);
        }
    }
    [TestMethod, DataRow("<numFmt numFmtId=\"invalid\" formatCode=\"0.00\"/>"), DataRow("<numFmt numFmtId=\"-1\" formatCode=\"0.00\"/>")]
    public void UnassignableMalformedDefinitionsRetainGlobalFallback(string definition)
    {
        var c = FormatCells(MixedStyles(definition, "<xf numFmtId=\"164\"/><xf numFmtId=\"2\"/>"));
        Assert.IsTrue(c.All(c => c.Interpretation!.Classification == "Malformed or over-budget stylesheet"));
    }
    [TestMethod]
    public void FormatIsolationPreservesLocaleAndOverPrecisionRejections()
    {
        var c = FormatCells(MixedStyles(Def(164, "[$-409]0.00") + Def(165, "0.00"), "<xf numFmtId=\"164\"/><xf numFmtId=\"165\"/>"), "1.234567890123456");
        Assert.IsTrue(c.All(c => c.Interpretation!.Confidence == "Unsupported"));
        Assert.AreEqual("Over-precision value", c[1].Interpretation!.Classification); Assert.AreEqual("1.234567890123456", c[1].Value);
    }
    [TestMethod]
    public void BoundedAnalysisCaptureMatchesOrdinaryWindowsAndIncludesHiddenRows()
    {
        var path = Package(cells: "<row r=\"1\"><c r=\"B1\" t=\"inlineStr\"><is><t>Vendor</t></is></c></row><row r=\"2\" hidden=\"1\"><c r=\"B2\"><v>12</v></c><c r=\"D2\"><f>B2+1</f><v>13</v></c></row><row r=\"100\" hidden=\"1\"/>");
        var hash = Hash(path); using var session = new WorkbookInspection().Open(path); var info = session.Info; var sheet = info.Worksheets[0];
        var r = ((IInspectionAnalysisSession)session).CaptureInspectionRegion(new(info.SessionId, sheet.Id, 0, info.SourceSha256, 1, 3, [4, 2]));
        CollectionAssert.AreEqual(new[] { 2, 4 }, r.Columns.ToArray()); CollectionAssert.AreEqual(new[] { 2 }, r.HiddenRows.ToArray());
        Assert.HasCount(6, r.Cells); Assert.AreEqual(2, r.Scan.LastRow);
        Assert.AreEqual(100,r.Scan.LastRowElement);
        foreach (var cell in r.Cells) Assert.AreEqual(session.ReadViewport(sheet.Id, cell.Row, cell.Column, 1, 1).Cells[0], cell);
        Assert.AreEqual(hash, Hash(path)); Assert.IsFalse(info.EditingAllowed); Assert.AreEqual(0, session.Info.Revision);
        Assert.ThrowsExactly<WorkbookException>(() => session.CreateWorkingCopy(sheet.Id, 0));
    }
    [TestMethod]
    public void CaptureRejectsStaleIdentityIncompleteRequestsCancellationAndDisposedSession()
    {
        var path = Package(cells: Rows(200)); var session = new WorkbookInspection().Open(path); var i = session.Info;
        var req = new InspectionRegionRequest(i.SessionId, i.Worksheets[0].Id, 0, i.SourceSha256, 1, 200, [1, 2]);
        var reader = (IInspectionAnalysisSession)session;
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { SessionId = Guid.NewGuid() }));
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { SourceSha256 = new string('0', 64) }));
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { ExpectedRevision = 1 }));
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { EndRow = 2001 }));
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { Columns = [1, 1] }));
        Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(req with { EndRow = 2000, Columns = Enumerable.Range(1, 32).ToArray() }));
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => reader.CaptureInspectionRegion(req, cancelled.Token));
        session.Dispose(); Assert.ThrowsExactly<ObjectDisposedException>(() => reader.CaptureInspectionRegion(req));
    }
    [TestMethod]
    public void CaptureResolvesSharedTextAndFormulasExactlyLikeNormalViews()
    {
        var path = InterpretedPackage("<row r=\"1\"><c r=\"A1\" t=\"s\"><v>0</v></c><c r=\"B1\"><f t=\"shared\" si=\"0\" ref=\"B1:C1\">A1+1</f><v>3</v></c><c r=\"C1\"><f t=\"shared\" si=\"0\"/><v>4</v></c></row>", "0.00");
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Update))
        {
            using var writer = new StreamWriter(zip.CreateEntry("xl/sharedStrings.xml").Open()); writer.Write($"<sst xmlns=\"{Ns}\"><si><t>Artificial identity</t></si></sst>");
            // Package fixture needs an actual shared-string relationship as well as the part.
            writer.Dispose();
            var entry = zip.GetEntry("xl/_rels/workbook.xml.rels")!; string xml; using (var r = new StreamReader(entry.Open())) xml = r.ReadToEnd(); entry.Delete();
            using var rel = new StreamWriter(zip.CreateEntry("xl/_rels/workbook.xml.rels").Open()); rel.Write(xml.Replace("</Relationships>", $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/></Relationships>"));
        }
        using var session = new WorkbookInspection().Open(path); var i = session.Info;
        var captured = ((IInspectionAnalysisSession)session).CaptureInspectionRegion(new(i.SessionId, i.Worksheets[0].Id, 0, i.SourceSha256, 1, 2, [1, 2, 3]));
        Assert.AreEqual("Artificial identity", captured.Cells[0].Value); Assert.AreEqual("B1+1", captured.Cells[2].Formula);
        foreach (var c in captured.Cells) Assert.AreEqual(session.ReadViewport(i.Worksheets[0].Id, c.Row, c.Column, 1, 1).Cells[0], c);
    }
    [TestMethod]
    public void CapturePayloadBudgetAndDuringScanCancellationLeaveSessionReadOnlyAndUsable()
    {
        var text = new string('X', 1024);
        var rows = string.Concat(Enumerable.Range(1, 1000).Select(r => $"<row r=\"{r}\">" + string.Concat(Enumerable.Range(1, 4).Select(c => $"<c r=\"{(char)('A'+c-1)}{r}\" t=\"s\"><v>0</v></c>")) + "</row>"));
        var path = Package(cells: rows, extra: new() { ["xl/sharedStrings.xml"] = $"<sst xmlns=\"{Ns}\"><si><t>{text}</t></si></sst>" }, extraRels: $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/>");
        using (var session = new WorkbookInspection().Open(path))
        {
            var i = session.Info; var reader = (IInspectionAnalysisSession)session;
            Assert.ThrowsExactly<WorkbookException>(() => reader.CaptureInspectionRegion(new(i.SessionId, i.Worksheets[0].Id, 0, i.SourceSha256, 1, 1000, [1,2,3,4])));
            Assert.AreEqual(text, session.ReadViewport(i.Worksheets[0].Id,1,1,1,1).Cells[0].Value); Assert.IsFalse(session.Info.IsDirty);
        }
        var large = Package(cells: Rows(125000)); using var largeSession = new WorkbookInspection().Open(large); var info = largeSession.Info;
        using var cancellation = new CancellationTokenSource(); cancellation.CancelAfter(1);
        Assert.ThrowsExactly<OperationCanceledException>(() => ((IInspectionAnalysisSession)largeSession).CaptureInspectionRegion(new(info.SessionId,info.Worksheets[0].Id,0,info.SourceSha256,1,2000,[1,2]), cancellation.Token));
        Assert.AreEqual(info.SourceSha256, Hash(large)); Assert.IsFalse(largeSession.Info.EditingAllowed);
    }
    [TestMethod]
    public void LowFormatIdsAreNotSchemaMalformedAndUnknownExtensionsStillFailClosed()
    {
        var element = new DocumentFormat.OpenXml.Spreadsheet.NumberingFormat { NumberFormatId = 43U, FormatCode = "0.00" };
        Assert.IsEmpty(new DocumentFormat.OpenXml.Validation.OpenXmlValidator().Validate(element).ToArray());
        var styles = MixedStyles(Def(164,"0.00"),"<xf numFmtId=\"164\"/><xf numFmtId=\"2\"/>")
            .Replace("</styleSheet>","<extLst><ext uri=\"unqualified\"><foreign xmlns=\"urn:unknown\"/></ext></extLst></styleSheet>");
        Assert.IsTrue(FormatCells(styles).All(c=>c.Interpretation!.Classification=="Malformed or over-budget stylesheet"));
    }
    [TestMethod]
    public void DifferentialFormatsDoNotOverrideOrdinaryFormatDefinitions()
    {
        var styles = MixedStyles(Def(164,"0.00"),"<xf numFmtId=\"164\"/><xf numFmtId=\"2\"/>")
            .Replace("</styleSheet>","<dxfs><dxf><numFmt numFmtId=\"164\" formatCode=\"0.0000\"/></dxf></dxfs></styleSheet>");
        Assert.IsTrue(FormatCells(styles).All(c=>c.Interpretation!.DisplayValue=="12.50"));
    }
    [TestMethod]
    public void NonDefaultNormalBaseDoesNotSilentlyBecomeGeneral()
    {
        var c=FormatCells(MixedStyles("","<xf numFmtId=\"0\" xfId=\"0\"/><xf numFmtId=\"2\"/>","<xf numFmtId=\"14\"/>"));
        Assert.AreEqual("Unresolved inheritance",c[0].Interpretation!.Classification); Assert.AreEqual("12.50",c[1].Interpretation!.DisplayValue);
    }
    [TestMethod]
    public void RowDefaultStyleCannotSilentlyBecomeCellDefaultGeneral()
    {
        var style=MixedStyles(Def(164,"0.00"),"<xf numFmtId=\"0\"/><xf numFmtId=\"164\"/>");
        using var session=new WorkbookInspection().Open(InterpretedPackage("<row r=\"1\" s=\"1\" customFormat=\"1\"><c r=\"A1\"><v>12.5</v></c><c r=\"B1\" s=\"1\"><v>12.5</v></c></row>",stylesheet:style));
        var cells=session.ReadViewport(session.Info.Worksheets[0].Id,1,1,1,2).Cells;
        Assert.AreEqual("Unresolved inheritance",cells[0].Interpretation!.Classification);Assert.AreEqual("12.5",cells[0].Interpretation!.DisplayValue);
        Assert.AreEqual("12.50",cells[1].Interpretation!.DisplayValue);
    }
}
