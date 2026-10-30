using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

// A separate trust boundary: no ClosedXML model, serializer or mutation authorization.
public sealed record InspectionLimits
{
    public long FileBytes { get; init; } = 128 * 1024 * 1024;
    public long ExpandedBytes { get; init; } = 512 * 1024 * 1024;
    public long XmlBytes { get; init; } = 64 * 1024 * 1024;
    public int Entries { get; init; } = 4096;
    public int Sheets { get; init; } = 512;
    public int Relationships { get; init; } = 16384;
    public int Cells { get; init; } = 2_000_000;
    public int SharedStrings { get; init; } = 500_000;
    public int Milliseconds { get; init; } = 10_000;
}

public sealed class WorkbookInspection(InspectionLimits? configured = null)
{
    private readonly InspectionLimits limits = configured ?? new();
    private static int sessions;
    public IWorkbookSession Open(string path, CancellationToken token = default)
    {
        if (Interlocked.Increment(ref sessions) > 2) { Interlocked.Decrement(ref sessions); throw Error("Close another read-only inspection before opening more (limit 2)."); }
        FileStream? file = null; ZipArchive? zip = null;
        try
        {
            var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!;
            if (!string.Equals(Path.GetExtension(full), ".xlsx", StringComparison.OrdinalIgnoreCase) || full.StartsWith("\\\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType == DriveType.Network)
                throw Error("Inspection requires a local .xlsx file.");
            var current = root;
            foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            { current = Path.Combine(current, segment); if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Error("Linked or redirected source paths are unavailable."); }
            file = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (file.Length > limits.FileBytes) throw Error($"Inspection file budget is {limits.FileBytes} bytes.");
            var budget = new Budget(limits, token);
            // Incremental fingerprint; no compressed-file byte array is retained.
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[65536]; int read;
            while ((read = file.Read(buffer)) != 0) { budget.Check(); sha.AppendData(buffer, 0, read); }
            var hash = Convert.ToHexString(sha.GetHashAndReset()); file.Position = 0;
            PreflightDirectory(file, limits, budget);
            zip = new ZipArchive(file, ZipArchiveMode.Read, true);
            var session = InspectionSession.Create(file, zip, Path.GetFileName(full), hash, limits, budget, () => Interlocked.Decrement(ref sessions));
            return session;
        }
        catch (OperationCanceledException) { zip?.Dispose(); file?.Dispose(); Interlocked.Decrement(ref sessions); throw; }
        catch (Exception ex)
        {
            zip?.Dispose(); file?.Dispose(); Interlocked.Decrement(ref sessions);
            if (ex is WorkbookException) throw;
            throw new WorkbookException("INSPECTION_REJECTED", "Read-only inspection rejected an invalid, inaccessible or over-budget package. No source bytes were changed.");
        }
    }
    internal static WorkbookException Error(string message) => new("INSPECTION_LIMIT", message);
    // Bound the central directory before ZipArchive can allocate an entry for every record.
    private static void PreflightDirectory(FileStream file, InspectionLimits limits, Budget budget)
    {
        var tail = new byte[(int)Math.Min(file.Length, 65557)]; file.Position = file.Length - tail.Length; file.ReadExactly(tail);
        static ushort U16(ReadOnlySpan<byte> b) => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(b);
        static uint U32(ReadOnlySpan<byte> b) => System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(b);
        var end = -1;
        for (var i = tail.Length - 22; i >= 0; i--) if (U32(tail.AsSpan(i)) == 0x06054b50 && i + 22 + U16(tail.AsSpan(i + 20)) == tail.Length) { end = i; break; }
        if (end < 0) throw Error("ZIP end record is missing.");
        var count = U16(tail.AsSpan(end + 10)); var size = U32(tail.AsSpan(end + 12)); var offset = U32(tail.AsSpan(end + 16));
        if (U16(tail.AsSpan(end + 4)) != 0 || U16(tail.AsSpan(end + 6)) != 0 || U16(tail.AsSpan(end + 8)) != count || count == ushort.MaxValue || count > limits.Entries || size > 4 * 1024 * 1024 || (long)offset + size != file.Length - tail.Length + end)
            throw Error("Split/ZIP64 archives or excessive central directories are outside inspection limits.");
        file.Position = offset; var header = new byte[46]; var seen = 0;
        while (file.Position < (long)offset + size)
        {
            budget.Check(); file.ReadExactly(header);
            if (++seen > limits.Entries || U32(header) != 0x02014b50 || (U16(header.AsSpan(8)) & 1) != 0 || U16(header.AsSpan(10)) is not (0 or 8) || U16(header.AsSpan(28)) > 512)
                throw Error("Encrypted, unsupported or excessive ZIP entries.");
            file.Position += U16(header.AsSpan(28)) + U16(header.AsSpan(30)) + U16(header.AsSpan(32));
        }
        if (seen != count || file.Position != (long)offset + size) throw Error("Inconsistent ZIP central directory.");
        file.Position = 0;
    }
    internal sealed class Budget(InspectionLimits limits, CancellationToken token)
    {
        private readonly Stopwatch clock = Stopwatch.StartNew();
        private long bytes; private int elements;
        public void Check() { token.ThrowIfCancellationRequested(); if (clock.ElapsedMilliseconds > limits.Milliseconds) throw Error("Inspection time budget exceeded; this view request was stopped."); }
        public void Count(int count) { Check(); bytes += count; if (bytes > limits.ExpandedBytes) throw Error("Actual decompression budget exceeded."); }
        public void Node(XmlReader reader) { Check(); if (reader.Depth > 64 || (reader.NodeType == XmlNodeType.Element && ++elements > 12_000_000)) throw Error("XML depth or element budget exceeded."); }
    }
}

internal sealed class InspectionSession : IWorkbookSession
{
    private const string Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string Rel = "http://schemas.openxmlformats.org/package/2006/relationships";
    private readonly FileStream file;
    private readonly ZipArchive zip;
    private readonly InspectionLimits limits;
    private readonly Action release;
    private readonly Dictionary<Guid, string?> sheetParts;
    private readonly string? sharedPart;
    private readonly string? stylesPart;
    private readonly bool date1904;
    private IReadOnlyList<InspectionNumberFormat>? numberFormats;
    private string styleWarning = "";
    private readonly CancellationTokenSource lifetime = new();
    private readonly object gate = new();
    private WorkbookInfo info;
    private readonly LinkedList<CellViewport> cached = new();
    private long cachedBytes;
    private bool disposed;

    private InspectionSession(FileStream file, ZipArchive zip, InspectionLimits limits, Action release, WorkbookInfo info, Dictionary<Guid, string?> parts, string? shared, string? styles, bool date1904)
    { this.file = file; this.zip = zip; this.limits = limits; this.release = release; this.info = info; sheetParts = parts; sharedPart = shared; stylesPart = styles; this.date1904 = date1904; }

    internal static InspectionSession Create(FileStream file, ZipArchive zip, string name, string hash, InspectionLimits limits, WorkbookInspection.Budget budget, Action release)
    {
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var categories = new Dictionary<string, int>();
        void Category(string value) => categories[value] = categories.GetValueOrDefault(value) + 1;
        long expanded = 0, compressed = 0;
        if (zip.Entries.Count > limits.Entries) throw WorkbookInspection.Error($"Package entry limit is {limits.Entries}.");
        foreach (var entry in zip.Entries)
        {
            budget.Check(); var path = entry.FullName;
            if (path.Length > 512 || path.Contains('%') || path.Contains('\\') || path.Contains(':') || path.StartsWith('/') || path.Split('/').Any(p => p is "." or "..") || !entries.TryAdd(path, entry))
                throw WorkbookInspection.Error("Duplicate, encoded or unsafe ZIP entry paths.");
            expanded = checked(expanded + entry.Length); compressed = checked(compressed + entry.CompressedLength);
            if (expanded > limits.ExpandedBytes || (entry.Length > 1024 * 1024 && entry.Length / Math.Max(1, entry.CompressedLength) > 1000)) throw WorkbookInspection.Error("Declared expansion or compression-ratio budget exceeded.");
            var lower = path.ToLowerInvariant();
            Category(lower.Contains("vbaproject") || lower.Contains("macrosheet") ? "VBA/macros (never executed)" : lower.Contains("activex") || lower.Contains("ctrlprop") ? "ActiveX/controls (ignored)" : lower.Contains("embedding") ? "Embedded OLE/objects (ignored)" : lower.StartsWith("xl/media/") ? "Images (not rendered)" : lower.StartsWith("xl/drawings/") || lower.StartsWith("xl/charts/") ? "Drawings/charts (not rendered)" : lower.Contains("externallink") || lower.Contains("connection") || lower.Contains("querytable") ? "External data (never resolved)" : lower.EndsWith(".bin") ? "Unknown binary (ignored)" : lower.EndsWith(".xml") || lower.EndsWith(".rels") ? "XML parts (not schema certified)" : "Other/unknown parts (ignored)");
        }
        XDocument Document(string path, long max)
        {
            if (!entries.TryGetValue(path, out var entry)) throw WorkbookInspection.Error("Required package part is missing.");
            using (var check = Reader(entry, Math.Min(max, limits.XmlBytes), budget))
            {
                var nodes = 0;
                while (check.Read()) { budget.Node(check); if (++nodes > 100_000) throw WorkbookInspection.Error("Metadata XML node budget exceeded."); }
            }
            using var reader = Reader(entry, Math.Min(max, limits.XmlBytes), budget); return XDocument.Load(reader);
        }
        if (Document("[Content_Types].xml", 4 * 1024 * 1024).Root?.Name != XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types")) throw WorkbookInspection.Error("Invalid content types.");
        var allRels = new Dictionary<string, Dictionary<string, (string Type, string? Target)>>(); int relationships = 0; bool external = false;
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase)))
        {
            var doc = Document(entry.FullName, 4 * 1024 * 1024);
            if (doc.Root?.Name != XName.Get("Relationships", Rel)) throw WorkbookInspection.Error("Invalid relationship XML.");
            var rels = new Dictionary<string, (string, string?)>();
            var source = entry.FullName == "_rels/.rels" ? "" : entry.FullName.Replace("/_rels/", "/", StringComparison.Ordinal)[..^5];
            if (source != "" && !entries.ContainsKey(source)) throw WorkbookInspection.Error("Relationship source part is missing.");
            foreach (var r in doc.Root.Elements())
            {
                budget.Check(); if (++relationships > limits.Relationships) throw WorkbookInspection.Error("Relationship budget exceeded.");
                var id = (string?)r.Attribute("Id"); var type = (string?)r.Attribute("Type") ?? ""; var target = (string?)r.Attribute("Target") ?? "";
                if (r.Name != XName.Get("Relationship", Rel) || string.IsNullOrEmpty(id) || id.Length > 512 || type.Length > 512 || target.Length > 2048) throw WorkbookInspection.Error("Invalid relationship.");
                var mode = (string?)r.Attribute("TargetMode"); string? resolved = null;
                if (mode == "External") { external = true; Category("External relationships (never followed)"); }
                else
                {
                    if (mode != null && mode != "Internal") throw WorkbookInspection.Error("Unknown relationship target mode.");
                    if (target.Contains('%') || target.Contains('\\') || target.Contains(':') || !Uri.TryCreate(new Uri("https://package/" + source), target, out var uri) || uri.Host != "package" || uri.Fragment.Length != 0 || uri.Query.Length != 0)
                        throw WorkbookInspection.Error("Unsafe internal relationship.");
                    resolved = uri.AbsolutePath.TrimStart('/');
                    if (!entries.TryGetValue(resolved, out var targetEntry) || targetEntry.FullName != resolved) throw WorkbookInspection.Error("Broken or case-mismatched internal relationship.");
                }
                if (!rels.TryAdd(id, (type, resolved))) throw WorkbookInspection.Error("Duplicate relationship identifiers.");
                var known = new[] { "officeDocument", "worksheet", "sharedStrings", "styles", "theme", "core-properties", "extended-properties" };
                if (!known.Contains(type.Split('/').Last())) Category("Preservation-sensitive/unknown relationships");
            }
            allRels.Add(source, rels);
        }
        if (!allRels.TryGetValue("", out var rootRels)) throw WorkbookInspection.Error("Missing root relationships.");
        var workbooks = rootRels.Values.Where(r => r.Type.EndsWith("/officeDocument", StringComparison.Ordinal)).ToArray();
        if (workbooks.Length != 1 || workbooks[0].Target is not string workbookPart) throw WorkbookInspection.Error("Missing or ambiguous workbook relationship.");
        var wb = Document(workbookPart, 4 * 1024 * 1024);
        if (wb.Root?.Name != XName.Get("workbook", Main)) throw WorkbookInspection.Error("Only transitional OOXML workbook metadata is supported.");
        if (!allRels.TryGetValue(workbookPart, out var workbookRels)) throw WorkbookInspection.Error("Missing workbook relationships.");
        XNamespace ns = Main; XNamespace rns = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var sheets = new List<WorksheetInfo>(); var parts = new Dictionary<Guid, string?>();
        foreach (var sheet in wb.Root.Element(ns + "sheets")?.Elements(ns + "sheet") ?? [])
        {
            if (sheets.Count >= limits.Sheets) throw WorkbookInspection.Error($"Metadata worksheet limit is {limits.Sheets}.");
            var title = (string?)sheet.Attribute("name") ?? ""; var id = (string?)sheet.Attribute(rns + "id") ?? ""; var state = (string?)sheet.Attribute("state") ?? "visible";
            if (title.Length is < 1 or > 512 || sheets.Any(s => s.Name == title) || !workbookRels.TryGetValue(id, out var relationship) || relationship.Target == null || state is not ("visible" or "hidden" or "veryHidden")) throw WorkbookInspection.Error("Invalid worksheet metadata.");
            var guid = Guid.NewGuid(); parts.Add(guid, relationship.Type.EndsWith("/worksheet", StringComparison.Ordinal) ? relationship.Target : null);
            sheets.Add(new(guid, title, sheets.Count + 1, state == "hidden" ? "Hidden" : state == "veryHidden" ? "VeryHidden" : "Visible", 0, 0));
        }
        if (sheets.Count == 0) throw WorkbookInspection.Error("No worksheet metadata.");
        if (wb.Descendants(ns + "extLst").Any()) Category("Unknown workbook extensions");
        var names = wb.Root.Element(ns + "definedNames")?.Elements(ns + "definedName").Count() ?? 0;
        var caps = new[] { new WorkbookCapability("Worksheet selection", true, "Ordered tab metadata; selecting a tab grants no mutation permission."), new WorkbookCapability("Metadata inspection", true, "Bounded package metadata; no preservation certification."), new WorkbookCapability("Read-only browsing", parts.Values.Any(p => p != null), "Ordinary selected-sheet XML only, subject to per-request budgets; non-worksheet tabs cannot be viewed.") }
            .Concat(new[] { "Cell editing", "Working copies", "Structural insertion", "Merge", "Structural Merge", "Export" }.Select(op => new WorkbookCapability(op, false, "Inspection sessions never authorize mutation. Reopen through Open workbook and pass full editable validation; production structural Apply remains disabled."))).ToArray();
        var report = new WorkbookCompatibilityReport(file.Length, compressed, expanded, zip.Entries.Count, names, relationships, external,
            categories.Select(p => new PackageCategory(p.Key, p.Value)).ToArray(), caps,
            $"{sheets.Count} tabs; editable limit {new WorkbookLimits().MaxWorksheets}. Cell counts and dimensions are unmeasured until a selected-sheet scan completes. Editable stored-cell limit {new WorkbookLimits().MaxCells}.");
        var info = new WorkbookInfo(Guid.NewGuid(), null, name, hash, 0, false, false, "", "", sheets.AsReadOnly(),
            [new("READ_ONLY_INSPECTION", "Package inspection does not establish edit/export eligibility. Content outside the requested ordinary cells is ignored, never rewritten."), new("RAW_VALUES", "Raw values are retained alongside limited display interpretation. Formula caches may be blank or stale; only qualified same-sheet shared formulas are reconstructed.")], Compatibility: report);
        var shared = workbookRels.Values.Where(r => r.Type.EndsWith("/sharedStrings", StringComparison.Ordinal)).ToArray();
        if (shared.Length > 1) throw WorkbookInspection.Error("Ambiguous shared-string parts.");
        var styles = workbookRels.Values.Where(r => r.Type.EndsWith("/styles", StringComparison.Ordinal)).ToArray();
        if (styles.Length > 1) throw WorkbookInspection.Error("Ambiguous style parts.");
        var dateSystem = (string?)wb.Root.Element(ns + "workbookPr")?.Attribute("date1904") ?? "0";
        if (dateSystem is not ("0" or "1" or "true" or "false")) throw WorkbookInspection.Error("Invalid workbook date system.");
        return new(file, zip, limits, release, info, parts, shared.SingleOrDefault().Target, styles.SingleOrDefault().Target, dateSystem is "1" or "true");
    }

