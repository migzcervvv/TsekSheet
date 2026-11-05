using System.Globalization;
using ClosedXML.Excel;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed class WorkbookSession : IWorkbookSession
{
    private readonly WorkbookEngine engine;
    private byte[] sourceBytes;
    private readonly PackageFacts facts;
    private readonly long estimate;
    private XLWorkbook? workbook;
    private Stream? inputStream;
    private Action release;
    private WorkbookInfo info;
    private readonly object gate = new();
    private Dictionary<Guid, Dictionary<long, CellInfo>> cellsBySheet = [];
    private bool disposed;

    internal WorkbookSession(WorkbookEngine engine, XLWorkbook workbook, Stream inputStream, byte[] sourceBytes, PackageFacts facts,
        string displayName, string hash, Guid? parent, IReadOnlyList<WorksheetInfo>? identities, long estimate, Action release)
    {
        this.engine = engine; this.sourceBytes = sourceBytes; this.facts = facts; this.estimate = estimate;
        this.workbook = workbook; this.inputStream = inputStream; this.release = release;
        var sheets = workbook.Worksheets.Select(s => new WorksheetInfo(identities?.Single(i => i.Name == s.Name).Id ?? Guid.NewGuid(), s.Name, s.Position,
            s.Visibility.ToString(), s.LastRowUsed(XLCellsUsedOptions.Contents)?.RowNumber() ?? 0, s.LastColumnUsed(XLCellsUsedOptions.Contents)?.ColumnNumber() ?? 0)).ToArray();
        info = new(Guid.NewGuid(), parent, displayName, hash, 0, false, false, Limit(facts.Creator), Limit(facts.Title), Array.AsReadOnly(sheets), facts.Findings);
        foreach (var s in sheets)
            cellsBySheet[s.Id] = workbook.Worksheet(s.Name).CellsUsed(XLCellsUsedOptions.Contents).ToDictionary(c => Key(c.Address.RowNumber, c.Address.ColumnNumber), Project);
        // This adapter exposes cached read-only views only.
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
                var serialized = sourceBytes.ToArray();
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
                return snapshot;
            }
            catch (WorkbookException) { snapshot?.Dispose(); throw; }
            catch (OperationCanceledException) { snapshot?.Dispose(); throw; }
            catch { snapshot?.Dispose(); throw PackageGuard.Reject("SNAPSHOT_FAILED", "Independent snapshot could not be created. The source session remains open."); }
        }
    }

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
            try { workbook?.Dispose(); } finally { inputStream?.Dispose(); inputStream = null; workbook = null; sourceBytes = []; cellsBySheet.Clear(); release(); }
        }
    }
}
