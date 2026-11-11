using System.Globalization;
using System.Text.RegularExpressions;
using TsekSheet.Application.Documents;
using TsekSheet.Application.Excel;
using Interop = Microsoft.Office.Interop.Excel;

namespace TsekSheet.Excel;

public sealed partial class ExcelHost : IBindingSectionWriter
{
    public Task<bool> HasBindingAsync(string workbookId, WorkbookBinding binding) => queue.InvokeAsync(() =>
        WithBook(workbookId, workbook => HasBindingNames(ExcelReadback.ReadNames(workbook), binding)));

    private static bool HasBindingNames(IReadOnlyList<NamedReference> names, WorkbookBinding binding) => names.Any(n =>
    {
        // A partially installed registration must still take the bound route and fail there.
        var name = n.Name[(n.Name.LastIndexOf('!') + 1)..];
        return EqualName(name, binding.WorkbookIdentityName) || binding.Targets.Any(t =>
            EqualName(name, t.Type == "namedRange" ? t.Reference : null) || EqualName(name, t.StartAnchor) || EqualName(name, t.Subtotal?.Anchor));
    });

    public Task<SectionInsertPreview> PreviewBoundInsertAsync(string workbookId, WorkbookBinding binding, DocumentType documentType,
        string targetId, IReadOnlyList<InsertValue> values)
    {
        // Copy configuration before dispatch to the STA. The pending insert owns the validated definition.
        var configuration = System.Text.Json.JsonSerializer.Serialize(binding, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        binding = WorkbookBindings.Parse(configuration, documentType);
        var target = binding.Targets.SingleOrDefault(t => t.Id == targetId) ?? throw Reject(targetId, "Choose a configured workbook target.");
        if (target.Type == "unsupported") throw Reject(targetId, "This destination does not support insertion.");
        var fields = values.ToArray();
        if (target.RequiredFields.Any(key => !fields.Any(f => f.Key == key && f.Value is not null && f.Value is not "")))
            throw Reject(targetId, "Enter every field required for this destination.");
        var bindingRequest = new BoundInsert(binding, target);
        return queue.InvokeAsync(() => WithBoundSheet(workbookId, target, (workbook, sheet) =>
        {
            var before = ExcelReadback.Capture(workbook);
            var bounds = ResolveBinding(sheet, before, bindingRequest);
            var named = target.Type == "namedRange";
            var section = new ExcelSectionDefinition(target.Id, target.Label, target.Sheet, target.HeaderSignature!.HeaderRow,
                target.ColumnMap, target.ExpectedFormulaColumns, target.Subtotal!.Column, target.Subtotal.Text, named ? "entireRow" : "tableRow",
                named ? null : target.Reference, SheetMatch: target.HeaderSignature, FillBlankRowsFirst: true,
                HybridInputs: named, HybridFormulaColumns: documentType.Fields.Where(f => f.ValueType == "money" && !f.Required && target.ColumnMap.ContainsKey(f.Key))
                    .Select(f => target.ColumnMap[f.Key]).ToArray(), BlankHybridNewRows: named);
            return PreviewInsertCore(workbookId, workbook, sheet, section, fields, before, bounds, bindingRequest);
        }));
    }

    private sealed record BoundInsert(WorkbookBinding Binding, WorkbookTarget Target);

    private T WithBoundSheet<T>(string workbookId, WorkbookTarget target, Func<Interop.Workbook, Interop.Worksheet, T> operation)
    {
        var entered = false;
        try
        {
            return WithBook(workbookId, workbook =>
            {
                Interop.Sheets? sheets = null;
                try
                {
                    sheets = workbook.Worksheets;
                    for (var i = 1; i <= sheets.Count; i++)
                    {
                        Interop.Worksheet? sheet = null;
                        try
                        {
                            sheet = (Interop.Worksheet)(object)sheets[i];
                            if (sheet.Name != target.Sheet) continue;
                            workbook.Activate();
                            entered = true;
                            return operation(workbook, sheet);
                        }
                        finally { ComLifetime.Release(sheet); }
                    }
                    throw Reject(target.Id, $"The required worksheet '{target.Sheet}' is missing.");
                }
                finally { ComLifetime.Release(sheets); }
            });
        }
        catch (InvalidOperationException error) when (!entered && error is not ExcelInsertRejectedException)
        {
            throw Reject(target.Id, error.Message);
        }
    }

    private static (int First, int Subtotal) ResolveBinding(Interop.Worksheet sheet,
        PreservationSnapshot snapshot, BoundInsert request)
    {
        var (binding, target) = request;
        var identity = snapshot.Names.SingleOrDefault(n => EqualName(n.Name, binding.WorkbookIdentityName));
        if (identity?.RefersTo != $"=\"{binding.Id}\"" || !binding.RequiredSheets.All(s => snapshot.Sheets.Any(actual => actual.Name == s)))
            throw Reject(target.Id, "The workbook identity name or a required worksheet does not match the binding.");
        var data = snapshot.Sheets.Single(s => s.Name == target.Sheet).Data;
        var signature = target.HeaderSignature!;
        if (!ExcelLayoutMatching.Matches(data, signature)) throw Reject(target.Id, "The expected literal Excel headers changed.");

        var subtotalColumn = A1Formula.ColumnNumber(target.Subtotal!.Column);
        var columns = target.ColumnMap.Values.Concat(target.ExpectedFormulaColumns).Select(A1Formula.ColumnNumber).Append(subtotalColumn).ToArray();
        int first, last;
        if (target.Type == "namedRange")
        {
            var range = NamedBounds(snapshot, target, target.Reference!);
            var start = NamedBounds(snapshot, target, target.StartAnchor!);
            var subtotal = NamedBounds(snapshot, target, target.Subtotal.Anchor!);
            first = range.Row;
            last = range.Row + range.Rows - 1;
            if (range.Column != columns.Min() || range.Columns != columns.Max() - range.Column + 1
                || start != new CellRange(first, range.Column, 1, 1) || subtotal != new CellRange(last, subtotalColumn, 1, 1))
                throw Reject(target.Id, "The defined range does not match its independent start and subtotal anchors.");
            foreach (var other in binding.Targets.Where(t => t.Type == "namedRange" && t.Sheet == target.Sheet && t.Id != target.Id))
            {
                var otherSubtotal = NamedBounds(snapshot, other, other.Subtotal!.Anchor!);
                if (otherSubtotal.Row >= first && otherSubtotal.Row <= last)
                    throw Reject(target.Id, "The range overlaps another target's subtotal anchor.");
            }
        }
        else
        {
            if (!snapshot.Sheets.Single(s => s.Name == target.Sheet).Tables.Any(t => EqualName(t.Name, target.Reference)))
                throw Reject(target.Id, "The configured Excel Table is missing from the target worksheet.");
            (first, last) = TableBounds(sheet, target, columns.Min(), columns.Max());
        }

        if (first <= signature.HeaderRow || last <= first || last >= 1048576
            || ExcelLayoutMatching.Formula(data, last, subtotalColumn) is not null
            || ExcelLayoutMatching.Value(data, last, subtotalColumn) is not string text
            || !string.Equals(text.Trim(), target.Subtotal.Text, StringComparison.OrdinalIgnoreCase))
            throw Reject(target.Id, "The target needs data rows followed by its expected literal subsection subtotal.");
        return (first, last);
    }

    private static (int First, int Last) TableBounds(Interop.Worksheet sheet, WorkbookTarget target, int left, int right)
    {
        Interop.ListObjects? tables = null;
        Interop.ListObject? table = null;
        Interop.Range? header = null, body = null, totals = null;
        try
        {
            tables = sheet.ListObjects;
            table = tables[target.Reference];
            header = table.HeaderRowRange;
            body = table.DataBodyRange;
            totals = table.TotalsRowRange;
            if (!table.ShowHeaders || !table.ShowTotals || header is null || body is null || totals is null
                || header.Row != target.HeaderSignature!.HeaderRow)
                throw Reject(target.Id, "The Table needs its configured header, at least one data row and a totals row.");
            var bounds = ExcelReadback.Bounds(body);
            if (bounds.Column != left || bounds.Columns != right - left + 1 || bounds.Row + bounds.Rows != totals.Row)
                throw Reject(target.Id, "The Table body and totals do not match the configured columns.");
            return (bounds.Row, totals.Row);
        }
        finally
        {
            ComLifetime.Release(totals); ComLifetime.Release(body); ComLifetime.Release(header);
            ComLifetime.Release(table); ComLifetime.Release(tables);
        }
    }

    private static CellRange NamedBounds(PreservationSnapshot snapshot, WorkbookTarget target, string reference)
    {
        var name = snapshot.Names.SingleOrDefault(n => EqualName(n.Name, reference));
        if (name is null) throw Reject(target.Id, $"The workbook-level name '{reference}' is missing.");
        var quoted = "'" + target.Sheet.Replace("'", "''") + "'";
        var prefix = $"={quoted}!";
        if (!name.RefersTo.StartsWith(prefix, StringComparison.Ordinal)) prefix = $"={target.Sheet}!";
        if (!name.RefersTo.StartsWith(prefix, StringComparison.Ordinal)) throw Reject(target.Id, $"'{reference}' refers outside the target worksheet.");
        var match = Regex.Match(name.RefersTo[prefix.Length..], @"\A\$([A-Z]{1,3})\$([1-9][0-9]{0,6})(?::\$([A-Z]{1,3})\$([1-9][0-9]{0,6}))?\z", RegexOptions.CultureInvariant);
        if (!match.Success) throw Reject(target.Id, $"'{reference}' must refer to one absolute cell or rectangular range.");
        try
        {
            var row = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            var column = A1Formula.ColumnNumber(match.Groups[1].Value);
            var endRow = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : row;
            var endColumn = match.Groups[3].Success ? A1Formula.ColumnNumber(match.Groups[3].Value) : column;
            var range = new CellRange(row, column, endRow - row + 1, endColumn - column + 1);
            range.Validate();
            return range;
        }
        catch (Exception e) when (e is FormatException or ArgumentOutOfRangeException)
        {
            throw Reject(target.Id, $"'{reference}' has invalid Excel bounds.");
        }
    }

    private static bool EqualName(string? first, string? second) => second is not null && string.Equals(first, second, StringComparison.OrdinalIgnoreCase);
    private static ExcelInsertRejectedException Reject(string target, string reason) => new($"Target '{target}'. {reason}");
}
