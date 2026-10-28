using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks.Tests;

[TestClass]
public sealed class WorkbookEditingTests
{
    private static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name + "-synthetic.xlsx");
    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    private static CellInfo Cell(IWorkbookSession s, Guid sheet, int row, int column) => s.ReadViewport(sheet, row, column, 1, 1).Cells[0];

    [TestMethod]
    [DataRow("reimbursement", "Expense Draft")] [DataRow("laboratory", "Lab Runs")]
    public void TypedEditsPreserveSourceAndOtherWorkbookContext(string file, string sheetName)
    {
        var path = Fixture(file); var hash = Hash(path); using var session = new WorkbookEngine().Open(path);
        var structuralBefore = PackageGuard.Inspect(File.ReadAllBytes(path), new WorkbookLimits(), CancellationToken.None).StructuralFacts;
        var sheet = session.Info.Worksheets.Single(s => s.Name == sheetName).Id;
        var other = session.Info.Worksheets.Single(s => s.Name == "Overview").Id;
        var crossSheet = Cell(session, other, 2, 2).Formula; var originalFormat = Cell(session, sheet, 5, 2).Format;
        session.Edit(new(sheet, 5, 1, "text", "=literal text", 0));
        Assert.AreEqual("=literal text", Cell(session, sheet, 5, 1).Value); Assert.IsNull(Cell(session, sheet, 5, 1).Formula);
        session.Edit(new(sheet, 5, 2, "number", "12.5", 1));
        Assert.AreEqual("12.5", Cell(session, sheet, 5, 2).Value); Assert.AreEqual(originalFormat, Cell(session, sheet, 5, 2).Format);
        session.Edit(new(sheet, 5, 4, "date", "2021-05-06", 2));
        Assert.AreEqual("DateTime", Cell(session, sheet, 5, 4).Kind);
        session.Edit(new(sheet, 5, 3, "formula", "=B5*'Lookup'!$B$2+1", 3));
        Assert.AreEqual("B5*'Lookup'!$B$2+1", Cell(session, sheet, 5, 3).Formula);
        Assert.AreEqual(crossSheet, Cell(session, other, 2, 2).Formula);
        var stateBytes = (byte[])session.GetType().GetField("currentBytes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        var structuralAfter = PackageGuard.Inspect(stateBytes, new WorkbookLimits(), CancellationToken.None).StructuralFacts;
        CollectionAssert.AreEqual(structuralBefore.ToArray(), structuralAfter.ToArray(), "Defined names, merges, table extents and metadata must remain unchanged.");
        Assert.AreEqual(4, session.Info.Revision); Assert.IsTrue(session.Info.IsDirty); Assert.AreEqual(hash, session.Info.SourceSha256); Assert.AreEqual(hash, Hash(path));
        using var snapshot = session.CreateSnapshot(); Assert.AreEqual("12.5", Cell(snapshot, sheet, 5, 2).Value);
        Assert.AreEqual(hash, Hash(path));
    }

    [TestMethod]
    public void MultipleWorkingCopiesAreIsolatedAndSelfReferencesKeepOriginalSheetIdentity()
    {
        var path = Fixture("reimbursement"); var hash = Hash(path); using var session = new WorkbookEngine().Open(path);
        var sheet = session.Info.Worksheets[0].Id; var unrelated = session.Info.Worksheets[1].Id;
        var self = Cell(session, sheet, 5, 5).Formula;
        using var a = session.CreateWorkingCopy(sheet, 0); using var b = session.CreateWorkingCopy(sheet, 0);
        var baseField = a.GetType().GetField("copyBaseBytes", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var baseHash = Convert.ToHexString(SHA256.HashData((byte[])baseField.GetValue(a)!));
        Assert.AreEqual(self, Cell(a, sheet, 5, 5).Formula); Assert.AreEqual(self, Cell(b, sheet, 5, 5).Formula);
        Assert.AreEqual(0, a.WorkingCopy!.BaseRevision); Assert.AreEqual(sheet, a.WorkingCopy.SourceWorksheetId);
        a.Edit(new(sheet, 5, 1, "text", "First copy", 0)); b.Edit(new(sheet, 5, 1, "text", "Second copy", 0));
        Assert.AreEqual("First copy", Cell(a, sheet, 5, 1).Value); Assert.AreEqual("Second copy", Cell(b, sheet, 5, 1).Value);
        Assert.AreEqual("Sample-1", Cell(session, sheet, 5, 1).Value); Assert.IsFalse(session.Info.IsDirty);
        Assert.AreEqual(self, Cell(a, sheet, 5, 5).Formula); Assert.AreEqual(self, Cell(b, sheet, 5, 5).Formula);
        Assert.AreEqual(1, a.WorkingCopy!.Changes.Count); Assert.AreEqual("First copy", a.WorkingCopy.Changes[0].Value);
        a.Edit(new(sheet, 5, 1, "text", "First copy revised", 1));
        Assert.AreEqual(2, a.WorkingCopy!.Revision); Assert.AreEqual(2, a.WorkingCopy.Changes.Count);
        Assert.AreEqual("Second copy", Cell(b, sheet, 5, 1).Value); Assert.AreEqual("Sample-1", Cell(session, sheet, 5, 1).Value);
        Assert.AreEqual(baseHash, Convert.ToHexString(SHA256.HashData((byte[])baseField.GetValue(a)!)), "The working-copy base must remain immutable.");
        Assert.AreEqual("READ_ONLY_WORKSHEET", Assert.ThrowsExactly<WorkbookException>(() => a.Edit(new(unrelated, 5, 1, "text", "Denied", 2))).Code);
        Assert.AreEqual(2, a.Info.Revision); Assert.AreEqual(hash, Hash(path));
    }

    [TestMethod]
    public void CopyRecordsTheMainRevisionAtCreationAndIsIndependentOfLaterMainEdits()
    {
        var path = Fixture("laboratory"); var hash = Hash(path); using var session = new WorkbookEngine().Open(path); var sheet = session.Info.Worksheets[0].Id;
        session.Edit(new(sheet, 5, 1, "text", "Main before copy", 0));
        using var copy = session.CreateWorkingCopy(sheet, 1);
        Assert.AreEqual(1, copy.WorkingCopy!.BaseRevision); Assert.AreEqual("Main before copy", Cell(copy, sheet, 5, 1).Value);
        session.Edit(new(sheet, 5, 1, "text", "Main after copy", 1));
        Assert.AreEqual("Main before copy", Cell(copy, sheet, 5, 1).Value); Assert.AreEqual("Main after copy", Cell(session, sheet, 5, 1).Value);
        Assert.AreEqual(hash, Hash(path));
    }

    [TestMethod]
    public void SnapshotOfWorkingCopyRetainsItsWorksheetRestrictionAndDirtyHistory()
    {
        using var session = new WorkbookEngine().Open(Fixture("reimbursement"));
        var selected = session.Info.Worksheets[0].Id; var unrelated = session.Info.Worksheets[1].Id;
        using var copy = session.CreateWorkingCopy(selected, 0);
        copy.Edit(new(selected, 5, 1, "text", "First copy", 0));
        using var snapshot = copy.CreateSnapshot();
        Assert.AreEqual(selected, snapshot.WorkingCopy!.SourceWorksheetId); Assert.IsTrue(snapshot.Info.IsDirty); Assert.AreEqual(1, snapshot.Info.Revision); Assert.AreEqual(1, snapshot.WorkingCopy.Changes.Count);
        Assert.AreEqual("READ_ONLY_WORKSHEET", Assert.ThrowsExactly<WorkbookException>(() => snapshot.Edit(new(unrelated, 5, 1, "text", "denied", 1))).Code);
        snapshot.Edit(new(selected, 5, 1, "text", "Snapshot only", 1));
        Assert.AreEqual("First copy", Cell(copy, selected, 5, 1).Value); Assert.AreEqual("Sample-1", Cell(session, selected, 5, 1).Value);
    }

    [TestMethod]
    public void DateEditingRespectsThe1904WorkbookDateSystem()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TsekSheet-date-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "date1904.xlsx");
        try
        {
            File.Copy(Fixture("reimbursement"), path);
            using (var document = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path, true))
                document.WorkbookPart!.Workbook!.WorkbookProperties!.Date1904 = true;
            var hash = Hash(path); using var session = new WorkbookEngine().Open(path); var sheet = session.Info.Worksheets[0].Id;
            session.Edit(new(sheet, 5, 4, "date", "2021-05-06", 0));
            Assert.AreEqual("DateTime", Cell(session, sheet, 5, 4).Kind);
            Assert.IsTrue(DateTime.TryParse(Cell(session, sheet, 5, 4).Value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date));
            Assert.AreEqual(new DateTime(2021, 5, 6), date); Assert.AreEqual(hash, Hash(path));
        }
        finally { File.Delete(path); Directory.Delete(folder); }
    }

    [TestMethod]
    public void InvalidOrStaleChangesNeverAdoptCandidates()
    {
        using var session = new WorkbookEngine().Open(Fixture("reimbursement")); var id = session.Info.Worksheets[0].Id;
        foreach (var (edit, code) in new (CellEdit, string)[] {
            (new(id,5,3,"text","lost formula",0),"FORMULA_PROTECTED"),
            (new(id,5,1,"text","stale",99),"STALE_REVISION"),
            (new(Guid.NewGuid(),5,1,"text","wrong",0),"UNKNOWN_WORKSHEET"),
            (new(id,0,1,"text","bad",0),"INVALID_CELL"),
            (new(id,1,1,"text","merged",0),"UNSUPPORTED_CELL"),
            (new(id,100,1,"text","blank",0),"UNSUPPORTED_CELL"),
            (new(id,5,2,"number","NaN",0),"INVALID_VALUE"),
            (new(id,5,2,"date","2020-01-01",0),"UNSUPPORTED_CELL"),
            (new(id,5,3,"formula","=WEBSERVICE(A5)",0),"UNSUPPORTED_FORMULA"),
            (new(id,5,3,"formula","='Missing Sheet'!A1",0),"UNSUPPORTED_FORMULA") })
            Assert.AreEqual(code, Assert.ThrowsExactly<WorkbookException>(() => session.Edit(edit)).Code);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        Assert.ThrowsExactly<OperationCanceledException>(() => session.Edit(new(id,5,1,"text","cancelled",0), cancelled.Token));
        Assert.AreEqual(0, session.Info.Revision); Assert.IsFalse(session.Info.IsDirty); Assert.AreEqual("Sample-1", Cell(session,id,5,1).Value);
    }

    [TestMethod]
    public void UnaffectedPartTamperingIsRejectedWithoutChangingSession()
    {
        var engine = new WorkbookEngine(); var path = Fixture("laboratory"); var hash = Hash(path);
        using var session = engine.Open(path); var id = session.Info.Worksheets[0].Id;
        engine.CandidateFaultForTesting = bytes =>
        {
            using var memory = new MemoryStream(); memory.Write(bytes); memory.Position = 0;
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("customXml/item.xml")!; entry.Delete();
                using var writer = new StreamWriter(zip.CreateEntry("customXml/item.xml").Open()); writer.Write("<tampered/>");
            }
            return memory.ToArray();
        };
        Assert.AreEqual("CANDIDATE_REJECTED", Assert.ThrowsExactly<WorkbookException>(() => session.Edit(new(id,5,1,"text","not adopted",0))).Code);
        Assert.AreEqual(0, session.Info.Revision); Assert.IsFalse(session.Info.IsDirty); Assert.AreEqual("Sample-1", Cell(session,id,5,1).Value); Assert.AreEqual(hash, Hash(path));
        engine.CandidateFaultForTesting = null; session.Edit(new(id,5,1,"text","accepted",0)); Assert.AreEqual("accepted", Cell(session,id,5,1).Value);
    }

    [TestMethod]
    public void CancellationAfterCandidateCreationNeverAdoptsTheEdit()
    {
        var engine = new WorkbookEngine(); var path = Fixture("reimbursement"); var hash = Hash(path);
        using var session = engine.Open(path); var id = session.Info.Worksheets[0].Id; using var cancellation = new CancellationTokenSource();
        engine.CandidateFaultForTesting = bytes => { cancellation.Cancel(); return bytes; };
        Assert.ThrowsExactly<OperationCanceledException>(() => session.Edit(new(id,5,1,"text","cancelled after candidate",0), cancellation.Token));
        Assert.AreEqual(0, session.Info.Revision); Assert.IsFalse(session.Info.IsDirty); Assert.AreEqual("Sample-1", Cell(session,id,5,1).Value); Assert.AreEqual(hash, Hash(path));
    }

    [TestMethod]
    public void TamperingWithAnotherCellInTheEditedSheetIsRejected()
    {
        var engine = new WorkbookEngine(); using var session = engine.Open(Fixture("reimbursement")); var id = session.Info.Worksheets[0].Id;
        engine.CandidateFaultForTesting = bytes =>
        {
            using var memory = new MemoryStream(); memory.Write(bytes); memory.Position = 0;
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Update, true))
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; string xml;
                using (var reader = new StreamReader(entry.Open())) xml = reader.ReadToEnd();
                entry.Delete(); using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open()); writer.Write(xml.Replace("Sample-2", "tampered"));
            }
            return memory.ToArray();
        };
        Assert.AreEqual("CANDIDATE_REJECTED", Assert.ThrowsExactly<WorkbookException>(() => session.Edit(new(id,5,1,"text","denied",0))).Code);
        Assert.AreEqual(0, session.Info.Revision); Assert.AreEqual("Sample-1", Cell(session,id,5,1).Value); Assert.AreEqual("Sample-2", Cell(session,id,6,1).Value);
    }

    [TestMethod]
    public void WorkingCopyAndCandidateRespectLimitsAndDisposal()
    {
        var engine = new WorkbookEngine(new() { MaxSessions = 2 }); var session = engine.Open(Fixture("reimbursement")); var id = session.Info.Worksheets[0].Id;
        var copy = session.CreateWorkingCopy(id, 0);
        Assert.AreEqual("LIMIT_EXCEEDED", Assert.ThrowsExactly<WorkbookException>(() => session.CreateWorkingCopy(id, 0)).Code);
        Assert.AreEqual("LIMIT_EXCEEDED", Assert.ThrowsExactly<WorkbookException>(() => copy.Edit(new(id,5,1,"text","no capacity",0))).Code);
        Assert.AreEqual(0, copy.Info.Revision); Assert.AreEqual("Sample-1", Cell(copy,id,5,1).Value);
        copy.Dispose(); Assert.ThrowsExactly<ObjectDisposedException>(() => copy.Edit(new(id,5,1,"text","disposed",0)));
        session.Dispose(); using var reopened = engine.Open(Fixture("laboratory")); Assert.AreEqual(4, reopened.Info.Worksheets.Count);
    }
}
