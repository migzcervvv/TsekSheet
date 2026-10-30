using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TsekSheet.Workbooks.Tests;

public sealed partial class WorkbookInspectionTests
{
    [TestMethod]
    public void DisplayBoundsTrimOnlyTrailingFormattingAndRetainInternalBlanksAndFormulas()
    {
        var path = Package(cells: """
            <row r="1"><c r="A1" t="inlineStr"><is><t>Artificial heading</t></is></c></row>
            <row r="3"><c r="A3" s="0"/></row>
            <row r="8" hidden="1"><c r="C8"><f>SUM(C2:C7)</f></c></row>
            <row r="9999"><c r="XFD9999" s="0"/></row>
            <row r="10000"/>
            """, dimension: "A1:XFD1048576");
        var hash = Hash(path);
        using (var session = new WorkbookInspection().Open(path))
        {
            var id = session.Info.Worksheets[0].Id;
            var view = session.ReadViewport(id, 1, 1, 20, 6);
            Assert.AreEqual(8, view.Scan!.MeaningfulLastRow);
            Assert.AreEqual(3, view.Scan.MeaningfulLastColumn);
            Assert.AreEqual(9999, view.Scan.LastRow);
            Assert.AreEqual(10000, view.Scan.LastRowElement);
            Assert.AreEqual(16384, view.Scan.LastColumn);
            Assert.AreEqual(120, view.Cells.Count);
            Assert.IsTrue(view.Cells.Where(c => c.Row == 2).All(c => c.Kind == "Blank"));
            Assert.AreEqual("SUM(C2:C7)", view.Cells.Single(c => c.Row == 8 && c.Column == 3).Formula);
            Assert.AreEqual(16384, session.ReadViewport(id, 9999, 16384, 1, 1).Cells[0].Column);
        }
        Assert.AreEqual(hash, Hash(path));
    }

    [TestMethod]
    public void HiddenDistantValuesAndUncachedFormulaFollowersRemainWithinDisplayBounds()
    {
        using var session = new WorkbookInspection().Open(Package(cells: """
            <row r="1"><c r="A1"><v>0</v></c></row>
            <row r="900000" hidden="1"><c r="XFC900000" t="inlineStr"><is><t>Hidden distant evidence</t></is></c><c r="XFD900000"><f t="array" ref="XFD900000">1</f></c></row>
            """));
        var id = session.Info.Worksheets[0].Id;
        var scan = session.ReadViewport(id, 1, 1, 1, 1).Scan!;
        Assert.AreEqual(900000, scan.MeaningfulLastRow);
        Assert.AreEqual(16384, scan.MeaningfulLastColumn);
        Assert.AreEqual("Hidden distant evidence", session.ReadViewport(id, 900000, 16383, 1, 1).Cells[0].Value);
    }

    [TestMethod]
    public void EntirelyStyledEmptySheetHasZeroMeaningfulBounds()
    {
        using var session = new WorkbookInspection().Open(Package(cells: "<row r=\"2000\"><c r=\"ZZ2000\" s=\"0\"/></row>"));
        var scan = session.ReadViewport(session.Info.Worksheets[0].Id, 1, 1, 1, 1).Scan!;
        Assert.AreEqual(0, scan.MeaningfulLastRow);
        Assert.AreEqual(0, scan.MeaningfulLastColumn);
        Assert.AreEqual(2000, scan.LastRow);
    }

    [TestMethod]
    public void EmptySharedStringReferenceConservativelyExtendsBoundsWithoutRetainingTheStringTable()
    {
        using var session = new WorkbookInspection().Open(Package(
            cells: "<row r=\"9000\"><c r=\"ZZ9000\" t=\"s\"><v>0</v></c></row>",
            extra: new() { ["xl/sharedStrings.xml"] = $"<sst xmlns=\"{Ns}\"><si><t/></si></sst>" },
            extraRels: $"<Relationship Id=\"strings\" Type=\"{Office}sharedStrings\" Target=\"sharedStrings.xml\"/>"));
        var id = session.Info.Worksheets[0].Id;
        var scan = session.ReadViewport(id, 1, 1, 1, 1).Scan!;
        Assert.AreEqual(9000, scan.MeaningfulLastRow);
        Assert.AreEqual(702, scan.MeaningfulLastColumn);
        Assert.AreEqual("", session.ReadViewport(id, 9000, 702, 1, 1).Cells[0].Value);
    }
}
