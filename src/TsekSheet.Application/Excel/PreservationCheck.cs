namespace TsekSheet.Application.Excel;

public static class PreservationCheck
{
    public static IReadOnlyList<string> Compare(PreservationSnapshot before, PreservationSnapshot after,
        string targetSheet, CellRange target, object?[,] intended)
    {
        var errors = new List<string>();
        if (!before.Names.SequenceEqual(after.Names)) errors.Add("Defined names changed.");
        if (!before.Sheets.Select(s => s.Name).SequenceEqual(after.Sheets.Select(s => s.Name)))
            errors.Add("Worksheet identities changed.");
        foreach (var first in before.Sheets)
        {
            var second = after.Sheets.SingleOrDefault(s => s.Name == first.Name);
            if (second is null) continue;
            if (!first.MergedAreas.SequenceEqual(second.MergedAreas) || !first.Tables.SequenceEqual(second.Tables) ||
                first.ValidationCells != second.ValidationCells || first.ValidationAreas != second.ValidationAreas ||
                first.ConditionalFormats != second.ConditionalFormats) errors.Add($"{first.Name}: worksheet structures changed.");
            var a = first.Data;
            var b = second.Data;
            var top = Math.Min(a.Range.Row, b.Range.Row);
            var left = Math.Min(a.Range.Column, b.Range.Column);
            var bottom = Math.Max(a.Range.Row + a.Range.Rows, b.Range.Row + b.Range.Rows);
            var right = Math.Max(a.Range.Column + a.Range.Columns, b.Range.Column + b.Range.Columns);
            for (var row = top; row < bottom; row++)
                for (var col = left; col < right; col++)
                {
                    var av = Value(a, row, col);
                    var bv = Value(b, row, col);
                    var af = Formula(a, row, col);
                    var bf = Formula(b, row, col);
                    var isTarget = first.Name == targetSheet && target.Contains(row, col);
                    if (isTarget ? bf is not null || !Equals(bv, intended[row - target.Row, col - target.Column])
                        : af != bf || (af is null && !Equals(av, bv)))
                        errors.Add($"{first.Name}!R{row}C{col}: value/formula changed unexpectedly.");
                    // Formula cached values can recalculate; the formula itself must remain identical.
                    if (Format(a, row, col) != Format(b, row, col)) errors.Add($"{first.Name}!R{row}C{col}: number format changed.");
                }
        }
        return errors;
    }

    private static object? Value(RangeData d, int r, int c) => d.Range.Contains(r, c) ? d.Values[r - d.Range.Row, c - d.Range.Column] : null;
    private static string? Formula(RangeData d, int r, int c) => d.Range.Contains(r, c) ? d.Formulas[r - d.Range.Row, c - d.Range.Column] : null;
    private static string Format(RangeData d, int r, int c) => d.Range.Contains(r, c) ? d.NumberFormats[r - d.Range.Row, c - d.Range.Column] : "General";
}