    private static XmlReader Reader(ZipArchiveEntry entry, long max, WorkbookInspection.Budget budget)
    {
        if (entry.Length > max) throw WorkbookInspection.Error($"Selected XML part exceeds {max} bytes; metadata remains available.");
        return XmlReader.Create(new LimitedStream(entry.Open(), max, budget), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = true, MaxCharactersInDocument = max, IgnoreComments = true, IgnoreProcessingInstructions = true });
    }
    private sealed class LimitedStream(Stream inner, long max, WorkbookInspection.Budget budget) : Stream
    {
        private long bytes;
        public override int Read(byte[] buffer, int offset, int count) { var n = inner.Read(buffer, offset, count); bytes += n; budget.Count(n); if (bytes > max) throw WorkbookInspection.Error("Actual XML decompression budget exceeded."); return n; }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { } public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private static string Text(XmlReader reader, WorkbookInspection.Budget budget, out bool truncated, char[] buffer, bool retain = true)
    {
        var result = retain ? new System.Text.StringBuilder() : null; int count; bool nonempty = false; truncated = false;
        while ((count = reader.ReadValueChunk(buffer, 0, buffer.Length)) > 0) { budget.Check(); nonempty = true; if (result == null) continue; var take = Math.Min(count, 1024 - result.Length); result.Append(buffer, 0, take); truncated |= take < count; }
        return result?.ToString() ?? (nonempty ? "1" : "");
    }
    public WorkbookInfo Info { get { lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); return info; } } }
    public WorkingCopyInfo? WorkingCopy => null;
    public CellViewport ReadViewport(Guid worksheetId, int startRow, int startColumn, int rows, int columns)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (startRow < 1 || startColumn < 1 || rows is < 1 or > 20 || columns is < 1 or > 10 || (long)startRow + rows - 1 > 1048576 || (long)startColumn + columns - 1 > 16384) throw WorkbookInspection.Error("Viewport is outside the bounded 20 by 10 window.");
            if (!sheetParts.TryGetValue(worksheetId, out var part) || part == null) throw WorkbookInspection.Error("Only ordinary worksheet tabs can be viewed.");
            var hit = cached.FirstOrDefault(c => c.WorksheetId == worksheetId && c.StartRow == startRow && c.StartColumn == startColumn && c.Rows == rows && c.Columns == columns);
            if (hit != null) { cached.Remove(hit); cached.AddFirst(hit); return hit; }
            var budget = new WorkbookInspection.Budget(limits, lifetime.Token);
            try
            {
                var view = Scan(part, worksheetId, startRow, startColumn, rows, columns, budget);
                var bytes = CacheSize(view);
                while (cached.Count > 0 && (cached.Count >= 8 || cachedBytes + bytes > 2 * 1024 * 1024)) { cachedBytes -= CacheSize(cached.Last!.Value); cached.RemoveLast(); }
                if (bytes <= 2 * 1024 * 1024) { cached.AddFirst(view); cachedBytes += bytes; }
                return view;
            }
            catch (XmlException) { throw WorkbookInspection.Error("Selected worksheet XML is malformed; metadata remains available."); }
            catch (InvalidDataException) { throw WorkbookInspection.Error("Selected worksheet compression is invalid; metadata remains available."); }
        }
    }
    private static long CacheSize(CellViewport view) => 4096L + view.Cells.Sum(c => 256L + 2L * (c.Value.Length + (c.Formula?.Length ?? 0) + c.Format.NumberFormat.Length + (c.Interpretation is { } i ? i.RawValue.Length + i.DisplayValue.Length + i.Warning.Length + i.FormulaStatus.Length : 0)));
    private InspectionNumberFormat Format(string? style, WorkbookInspection.Budget budget)
    {
        if (numberFormats == null)
        {
            try
            {
                var codes = new Dictionary<int, string>(); var ids = new List<(int Id, bool Ambiguous)>();
                if (stylesPart != null)
                {
                    using var reader = Reader(zip.GetEntry(stylesPart)!, Math.Min(limits.XmlBytes, 4 * 1024 * 1024), budget);
                    bool inCells = false, inFormats = false; int xfs = 0;
                    while (reader.Read())
                    {
                        budget.Node(reader);
                        if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 1 && reader.LocalName == "cellXfs") inCells = false;
                        if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 1 && reader.LocalName == "numFmts") inFormats = false;
                        if (reader.NodeType != XmlNodeType.Element) continue;
                        if (reader.Depth == 0 && (reader.LocalName != "styleSheet" || reader.NamespaceURI != Main)) throw WorkbookInspection.Error("Invalid stylesheet.");
                        if (reader.NamespaceURI != Main) continue;
                        if (reader.LocalName == "cellXfs") { if (reader.Depth != 1) throw WorkbookInspection.Error("Invalid cell style nesting."); inCells = !reader.IsEmptyElement; }
                        if (reader.LocalName == "numFmts") inFormats = reader.Depth == 1 && !reader.IsEmptyElement;
                        if (reader.LocalName == "numFmt")
                        {
                            if (!inFormats || reader.Depth != 2) throw WorkbookInspection.Error("Invalid number format nesting.");
                            var code = reader.GetAttribute("formatCode") ?? "";
                            if (codes.Count >= 1024 || code.Length > 256 || !int.TryParse(reader.GetAttribute("numFmtId"), out var n) || n < 164 || !codes.TryAdd(n, code)) throw WorkbookInspection.Error("Unsupported or excessive custom formats.");
                        }
                        if (reader.LocalName == "xf")
                        {
                            if (++xfs > 4096) throw WorkbookInspection.Error("Style record budget exceeded.");
                            if (inCells) { if (reader.Depth != 2) throw WorkbookInspection.Error("Invalid style nesting."); var valid = int.TryParse(reader.GetAttribute("numFmtId"), out var n) && n >= 0; ids.Add((valid ? n : -1, !valid || reader.GetAttribute("applyNumberFormat") is "0" or "false" || reader.GetAttribute("xfId") is not (null or "0"))); }
                        }
                    }
                }
                numberFormats = ids.Select(i => new InspectionNumberFormat(i.Id, codes.GetValueOrDefault(i.Id) ?? (i.Id switch { 0 => "General", 1 => "0", 2 => "0.00", 3 => "#,##0", 4 => "#,##0.00", 9 => "0%", 10 => "0.00%", >= 14 and <= 22 => "Built-in date/time " + i.Id, _ => "Unsupported built-in " + i.Id }), i.Ambiguous ? "Inherited or disabled number format is ambiguous; raw value retained." : "")).ToArray();
            }
            catch (Exception ex) when (ex is WorkbookException or XmlException or InvalidDataException) { numberFormats = []; styleWarning = "Styles are malformed, ambiguous or over budget; raw values retained."; }
        }
        if (styleWarning.Length > 0) return new(-1, "", styleWarning);
        if (style == null && numberFormats.Count == 0) return new(0, "General");
        if (!int.TryParse(style ?? "0", out var index) || index < 0 || index >= numberFormats.Count) return new(-1, "", "Missing or invalid cell style; raw value retained.");
        return numberFormats[index];
    }
    private CellViewport Scan(string part, Guid id, int row, int column, int rows, int columns, WorkbookInspection.Budget budget)
    {
        var found = new Dictionary<(int, int), CellInfo>(); var shared = new Dictionary<(int, int), int>(); var warnings = new HashSet<string>();
        var textBuffer = new char[1024];
        var groups = new InspectionSharedGroups(); var sharedFormulas = new Dictionary<(int, int), string?>();
        int previousRowElement = 0, currentRow = 0;
        bool rowOpen = false, inSheetData = false;
        long cells = 0, nonempty = 0, formulas = 0, styled = 0; int lastRow = 0, lastColumn = 0, valueRow = 0, valueColumn = 0, previousRow = 0, previousColumn = 0;
        string dimension = "Not declared";
        using (var reader = Reader(zip.GetEntry(part)!, limits.XmlBytes, budget))
        {
            while (reader.Read())
            {
                budget.Node(reader);
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "row" && reader.Depth == 2) { rowOpen = false; if (currentRow == 0 && ++previousRowElement > 1048576) throw WorkbookInspection.Error("Inferred row exceeds worksheet bounds."); }
                if (reader.NodeType == XmlNodeType.EndElement && reader.LocalName == "sheetData" && reader.Depth == 1) inSheetData = false;
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (reader.Depth == 0 && (reader.LocalName != "worksheet" || reader.NamespaceURI != Main)) throw WorkbookInspection.Error("Requested part is not an ordinary worksheet.");
                if (reader.NamespaceURI != Main) { if (warnings.Count < 12) warnings.Add("Unknown XML extension content is not rendered."); continue; }
                if (reader.LocalName == "sheetData" && reader.Depth == 1) inSheetData = !reader.IsEmptyElement;
                if (reader.LocalName == "row")
                {
                    if (reader.Depth != 2 || !inSheetData) throw WorkbookInspection.Error("Malformed row nesting.");
                    rowOpen = !reader.IsEmptyElement;
                    var rowText = reader.GetAttribute("r"); currentRow = 0;
                    if (rowText != null) { if (!int.TryParse(rowText, out currentRow) || currentRow <= previousRowElement || currentRow > 1048576) throw WorkbookInspection.Error("Duplicate, malformed or out-of-order row elements."); previousRowElement = currentRow; }
                    else if (reader.IsEmptyElement && ++previousRowElement > 1048576) throw WorkbookInspection.Error("Inferred row exceeds worksheet bounds.");
                }
                if (reader.LocalName == "dimension") { dimension = reader.GetAttribute("ref") ?? "Not declared"; if (dimension.Length > 64) throw WorkbookInspection.Error("Invalid dimension declaration."); }
                if (reader.LocalName is "drawing" or "oleObjects" or "controls" or "mergeCells" or "tableParts" or "extLst") warnings.Add("Drawings, objects, merges, tables and extensions are not rendered.");
                if (reader.LocalName != "c") continue;
                if (reader.Depth != 3 || !rowOpen) throw WorkbookInspection.Error("Unexpected cell structure is outside the view prototype.");
                if (++cells > limits.Cells) throw WorkbookInspection.Error($"Selected sheet exceeds {limits.Cells} stored cell elements; this view was stopped.");
                var address = Address(reader.GetAttribute("r") ?? ""); var (r, col) = address;
                if (currentRow == 0) { if (r <= previousRowElement) throw WorkbookInspection.Error("Out-of-order inferred row."); currentRow = previousRowElement = r; }
                if (r != currentRow) throw WorkbookInspection.Error("Cell address disagrees with its row.");
                var retain = r >= row && r < row + rows && col >= column && col < column + columns;
                if (r < previousRow || r == previousRow && col <= previousColumn) throw WorkbookInspection.Error("Unordered or duplicate cell addresses are outside the view prototype.");
                previousRow = r; previousColumn = col; lastRow = Math.Max(lastRow, r); lastColumn = Math.Max(lastColumn, col);
                var style = reader.GetAttribute("s"); if (style != null) styled++;
                var type = reader.GetAttribute("t") ?? "n"; var formulaType = ""; string? formula = null; string value = ""; bool truncated = false, hasValue = false;
                string? formulaIndex = null, formulaRange = null;
                using (var cell = reader.ReadSubtree())
                {
                    string field = "";
                    bool formulaSeen = false;
                    while (cell.Read())
                    {
                        budget.Node(cell);
                        if (cell.NodeType == XmlNodeType.Element && cell.LocalName == "rPh") { using var annotation = cell.ReadSubtree(); while (annotation.Read()) budget.Node(annotation); warnings.Add("Phonetic annotations are not rendered."); continue; }
                        if (cell.NodeType == XmlNodeType.Element && cell.NamespaceURI == Main) { field = cell.LocalName; if (field == "f") { if (formulaSeen) throw WorkbookInspection.Error("Multiple formulas in one cell."); formulaSeen = true; formula = ""; formulaType = cell.GetAttribute("t") ?? ""; formulaIndex = cell.GetAttribute("si"); formulaRange = cell.GetAttribute("ref"); } }
                        else if (cell.NodeType == XmlNodeType.EndElement) field = "";
                        else if (cell.NodeType is XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace)
                        {
                            var text = Text(cell, budget, out var cut, textBuffer, retain || field == "f" && formulaType == "shared"); truncated |= cut;
                            if (field == "f") { truncated |= (formula?.Length ?? 0) + text.Length > 1024; formula = ((formula ?? "") + text)[..Math.Min(1024, (formula?.Length ?? 0) + text.Length)]; }
                            if (field is "v" or "t") { hasValue |= text.Length > 0; truncated |= value.Length + text.Length > 1024; value = (value + text)[..Math.Min(1024, value.Length + text.Length)]; }
                        }
                    }
                }
                if (hasValue) { nonempty++; valueRow = Math.Max(valueRow, r); valueColumn = Math.Max(valueColumn, col); }
                if (formula != null) formulas++;
                if (formulaType == "shared") groups.Add(r, col, formulaIndex, formulaRange, formula ?? "", truncated);
                if (formulaType is "array" or "dataTable") warnings.Add("Array/spill/data-table formulas are not reconstructed or evaluated.");
                if (r < row || r >= row + rows || col < column || col >= column + columns) continue;
                var kind = type switch { "s" or "inlineStr" or "str" => "Text", "b" => "Boolean", "d" => "Date", "n" => "Number", "e" => "Error", _ => "Unsupported" };
                if (type == "s") { if (!int.TryParse(value, out var index) || index < 0 || truncated) throw WorkbookInspection.Error("Invalid shared-string index."); shared[address] = index; value = ""; }
                if (type == "b") value = value == "1" ? "TRUE" : value == "0" ? "FALSE" : value;
                var format = Format(style, budget);
                var formulaStatus = formula == null ? "No formula" : formulaType == "" || formulaType == "normal" ? "Stored formula; cached value is unverified." : "Unsupported " + formulaType + " formula; cached value is unverified.";
                if (formulaType == "shared") sharedFormulas[address] = formulaIndex;
                found[address] = new(r, col, value, kind, formula, truncated, new(format.Code, format.Id, false, false, ""), InspectionCellFormatting.Interpret(value, kind, format, date1904, truncated, formulaStatus));
            }
        }
        if (shared.Count > 0)
        {
            if (sharedPart == null) throw WorkbookInspection.Error("Missing shared-string part.");
            var needed = shared.Values.ToHashSet(); var strings = new Dictionary<int, (string Text, bool Cut)>(); var index = -1;
            using var reader = Reader(zip.GetEntry(sharedPart)!, limits.XmlBytes, budget);
            while (reader.Read())
            {
                budget.Node(reader); if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "si" || reader.NamespaceURI != Main) continue;
                if (++index >= limits.SharedStrings) throw WorkbookInspection.Error($"Shared-string scan limit is {limits.SharedStrings}; this viewport is unavailable.");
                string value = ""; bool truncated = false;
                using var item = reader.ReadSubtree(); bool textNode = false;
                while (item.Read()) { budget.Node(item); if (item.NodeType == XmlNodeType.Element && item.LocalName == "rPh") { using var annotation = item.ReadSubtree(); while (annotation.Read()) budget.Node(annotation); warnings.Add("Phonetic annotations are not rendered."); continue; } if (item.NodeType == XmlNodeType.Element) textNode = item.LocalName == "t" && item.NamespaceURI == Main; else if (item.NodeType == XmlNodeType.EndElement) textNode = false; else if (textNode && item.NodeType is XmlNodeType.Text or XmlNodeType.CDATA) { var text = Text(item, budget, out var cut, textBuffer, needed.Contains(index)); truncated |= cut || value.Length + text.Length > 1024; if (needed.Contains(index)) value = (value + text)[..Math.Min(1024, value.Length + text.Length)]; } }
                if (needed.Contains(index)) strings[index] = (value, truncated);
            }
            foreach (var pair in shared) { if (!strings.TryGetValue(pair.Value, out var text)) throw WorkbookInspection.Error("Shared-string index is out of range."); found[pair.Key] = found[pair.Key] with { Value = text.Text, Truncated = found[pair.Key].Truncated || text.Cut, Interpretation = found[pair.Key].Interpretation! with { RawValue = text.Text, DisplayValue = text.Text, Warning = text.Cut ? "Shared text truncated." : "" } }; }
        }
        groups.Validate(budget.Check);
        foreach (var pair in sharedFormulas) { var resolved = groups.Resolve(pair.Value, pair.Key.Item1, pair.Key.Item2); var cell = found[pair.Key]; found[pair.Key] = cell with { Formula = resolved.Formula ?? cell.Formula, Interpretation = cell.Interpretation! with { FormulaStatus = resolved.Status } }; }
        var reliability = "Untrusted declaration; stored-cell extent measured by complete selected-sheet scan"; bool inflated = lastRow > Math.Max(1000, valueRow * 10L) || lastColumn > Math.Max(100, valueColumn * 10L);
        if (dimension != "Not declared") { try { var extent = Address(dimension.Split(':').Last()); inflated |= extent.Row > Math.Max(1000, lastRow * 10L) || extent.Column > Math.Max(100, lastColumn * 10L); } catch (WorkbookException) { reliability = "Invalid dimension declaration; ignored"; } }
        var scan = new WorksheetScan(dimension, reliability, cells, nonempty, formulas, styled, lastRow, lastColumn, inflated, warnings.ToArray());
        var result = new List<CellInfo>();
        for (var r = row; r < row + rows; r++) for (var col = column; col < column + columns; col++) result.Add(found.GetValueOrDefault((r, col)) ?? new(r, col, "", "Blank", null, false, new("", -1, false, false, "")));
        return new(info.SessionId, id, row, column, rows, columns, result.AsReadOnly(), scan);
    }
    private static (int Row, int Column) Address(string address)
    {
        int col = 0, i = 0; while (i < address.Length && address[i] is >= 'A' and <= 'Z') { if (i >= 3) throw WorkbookInspection.Error("Invalid cell address."); col = col * 26 + address[i++] - 'A' + 1; }
        if (col is < 1 or > 16384 || !int.TryParse(address.AsSpan(i), out var row) || row is < 1 or > 1048576) throw WorkbookInspection.Error("Invalid cell address."); return (row, col);
    }
    private static WorkbookException ReadOnly() => new("READ_ONLY_INSPECTION", "Read-only package inspection cannot authorize copies, edits, integration or export. Reopen and pass full editable validation.");
    public IWorkbookSession CreateSnapshot(CancellationToken cancellationToken = default) => throw ReadOnly();
    public IWorkbookSession CreateWorkingCopy(Guid worksheetId, int expectedRevision, CancellationToken cancellationToken = default) => throw ReadOnly();
    public WorkbookInfo Edit(CellEdit edit, CancellationToken cancellationToken = default) => throw ReadOnly();
    public SaveReceipt SaveAsNew(string destination, int expectedRevision, CancellationToken cancellationToken = default) => throw ReadOnly();
    public void Dispose() { lifetime.Cancel(); lock (gate) { if (disposed) return; disposed = true; cached.Clear(); cachedBytes = 0; numberFormats = null; zip.Dispose(); file.Dispose(); release(); } }
}
