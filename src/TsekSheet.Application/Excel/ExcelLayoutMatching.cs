using TsekSheet.Application.Workflows;

namespace TsekSheet.Application.Excel;

public static class ExcelLayoutMatching
{
    public static object? Value(RangeData data, int row, int column) => data.Range.Contains(row, column)
        ? data.Values[row - data.Range.Row, column - data.Range.Column] : null;
    public static string? Formula(RangeData data, int row, int column) => data.Range.Contains(row, column)
        ? data.Formulas[row - data.Range.Row, column - data.Range.Column] : null;
    public static bool Matches(RangeData data, SheetMatch match) => match.Anchors.All(a =>
        Formula(data, match.HeaderRow, A1Formula.ColumnNumber(a.Column)) is null
        && string.Equals(Convert.ToString(Value(data, match.HeaderRow, A1Formula.ColumnNumber(a.Column)))?.Trim(), a.Text.Trim(), StringComparison.OrdinalIgnoreCase));

    public static int Marker(RangeData data, RowMarker marker)
    {
        var rows = Enumerable.Range(data.Range.Row, data.Range.Rows).Where(r =>
            Formula(data, r, A1Formula.ColumnNumber(marker.Column)) is null &&
            string.Equals(Convert.ToString(Value(data, r, A1Formula.ColumnNumber(marker.Column)))?.Trim(), marker.Text.Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        if (rows.Length < marker.Occurrence) throw new ExcelInsertRejectedException("The selected sheet has no matching profile section marker.");
        return rows[marker.Occurrence - 1];
    }

    public static (int First, int Subtotal, IReadOnlyList<int> Protected) Resolve(RangeData data, ExcelSectionDefinition section)
    {
        if (section.SheetMatch is not null && !Matches(data, section.SheetMatch))
            throw new ExcelInsertRejectedException("The selected sheet does not match the profile headers.");
        int subtotal;
        if (section.SubtotalOccurrence > 0) subtotal = Marker(data, new(section.SubtotalColumn, section.SubtotalMarker, section.SubtotalOccurrence));
        else
        {
            var rows = Enumerable.Range(data.Range.Row, data.Range.Rows).Where(r => r > section.HeaderRow &&
                Equals(Value(data, r, A1Formula.ColumnNumber(section.SubtotalColumn)), section.SubtotalMarker)).ToArray();
            if (rows.Length != 1) throw new ExcelInsertRejectedException("The section needs one exact subtotal marker.");
            subtotal = rows[0];
        }
        var first = section.Start is null ? section.HeaderRow + 1 : section.Start.HeaderRowOffset is int offset
            ? section.HeaderRow + offset : Marker(data, section.Start.AfterRow!) + section.Start.Offset;
        var protectedRows = (section.ProtectedRows ?? []).Select(m => Marker(data, m)).ToArray();
        if (first > subtotal - 1 || protectedRows.Any(r => r >= first && r <= subtotal))
            throw new ExcelInsertRejectedException("The profile section overlaps a protected row or has no template data row.");
        return (first, subtotal, protectedRows);
    }
}
