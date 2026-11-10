using System.Runtime.InteropServices;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;
using System.Diagnostics;

namespace TsekSheet.Excel;

public sealed partial class ExcelHost
{
    private sealed record PendingInsert(string WorkbookId, ExcelSectionDefinition Section, InsertValue[] Values,
        PreservationSnapshot Before, int Row, int Left, int Right, string?[] Subtotals, string?[] Rewritten,
        FormulaReview[] Warnings, long AddedValidation, bool InsertNew);
    private readonly Dictionary<string, PendingInsert> insertPlans = [];
    public Action<PreviewStageTiming>? PreviewTiming { get; set; }

    public Task<SectionInsertPreview> PreviewInsertAsync(string workbookId, ExcelSectionDefinition section, IReadOnlyList<InsertValue> values) =>
        queue.InvokeAsync(() => WithSheet(workbookId, section.SheetName, (workbook, sheet) =>
        {
            if (books[workbookId].Faulted) throw new ExcelInsertRejectedException("This workbook needs manual review after a readback failure.");
            if (sheet.ProtectContents) throw new ExcelInsertRejectedException("The target worksheet is protected. Unprotect it in Excel before inserting.");
            var fields = values.ToArray();
            var columns = section.ColumnMap.Values.Concat(section.InputColumns ?? []).Concat(section.FormulaColumns).Append(section.SubtotalColumn).Select(A1Formula.ColumnNumber).ToArray();
            var left = columns.Min(); var right = columns.Max();
            if (fields.Length != section.ColumnMap.Count || fields.Select(v => v.Column).Distinct().Count() != fields.Length ||
                fields.Any(v => !section.ColumnMap.TryGetValue(v.Key, out var column) || A1Formula.ColumnNumber(column) != v.Column))
                throw new ArgumentException("Values must match the profile's exact column map.");
            fields = fields.Select(field => field with { Value = Normalize(new object?[,] { { field.Value } }, new(1, 1, 1, 1))[0, 0] }).ToArray();
            var date1904 = workbook.Date1904;
            fields = fields.Select(field =>
            {
                if (!field.IsDate || field.Value is null) return field;
                if (field.Value is not double serial) throw new ArgumentException("A date must supply its OLE Automation serial.");
                var adjusted = date1904 ? serial - 1462d : serial < 61d ? serial - 1d : serial;
                if (adjusted < (date1904 ? 0d : 1d)) throw new ExcelInsertRejectedException("A verified date precedes this workbook's supported date system.");
                return field with { Value = adjusted };
            }).ToArray();
            var watch = Stopwatch.StartNew();
            var before = ExcelReadback.Capture(workbook);
            PreviewTiming?.Invoke(new("excel.preservationCapture", watch.Elapsed.TotalMilliseconds)); watch.Restart();
            var snapshot = before.Sheets.Single(s => s.Name == sheet.Name);
            var boundary = ExcelLayoutMatching.Resolve(snapshot.Data, section);
            PreviewTiming?.Invoke(new("excel.layout", watch.Elapsed.TotalMilliseconds));
            var inputColumns = (section.InputColumns ?? section.ColumnMap.Values.ToArray()).Select(A1Formula.ColumnNumber).ToArray();
            bool Compatible(int r) => inputColumns.All(c =>
                Formula(snapshot.Data, r, c) is null
                    ? Cell(snapshot.Data, r, c) is null or ""
                    : section.HybridInputs && (section.HybridFormulaColumns ?? []).Any(column => A1Formula.ColumnNumber(column) == c) && fields.SingleOrDefault(f => f.Column == c)?.Value is null);
            var blank = section.FillBlankRowsFirst ? Enumerable.Range(boundary.First, boundary.Subtotal - boundary.First)
                .FirstOrDefault(r => Compatible(r) && (!section.HybridInputs || SafeHybridRow(sheet, snapshot.Data, r, left, right))) : 0;
            var insertNew = blank == 0;
            var row = insertNew ? boundary.Subtotal : blank;
            if (section.HybridInputs && insertNew && section.InsertMode != "entireRow" && !SafeHybridRow(sheet, snapshot.Data, row - 1, left, right))
                throw new ExcelInsertRejectedException("The subsection has no safe row template. Review its final data row in Excel.");
            foreach (var field in fields)
            {
                if (section.SheetMatch is null && !Equals(Cell(snapshot.Data, section.HeaderRow, field.Column), field.Header)) throw new ExcelInsertRejectedException("The profile headers do not match this workbook section.");
                var preserveOptionalFormula = section.HybridInputs && !insertNew && field.Value is null;
                var createBlankInput = section.HybridInputs && insertNew && section.BlankHybridNewRows;
                if (Formula(snapshot.Data, insertNew ? row - 1 : row, field.Column) is not null && !preserveOptionalFormula && !createBlankInput) throw new ExcelInsertRejectedException($"Column {new CellRange(1, field.Column, 1, 1).Address} contains a formula in the target or new-row template. Choose a compatible subsection or review its template in Excel.");
            }
            if (section.HybridInputs && !insertNew)
                fields = fields.Select(field => field.Value is null && Formula(snapshot.Data, row, field.Column) is not null
                    ? field with { DisplayValue = "Existing formula preserved" } : field).ToArray();
            foreach (var formulaColumn in section.FormulaColumns.Select(A1Formula.ColumnNumber))
                if (Formula(snapshot.Data, insertNew ? row - 1 : row, formulaColumn) is null) throw new ExcelInsertRejectedException("A formula column is missing its profile template formula.");
            try { ValidateInsertTarget(sheet, section, insertNew, row, left, right); }
            catch (InvalidOperationException error) { throw new ExcelInsertRejectedException(error.Message); }
            if (section.InsertMode is not ("tableRow" or "entireRow" or "columnsOnly")) throw new ArgumentException("Unknown insert mode.");
            if (section.InsertMode == "tableRow") ValidateTable(sheet, section, row, left, right);
            var originals = new string?[right - left + 1]; var rewritten = new string?[originals.Length];
            var warnings = new List<FormulaReview>();
            for (var c = left; insertNew && c <= right; c++)
            {
                var formula = Formula(snapshot.Data, row, c);
                originals[c - left] = formula;
                if (formula is null) continue;
                if (A1Formula.TryExtendSubtotal(formula, sheet.Name, row - 1, row, out var extended)) rewritten[c - left] = extended;
                else { rewritten[c - left] = formula; warnings.Add(new(new CellRange(row + 1, c, 1, 1).Address, formula, "Formula is outside the supported same-sheet A1 grammar; left unchanged. Review its total in Excel.")); }
            }
            var token = Guid.NewGuid().ToString("N");
            if (insertPlans.Count >= 64) insertPlans.Clear();
            var pending = new PendingInsert(workbookId, section, fields, before, row, left, right, originals, rewritten, warnings.ToArray(),
                insertNew ? TemplateValidation(sheet, row - 1, section.InsertMode == "entireRow" ? 1 : left, section.InsertMode == "entireRow" ? 16384 : right) : 0, insertNew);
            insertPlans.Add(token, pending);
            return new SectionInsertPreview(token, workbookId, section.Id, sheet.Name, new CellRange(row, left, 1, right - left + 1).Address, row, fields, warnings);
        }));

