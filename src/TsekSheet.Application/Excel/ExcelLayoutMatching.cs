using TsekSheet.Application.Workflows;
using System.Text.RegularExpressions;

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

    private static bool MarkerText(object? value, RowMarker marker)
    {
        var text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)?.Trim();
        return marker.Match switch
        {
            "exact" => string.Equals(text, marker.Text.Trim(), StringComparison.OrdinalIgnoreCase),
            "year-category" => value is string && text is not null && Regex.IsMatch(text,
                @"^[0-9]{4}\s+PAYABLES\s*\(\s*" + Regex.Escape(marker.Text.Trim()).Replace(@"\ ", @"\s+") + @"\s*\)$",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)),
            "nonempty-text" => value is string && !string.IsNullOrWhiteSpace(text),
            _ => throw new ExcelInsertRejectedException("Unsupported section heading match contract.")
        };
    }

    public static int Marker(RangeData data, RowMarker marker)
    {
        var after = marker.After is null ? 0 : Marker(data, marker.After);
        var before = marker.Before is null ? 1048577 : Marker(data, marker.Before);
        var rows = Enumerable.Range(data.Range.Row, data.Range.Rows).Where(r => r > after && r < before &&
            Formula(data, r, A1Formula.ColumnNumber(marker.Column)) is null &&
            MarkerText(Value(data, r, A1Formula.ColumnNumber(marker.Column)), marker)).ToArray();
        if (marker.ExpectedCount is int count && rows.Length != count) throw new ExcelInsertRejectedException($"Section marker '{marker.Text}' is ambiguous. Verify its heading scope and expected count ({count}) in Excel.");
        if (rows.Length < marker.Occurrence) throw new ExcelInsertRejectedException($"No matching '{marker.Text}' section marker. Verify its column and heading text in Excel; no alternate destination was selected.");
        return rows[marker.Occurrence - 1];
    }

    public static (int First, int Subtotal, IReadOnlyList<int> Protected) Resolve(RangeData data, ExcelSectionDefinition section)
    {
        if (section.SheetMatch is not null && !Matches(data, section.SheetMatch))
            throw new ExcelInsertRejectedException("The selected sheet does not match the profile headers.");
        foreach (var marker in section.StructureMarkers ?? []) _ = Marker(data, marker);
        int subtotal;
        if (section.SubtotalLocator is not null) subtotal = Marker(data, section.SubtotalLocator);
        else if (section.SubtotalOccurrence > 0) subtotal = Marker(data, new(section.SubtotalColumn, section.SubtotalMarker, section.SubtotalOccurrence));
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
        if (first <= section.HeaderRow || first > subtotal - 1 || protectedRows.Any(r => r >= first && r <= subtotal))
            throw new ExcelInsertRejectedException("The profile section overlaps a protected row or has no template data row.");
        return (first, subtotal, protectedRows);
    }
}
