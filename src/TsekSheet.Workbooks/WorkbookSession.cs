using System.Globalization;
using System.Security.Cryptography;
using ClosedXML.Excel;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed partial class WorkbookSession : IWorkbookSession
{
    private readonly WorkbookEngine engine;
    private XLWorkbook? workbook;
    private Stream? inputStream;
    private byte[] baseline;
    private byte[] currentBytes;
    private byte[]? copyBaseBytes;
    private PackageFacts facts;
    private Action release;
    private long estimate;
    private WorkbookInfo info;
    private readonly object gate = new();
    private Dictionary<Guid, Dictionary<long, CellInfo>> cellsBySheet = [];
    private Guid? workingSheet;
    private int baseRevision;
    private readonly List<CellChange> changes = [];
    private bool disposed;
    private string? originalPath; // Native-only binding; never part of a frontend DTO.
    internal void BindOriginalPath(string path) => originalPath = path;

    internal WorkbookSession(WorkbookEngine engine, XLWorkbook workbook, Stream inputStream, byte[] baseline, PackageFacts facts,
        string displayName, string hash, Guid? parent, IReadOnlyList<WorksheetInfo>? identities, long estimate, Action release)
    {
        this.engine = engine; this.workbook = workbook; this.inputStream = inputStream; this.baseline = baseline; currentBytes = baseline; this.facts = facts; this.estimate = estimate; this.release = release;
        var sheets = workbook.Worksheets.Select(s => new WorksheetInfo(identities?.Single(i => i.Name == s.Name).Id ?? Guid.NewGuid(), s.Name, s.Position,
            s.Visibility.ToString(), s.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0, s.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0)).ToArray();
        info = new(Guid.NewGuid(), parent, displayName, hash, 0, false, false, Limit(facts.Creator), Limit(facts.Title), Array.AsReadOnly(sheets), facts.Findings);
        foreach (var s in sheets)
            cellsBySheet[s.Id] = workbook.Worksheet(s.Name).CellsUsed(XLCellsUsedOptions.Contents).ToDictionary(c => Key(c.Address.RowNumber, c.Address.ColumnNumber), Project);
        info = info with { EditingAllowed = !facts.Findings.Any(f => f.Code is "ADVANCED_CONTENT" or "UNKNOWN_PARTS") && !cellsBySheet.Values.Any(s => s.Values.Any(c => c.Truncated)) };
    }

    public WorkbookInfo Info { get { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return info; } } }

    public CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (rows < 1 || columns < 1 || rows > 20 || columns > 10 || startRow < 1 || startColumn < 1 || startRow > 1048576 - rows + 1 || startColumn > 16384 - columns + 1)
                throw PackageGuard.Reject("INVALID_VIEWPORT", "Request 1–20 rows and 1–10 columns within Excel worksheet bounds.");
            var sheetInfo = info.Worksheets.SingleOrDefault(s => s.Id == worksheetId) ?? throw PackageGuard.Reject("UNKNOWN_WORKSHEET", "Worksheet does not belong to this session.");
            var stored = cellsBySheet[sheetInfo.Id]; var cells = new List<CellInfo>();
            for (var r = startRow; r < startRow + rows; r++)
            for (var c = startColumn; c < startColumn + columns; c++)
            {
                // Blank reads must not create new ClosedXML cells or grow the session indefinitely.
                cells.Add(stored.GetValueOrDefault(Key(r, c)) ?? new(r, c, "", "Blank", null, false, new("", 0, false, false, "")));
            }
            return new(info.SessionId, worksheetId, startRow, startColumn, rows, columns, cells.AsReadOnly());
        }
    }

    public IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            cancellationToken.ThrowIfCancellationRequested();
            engine.CheckSnapshotCapacity();
            if (facts.Findings.Any(f => f.Code is "ADVANCED_CONTENT" or "UNKNOWN_PARTS")) throw PackageGuard.Reject("SNAPSHOT_UNSUPPORTED", "This workbook can be inspected, but its advanced content is outside the snapshot compatibility scope.");
            if (cellsBySheet.Values.Any(s => s.Values.Any(c => c.Truncated))) throw PackageGuard.Reject("SNAPSHOT_UNSUPPORTED", "Snapshot validation does not support truncated cell content.");
            using var preparation = engine.ReserveSnapshotWork(estimate);
            WorkbookSession? snapshot = null;
            try
            {
                // Reopen an independent copy of the already serialized current package.
                // No high-level round trip can introduce unrequested package changes.
                var serialized = currentBytes.ToArray();
                cancellationToken.ThrowIfCancellationRequested();
                snapshot = engine.Load(serialized, info.DisplayName, info.SourceSha256, info.SessionId, info.Worksheets, cancellationToken);
                // Compare the bounded inspection contract. This is not a full Excel preservation proof.
                foreach (var s in info.Worksheets)
                {
                    if (snapshot.Info.Worksheets.Single(i => i.Id == s.Id) != s) throw PackageGuard.Reject("SNAPSHOT_VALIDATION_FAILED", "Snapshot changed worksheet metadata.");
                    if (cellsBySheet[s.Id].Count != snapshot.cellsBySheet[s.Id].Count) throw PackageGuard.Reject("SNAPSHOT_VALIDATION_FAILED", "Snapshot changed stored cell content.");
                    foreach (var cell in cellsBySheet[s.Id].Values)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var after = snapshot.ReadViewport(s.Id, cell.Row, cell.Column, 1, 1).Cells[0];
                        if (cell != after) throw PackageGuard.Reject("SNAPSHOT_VALIDATION_FAILED", "Snapshot changed a value, formula or basic format.");
                    }
                }
                if (facts.Creator != snapshot.facts.Creator || facts.Title != snapshot.facts.Title || facts.OpaqueHashes.Count != snapshot.facts.OpaqueHashes.Count
                    || facts.OpaqueHashes.Any(p => !snapshot.facts.OpaqueHashes.TryGetValue(p.Key, out var hash) || hash != p.Value)
                    || !facts.StructuralFacts.SequenceEqual(snapshot.facts.StructuralFacts))
                    throw PackageGuard.Reject("SNAPSHOT_VALIDATION_FAILED", "Snapshot changed inspected metadata or custom XML.");
                snapshot.baseline = baseline.ToArray();
                snapshot.info = snapshot.info with { IsDirty = info.IsDirty };
                if (workingSheet != null)
                {
                    snapshot.workingSheet = workingSheet; snapshot.baseRevision = baseRevision;
                    snapshot.copyBaseBytes = copyBaseBytes?.ToArray(); snapshot.changes.AddRange(changes);
                    snapshot.info = snapshot.info with { Revision = info.Revision };
                }
                return snapshot;
            }
            catch (WorkbookException) { snapshot?.Dispose(); throw; }
            catch (OperationCanceledException) { snapshot?.Dispose(); throw; }
            catch { snapshot?.Dispose(); throw PackageGuard.Reject("SNAPSHOT_FAILED", "Independent snapshot could not be created. The source session remains open."); }
        }
    }

    public WorkingCopyInfo? WorkingCopy
    {
        get { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return workingSheet is Guid sheet ? new(info.SessionId, sheet, baseRevision, info.Revision, info.IsDirty, Array.AsReadOnly(changes.ToArray())) : null; } }
    }

    public IWorkbookSession CreateWorkingCopy(Guid worksheetId, int expectedRevision, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested();
            if (info.Revision != expectedRevision) throw PackageGuard.Reject("STALE_REVISION", "The workbook changed. Refresh before creating a working copy.");
            if (workingSheet != null) throw PackageGuard.Reject("UNSUPPORTED_OPERATION", "Create working copies from the original session.");
            if (!info.Worksheets.Any(s => s.Id == worksheetId)) throw PackageGuard.Reject("UNKNOWN_WORKSHEET", "Worksheet does not belong to this session.");
            EnsureEditable(); engine.CheckSnapshotCapacity();
            using var preparation = engine.ReserveSnapshotWork(estimate);
            var clone = engine.Load(currentBytes.ToArray(), info.DisplayName, info.SourceSha256, info.SessionId, info.Worksheets, cancellationToken);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                // The copy uses the complete serialized workbook: names, formulas and explicit
                // self-references retain their original worksheet identity and context.
                clone.baseline = baseline.ToArray(); clone.copyBaseBytes = clone.currentBytes; clone.workingSheet = worksheetId; clone.baseRevision = info.Revision;
                return clone;
            }
            catch { clone.Dispose(); throw; }
        }
    }

    public WorkbookInfo Edit(CellEdit edit, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested();
            if (edit.ExpectedRevision != info.Revision) throw PackageGuard.Reject("STALE_REVISION", "The workbook changed. Refresh before editing.");
            if (info.Revision == int.MaxValue) throw PackageGuard.Reject("LIMIT_EXCEEDED", "The session revision limit has been reached.");
            if (edit.Row is < 1 or > 1048576 || edit.Column is < 1 or > 16384) throw PackageGuard.Reject("INVALID_CELL", "Cell coordinates are outside the worksheet.");
            var sheet = info.Worksheets.SingleOrDefault(s => s.Id == edit.WorksheetId) ?? throw PackageGuard.Reject("UNKNOWN_WORKSHEET", "Worksheet does not belong to this session.");
            if (workingSheet != null && workingSheet != edit.WorksheetId) throw PackageGuard.Reject("READ_ONLY_WORKSHEET", "Only the selected worksheet can be edited in this working copy.");
            if (changes.Count >= 1000) throw PackageGuard.Reject("LIMIT_EXCEEDED", "This working copy has reached its change limit.");
            if (workingSheet != null) changes.EnsureCapacity(changes.Count + 1);
            EnsureEditable();
            if (!cellsBySheet[sheet.Id].TryGetValue(Key(edit.Row, edit.Column), out var original)) throw PackageGuard.Reject("UNSUPPORTED_CELL", "Only existing populated cells can be edited in this milestone.");
            if (workbook!.Worksheet(sheet.Name).MergedRanges.Any(r => edit.Row >= r.RangeAddress.FirstAddress.RowNumber && edit.Row <= r.RangeAddress.LastAddress.RowNumber && edit.Column >= r.RangeAddress.FirstAddress.ColumnNumber && edit.Column <= r.RangeAddress.LastAddress.ColumnNumber))
                throw PackageGuard.Reject("UNSUPPORTED_CELL", "Merged cells cannot be edited in this milestone.");
            var value = CellCandidate.Validate(edit, original, info.Worksheets);
            using var preparation = engine.ReserveSnapshotWork(estimate);
            WorkbookSession? candidate = null;
            try
            {
                var (bytes, part, address) = CellCandidate.Apply(currentBytes, sheet.Name, edit, value, cancellationToken);
                if (engine.CandidateFaultForTesting != null) bytes = engine.CandidateFaultForTesting(bytes);
                CellCandidate.ValidateUnchangedParts(currentBytes, bytes, part, address);
                candidate = engine.Load(bytes, info.DisplayName, info.SourceSha256, info.ParentSessionId, info.Worksheets, cancellationToken);
                var after = candidate.ReadViewport(sheet.Id, edit.Row, edit.Column, 1, 1).Cells[0];
                if (after.Format != original.Format || !Matches(edit.Kind, value, after) || !facts.StructuralFacts.SequenceEqual(candidate.facts.StructuralFacts))
                    throw PackageGuard.Reject("CANDIDATE_REJECTED", "Candidate did not preserve the expected cell, formatting or workbook structure.");
                cancellationToken.ThrowIfCancellationRequested();
                var change = workingSheet != null ? new CellChange(edit.WorksheetId, edit.Row, edit.Column, edit.Kind, value, info.Revision + 1) : null;
                var oldWorkbook = workbook; var oldStream = inputStream; var oldRelease = release;
                workbook = candidate.workbook; inputStream = candidate.inputStream; currentBytes = candidate.baseline; facts = candidate.facts;
                cellsBySheet = candidate.cellsBySheet; estimate = candidate.estimate; release = candidate.release;
                info = info with { Revision = checked(info.Revision + 1), IsDirty = true, Worksheets = candidate.info.Worksheets };
                if (change != null) changes.Add(change);
                candidate.disposed = true; candidate.workbook = null; candidate.inputStream = null; candidate.baseline = []; candidate.currentBytes = []; candidate.cellsBySheet = [];
                candidate = null;
                try { oldWorkbook?.Dispose(); } catch { /* Adopted candidate remains authoritative. */ }
                try { oldStream?.Dispose(); } finally { oldRelease(); }
                return info;
            }
            catch (WorkbookException) { candidate?.Dispose(); throw; }
            catch (OperationCanceledException) { candidate?.Dispose(); throw; }
            catch { candidate?.Dispose(); throw PackageGuard.Reject("CANDIDATE_REJECTED", "The edit could not be validated. The workbook was not changed."); }
        }
    }

    public SaveReceipt SaveAsNew(string destination, int expectedRevision, CancellationToken cancellationToken = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); cancellationToken.ThrowIfCancellationRequested();
            if (workingSheet != null || info.ParentSessionId != null) throw PackageGuard.Reject("WORKING_COPY_SAVE_UNSUPPORTED", "Only the main session can be exported. Copies cannot be saved or integrated in this milestone.");
            if (info.Revision != expectedRevision) throw PackageGuard.Reject("STALE_REVISION", "The workbook changed. Refresh before saving.");
            EnsureEditable();
            try
            {
                if (originalPath != null && Path.GetFullPath(destination).Equals(originalPath, StringComparison.OrdinalIgnoreCase))
                    throw PackageGuard.Reject("ORIGINAL_DESTINATION", "Choose a separate new filename. Save As cannot recreate or replace the original source path.");
                using var reservation = engine.ReserveSnapshotWork(estimate);
                var frozenHash = Convert.ToHexString(SHA256.HashData(currentBytes));
                var candidateBytes = currentBytes.ToArray();
                if (engine.CandidateFaultForTesting != null) candidateBytes = engine.CandidateFaultForTesting(candidateBytes);
                if (!Convert.ToHexString(SHA256.HashData(candidateBytes)).Equals(frozenHash, StringComparison.Ordinal)) throw PackageGuard.Reject("CANDIDATE_REJECTED", "The serialized workbook differs from the frozen session.");
                using (var candidate = engine.Load(candidateBytes, info.DisplayName, info.SourceSha256, info.ParentSessionId, info.Worksheets, cancellationToken))
                {
                    if (!facts.StructuralFacts.SequenceEqual(candidate.facts.StructuralFacts) || facts.Creator != candidate.facts.Creator || facts.Title != candidate.facts.Title
                        || !info.Worksheets.SequenceEqual(candidate.info.Worksheets) || facts.OpaqueHashes.Count != candidate.facts.OpaqueHashes.Count
                        || facts.OpaqueHashes.Any(p => !candidate.facts.OpaqueHashes.TryGetValue(p.Key, out var hash) || hash != p.Value))
                        throw PackageGuard.Reject("CANDIDATE_REJECTED", "The reopened candidate differs from the frozen workbook.");
                    foreach (var sheet in info.Worksheets)
                    {
                        if (cellsBySheet[sheet.Id].Count != candidate.cellsBySheet[sheet.Id].Count) throw PackageGuard.Reject("CANDIDATE_REJECTED", "The reopened candidate changed stored cells.");
                        foreach (var (key, cell) in cellsBySheet[sheet.Id])
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if (!candidate.cellsBySheet[sheet.Id].TryGetValue(key, out var actual) || actual != cell) throw PackageGuard.Reject("CANDIDATE_REJECTED", "The reopened candidate changed cell values, formulas or formatting.");
                        }
                    }
                }
                cancellationToken.ThrowIfCancellationRequested();
                var exported = info with { LastExportedRevision = info.Revision, LastExportedSha256 = frozenHash };
                // Allocate response/state before commit so response construction cannot turn
                // an already confirmed disk commit into an ordinary failed-save result.
                var receipt = new SaveReceipt("saved", info.SessionId, info.Revision, info.IsDirty, false, false, frozenHash, Path.GetFileName(destination));
                var lateCancellationReceipt = receipt with { CancellationAfterCommit = true };
                NewFileCommit.Commit(destination, candidateBytes, frozenHash, cancellationToken, engine.CommitFaultsForTesting);
                info = exported;
                return cancellationToken.IsCancellationRequested ? lateCancellationReceipt : receipt;
            }
            catch (OperationCanceledException) { throw; }
            catch (WorkbookException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw PackageGuard.Reject("SAVE_FAILED", "The new file could not be committed. Check permissions and locks; the session remains unchanged."); }
            catch { throw PackageGuard.Reject("SAVE_FAILED", "The new file could not be committed. The session remains unchanged."); }
        }
    }

    private void EnsureEditable()
    {
        if (facts.Findings.Any(f => f.Code is "ADVANCED_CONTENT" or "UNKNOWN_PARTS") || cellsBySheet.Values.Any(s => s.Values.Any(c => c.Truncated)))
            throw PackageGuard.Reject("EDIT_UNSUPPORTED", "This workbook has content outside the validated editing scope.");
    }

    private static bool Matches(string kind, string value, CellInfo cell) => kind switch
    {
        "text" => cell.Formula == null && cell.Kind == "Text" && cell.Value == value,
        "number" => cell.Formula == null && cell.Kind == "Number" && double.TryParse(cell.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && number == double.Parse(value, CultureInfo.InvariantCulture),
        "date" => cell.Formula == null && cell.Kind == "DateTime" && DateTime.TryParse(cell.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) && DateOnly.FromDateTime(date) == DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture),
        "formula" => cell.Formula == value,
        _ => false
    };

    private static string Limit(string value) => value.Length <= 1024 ? value : value[..1024];
    private static long Key(int row, int column) => ((long)row << 16) | (uint)column;
    private static CellInfo Project(IXLCell cell)
    {
        // CachedValue never requests calculation, link refresh or formula execution.
        var value = cell.CachedValue; var text = value.ToString(CultureInfo.InvariantCulture);
        var formula = cell.HasFormula ? cell.FormulaA1 : null; var fill = cell.Style.Fill.BackgroundColor;
        return new(cell.Address.RowNumber, cell.Address.ColumnNumber, Limit(text), value.Type.ToString(), formula == null ? null : Limit(formula), text.Length > 1024 || formula?.Length > 1024,
            new(Limit(cell.Style.NumberFormat.Format), cell.Style.NumberFormat.NumberFormatId, cell.Style.Font.Bold, cell.Style.Font.Italic,
                fill.ColorType == XLColorType.Color ? fill.Color.ToArgb().ToString("X8", CultureInfo.InvariantCulture) : ""));
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            try { workbook?.Dispose(); } finally { inputStream?.Dispose(); inputStream = null; workbook = null; baseline = []; currentBytes = []; copyBaseBytes = null; cellsBySheet.Clear(); changes.Clear(); release(); }
        }
    }
}