    public Task<SectionInsertResult> InsertAsync(string previewToken) => queue.InvokeAsync(() =>
    {
        if (!insertPlans.Remove(previewToken, out var pending)) throw new ExcelInsertRejectedException("Preview expired or was already used. Request a new preview.");
        return WithSheet(pending.WorkbookId, pending.Section.SheetName, (workbook, sheet) => InsertCore(workbook, sheet, pending));
    });

    private SectionInsertResult InsertCore(Interop.Workbook workbook, Interop.Worksheet sheet, PendingInsert pending)
    {
        var registration = books[pending.WorkbookId];
        if (registration.Faulted || sheet.ProtectContents || SnapshotIdentity.Hash(ExcelReadback.Capture(workbook)) != SnapshotIdentity.Hash(pending.Before))
            throw new ExcelInsertRejectedException("The workbook changed after preview. Request a new preview.");
        registration.Backup ??= backups.Create(workbook.FullName);
        var mutationStarted = false;
        try
        {
            InSectionBatch(pending, () =>
            {
                if (SnapshotIdentity.Hash(ExcelReadback.Capture(workbook)) != SnapshotIdentity.Hash(pending.Before))
                    throw new ExcelInsertRejectedException("The workbook changed after preview. Request a new preview.");
                ValidateInsertTarget(sheet, pending.Section, pending.InsertNew, pending.Row, pending.Left, pending.Right);
                mutationStarted = true;
                if (pending.InsertNew) InsertRow(sheet, pending);
                if (pending.InsertNew && pending.Section.BlankHybridNewRows)
                {
                    RequireBlankManualRow(sheet, pending);
                }
                foreach (var group in Groups(pending.InsertNew ? pending.Section.FormulaColumns.Select(A1Formula.ColumnNumber) : []))
                {
                    Interop.Range? fill = null;
                    try { fill = ExcelReadback.Range(sheet, new(pending.Row - 1, group.Left, 2, group.Right - group.Left + 1)); fill.FillDown(); }
                    finally { ComLifetime.Release(fill); }
                }
                foreach (var group in Groups(pending.Values.Where(v => !pending.Section.HybridInputs || v.Value is not null).Select(v => v.Column)))
                {
                    var bounds = new CellRange(pending.Row, group.Left, 1, group.Right - group.Left + 1);
                    ValidateTarget(sheet, bounds, pending.Section.InsertMode == "tableRow" ? pending.Section.TableName : null, pending.Row);
                    var current = ExcelReadback.Read(sheet, bounds);
                    if (current.Formulas.Cast<string?>().Any(f => f is not null)) throw new InvalidOperationException("Excel filled a formula into a value column. The inserted row needs manual review.");
                    var values = new object?[1, bounds.Columns];
                    foreach (var field in pending.Values.Where(v => v.Column >= group.Left && v.Column <= group.Right))
                        values[0, field.Column - group.Left] = field.Value;
                    values = Normalize(values, bounds);
                    for (var c = 0; c < bounds.Columns; c++) if (values[0, c] is string text) values[0, c] = "'" + text;
                    Interop.Range? target = null;
                    try { target = ExcelReadback.Range(sheet, bounds); target.Value2 = values; }
                    finally { ComLifetime.Release(target); }
                }
                // Write only formula runs in this section's moved subtotal row. Unsupported
                // formulas are restored verbatim if native insertion adjusted their text.
                foreach (var group in Groups(Enumerable.Range(pending.Left, pending.Rewritten.Length).Where(c => pending.Rewritten[c - pending.Left] is not null)))
                {
                    var formulas = new object[1, group.Right - group.Left + 1];
                    for (var c = group.Left; c <= group.Right; c++) formulas[0, c - group.Left] = pending.Rewritten[c - pending.Left]!;
                    Interop.Range? subtotal = null;
                    try { subtotal = ExcelReadback.Range(sheet, new(pending.Row + 1, group.Left, 1, formulas.GetLength(1))); subtotal.Formula2 = formulas; }
                    finally { ComLifetime.Release(subtotal); }
                }
                application!.Calculate(); return true;
            });
            var bounds = new CellRange(pending.Row, pending.Left, 1, pending.Right - pending.Left + 1);
            var highlight = HighlightCore(pending.WorkbookId, sheet, bounds, 0x99FFFF);
            var after = ExcelReadback.Capture(workbook);
            var errors = CheckInsertion(pending, after);
            if (errors.Count != 0) throw new PreservationException(errors);
            return new(pending.WorkbookId, pending.Section.Id, sheet.Name, bounds.Address, pending.Warnings, highlight);
        }
        catch { if (mutationStarted) registration.Faulted = true; throw; }
    }

    private static void ValidateInsertTarget(Interop.Worksheet sheet, ExcelSectionDefinition section, bool insertNew, int row, int left, int right)
    {
        var wholeRow = insertNew && section.HybridInputs && section.InsertMode == "entireRow";
        var source = insertNew ? row - 1 : row;
        ValidateTarget(sheet, new(source, wholeRow ? 1 : left, 1, wholeRow ? 16384 : right - left + 1),
            section.InsertMode == "tableRow" ? section.TableName : null, source);
        // Inspect the insertion boundary too: whole-row shifts cannot split a merged,
        // table, array or spill structure intersecting the subtotal row.
        if (wholeRow) ValidateTarget(sheet, new(row, 1, 1, 16384));
    }

    private static void RequireBlankManualRow(Interop.Worksheet sheet, PendingInsert pending)
    {
        Interop.Range? created = null;
        try
        {
            var wholeRow = pending.Section.InsertMode == "entireRow";
            created = ExcelReadback.Range(sheet, new(pending.Row, wholeRow ? 1 : pending.Left, 1, wholeRow ? 16384 : pending.Right - pending.Left + 1));
            var raw = created.Value2;
            var occupied = raw is object[,] values ? values.Cast<object?>().Any(value => value is not null and not "") : raw is not null and not "";
            if (created.HasFormula is not false || occupied)
                throw new InvalidOperationException("Excel populated the new manual row unexpectedly. No supplied values were written; review the inserted row in Excel.");
        }
        finally { ComLifetime.Release(created); }
    }

    // A merged separator, foreign content or array/spill target is not a reusable data row.
    private static bool SafeHybridRow(Interop.Worksheet sheet, RangeData data, int row, int left, int right)
    {
        for (var c = data.Range.Column; c < data.Range.Column + data.Range.Columns; c++)
            if ((c < left || c > right) && (Cell(data, row, c) is not null and not "" || Formula(data, row, c) is not null)) return false;
        try { ValidateTarget(sheet, new(row, left, 1, right - left + 1), null, row); return true; }
        catch (InvalidOperationException) { return false; }
    }

    private T InSectionBatch<T>(PendingInsert pending, Func<T> operation) => InBatch(() =>
    {
        if (!pending.Section.BlankHybridNewRows) return operation();
        var extendList = application!.ExtendList;
        try { application.ExtendList = false; return operation(); }
        finally { application.ExtendList = extendList; }
    });

    private static void InsertRow(Interop.Worksheet sheet, PendingInsert pending)
    {
        if (pending.Section.InsertMode == "tableRow")
        {
            Interop.ListObjects? tables = null; Interop.ListObject? table = null;
            Interop.ListRows? rows = null; Interop.ListRow? added = null; Interop.Range? range = null;
            try
            {
                tables = sheet.ListObjects; table = tables[pending.Section.TableName]; rows = table.ListRows;
                added = rows.Add(); range = added.Range;
                if (range.Row != pending.Row) throw new InvalidOperationException("Excel inserted the table row at an unexpected position.");
            }
            finally { ComLifetime.Release(range); ComLifetime.Release(added); ComLifetime.Release(rows); ComLifetime.Release(table); ComLifetime.Release(tables); }
        }
        else
        {
            Interop.Range? target = null;
            try
            {
                target = ExcelReadback.Range(sheet, new(pending.Row, pending.Section.InsertMode == "entireRow" ? 1 : pending.Left, 1,
                    pending.Section.InsertMode == "entireRow" ? 16384 : pending.Right - pending.Left + 1));
                target.Insert(Interop.XlInsertShiftDirection.xlShiftDown, Interop.XlInsertFormatOrigin.xlFormatFromLeftOrAbove);
            }
            finally { ComLifetime.Release(target); }
        }
    }

    private static void ValidateTable(Interop.Worksheet sheet, ExcelSectionDefinition section, int row, int left, int right)
    {
        Interop.ListObjects? tables = null; Interop.ListObject? table = null; Interop.Range? header = null, totals = null, range = null;
        try
        {
            tables = sheet.ListObjects; table = tables[section.TableName]; header = table.HeaderRowRange; totals = table.TotalsRowRange; range = table.Range;
            var bounds = ExcelReadback.Bounds(range);
            if (!table.ShowTotals || totals is null || totals.Row != row || header.Row != section.HeaderRow || bounds.Column != left || bounds.Columns != right - left + 1)
                throw new InvalidOperationException("The table boundaries and totals must match the section profile.");
        }
        finally { ComLifetime.Release(range); ComLifetime.Release(totals); ComLifetime.Release(header); ComLifetime.Release(table); ComLifetime.Release(tables); }
    }

    private static long TemplateValidation(Interop.Worksheet sheet, int row, int left, int right)
    {
        Interop.Range? range = null, validation = null, overlap = null;
        Interop.Application? app = null;
        try
        {
            range = ExcelReadback.Range(sheet, new(row, left, 1, right - left + 1));
            try { validation = range.SpecialCells(Interop.XlCellType.xlCellTypeAllValidation); }
            catch (COMException e) when (e.HResult == unchecked((int)0x800A03EC)) { return 0; }
            // Single-row SpecialCells may return more than the supplied range; intersect explicitly.
            app = sheet.Application; overlap = app.Intersect(range, validation);
            return overlap is null ? 0 : Convert.ToInt64((object)overlap.CountLarge);
        }
        finally { ComLifetime.Release(overlap); ComLifetime.Release(app); ComLifetime.Release(validation); ComLifetime.Release(range); }
    }

    private static IEnumerable<(int Left, int Right)> Groups(IEnumerable<int> columns)
    {
        var sorted = columns.Distinct().Order().ToArray();
        for (var i = 0; i < sorted.Length; i++)
        {
            var first = sorted[i]; var last = first;
            while (i + 1 < sorted.Length && sorted[i + 1] == last + 1) last = sorted[++i];
            yield return (first, last);
        }
    }
    private static object? Cell(RangeData d, int row, int column) => d.Range.Contains(row, column) ? d.Values[row - d.Range.Row, column - d.Range.Column] : null;
    private static string? Formula(RangeData d, int row, int column) => d.Range.Contains(row, column) ? d.Formulas[row - d.Range.Row, column - d.Range.Column] : null;
    private static string NumberFormat(RangeData d, int row, int column) => d.Range.Contains(row, column) ? d.NumberFormats[row - d.Range.Row, column - d.Range.Column] : "General";
}
