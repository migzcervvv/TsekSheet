using System.Globalization;
using System.Text;

namespace TsekSheet.Workbooks;

// Deliberately independent of all mutation translators. No evaluation or cross-sheet syntax.
internal static class InspectionSharedFormula
{
    internal static bool TryTranslate(string text, int deltaRow, int deltaColumn, out string translated)
    {
        translated = ""; if (text.Length is 0 or > 1024) return false;
        try { translated = new Parser(text, deltaRow, deltaColumn).Run(); return true; }
        catch (FormatException) { return false; }
    }
    private sealed class Parser(string text, int dr, int dc)
    {
        private int at, nodes, depth; private readonly StringBuilder output = new();
        internal string Run() { Expression(); if (at != text.Length || output.Length > 1024) throw new FormatException(); return output.ToString(); }
        private void Expression() { Term(); while (Take('+') || Take('-')) Term(); }
        private void Term() { Factor(); while (Take('*') || Take('/')) Factor(); }
        private bool Take(char c) { if (at >= text.Length || text[at] != c) return false; at++; output.Append(c); return true; }
        private void Factor()
        {
            if (++nodes > 128 || ++depth > 32) throw new FormatException();
            if (Take('+') || Take('-')) Factor();
            else if (Take('(')) { Expression(); if (!Take(')')) throw new FormatException(); }
            else if (text.AsSpan(at).StartsWith("SUM("))
            {
                at += 4; output.Append("SUM("); Reference(); if (Take(':')) Reference();
                if (!Take(')')) throw new FormatException();
            }
            else if (at < text.Length && (text[at] == '$' || char.IsAsciiLetterUpper(text[at]))) Reference();
            else
            {
                var start = at; while (at < text.Length && char.IsAsciiDigit(text[at])) at++;
                if (at < text.Length && text[at] == '.') { at++; var decimals = at; while (at < text.Length && char.IsAsciiDigit(text[at])) at++; if (decimals == at) throw new FormatException(); }
                if (at == start || !decimal.TryParse(text.AsSpan(start, at - start), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out _)) throw new FormatException();
                output.Append(text.AsSpan(start, at - start));
            }
            depth--;
        }
        private void Reference()
        {
            var fixedColumn = at < text.Length && text[at] == '$'; if (fixedColumn) at++;
            int column = 0, letters = 0;
            while (at < text.Length && char.IsAsciiLetterUpper(text[at])) { if (++letters > 3) throw new FormatException(); column = column * 26 + text[at++] - 'A' + 1; }
            var fixedRow = at < text.Length && text[at] == '$'; if (fixedRow) at++;
            var start = at; while (at < text.Length && char.IsAsciiDigit(text[at])) at++;
            if (letters == 0 || column > 16384 || start == at || text[start] == '0' || !int.TryParse(text.AsSpan(start, at - start), out var row) || row is < 1 or > 1048576) throw new FormatException();
            if (!fixedColumn) column += dc; if (!fixedRow) row += dr;
            if (column is < 1 or > 16384 || row is < 1 or > 1048576) throw new FormatException();
            var label = ""; while (column > 0) { column--; label = (char)('A' + column % 26) + label; column /= 26; }
            output.Append(fixedColumn ? "$" : "").Append(label).Append(fixedRow ? "$" : "").Append(row);
        }
    }
}

internal sealed class InspectionSharedGroups
{
    private sealed class Group
    {
        internal int Count, MinRow = int.MaxValue, MaxRow, MinColumn = int.MaxValue, MaxColumn, AnchorRow, AnchorColumn;
        internal (int R1, int C1, int R2, int C2)? Range;
        internal string? Formula;
        internal bool Invalid;
    }
    private readonly Dictionary<int, Group> groups = [];
    internal void Add(int row, int column, string? index, string? range, string formula, bool truncated)
    {
        if (!int.TryParse(index, out var id) || id < 0) return;
        if (!groups.TryGetValue(id, out var group)) { if (groups.Count >= 1024) throw WorkbookInspection.Error("Shared-formula group limit is 1024."); groups.Add(id, group = new()); }
        group.Count++; group.MinRow = Math.Min(group.MinRow, row); group.MaxRow = Math.Max(group.MaxRow, row); group.MinColumn = Math.Min(group.MinColumn, column); group.MaxColumn = Math.Max(group.MaxColumn, column);
        if (formula.Length == 0) { if (range != null) group.Invalid = true; return; }
        if (group.Formula != null || truncated) group.Invalid = true;
        group.Formula = formula; group.AnchorRow = row; group.AnchorColumn = column;
        var parts = range?.Split(':');
        if (parts?.Length != 2 || !Address(parts[0], out var r1, out var c1) || !Address(parts[1], out var r2, out var c2) || r2 < r1 || c2 < c1 || row != r1 || column != c1) { group.Invalid = true; return; }
        group.Range = (r1, c1, r2, c2);
    }
    internal void Validate(Action check)
    {
        var values = groups.Values.ToArray();
        foreach (var g in values)
        {
            check();
            if (g.Range is not { } r || g.Formula == null || g.MinRow != r.R1 || g.MinColumn != r.C1 || g.MaxRow != r.R2 || g.MaxColumn != r.C2 || (long)(r.R2 - r.R1 + 1) * (r.C2 - r.C1 + 1) != g.Count) g.Invalid = true;
        }
        for (var i = 0; i < values.Length; i++) for (var j = i + 1; j < values.Length; j++)
        { check(); if (values[i].Range is { } a && values[j].Range is { } b && a.R1 <= b.R2 && b.R1 <= a.R2 && a.C1 <= b.C2 && b.C1 <= a.C2) values[i].Invalid = values[j].Invalid = true; }
    }
    internal (string? Formula, string Status) Resolve(string? index, int row, int column)
    {
        if (!int.TryParse(index, out var id) || !groups.TryGetValue(id, out var g) || g.Invalid || g.Formula == null || !InspectionSharedFormula.TryTranslate(g.Formula, row - g.AnchorRow, column - g.AnchorColumn, out var formula))
            return (null, "Unsupported shared formula: missing/inconsistent group or unsupported grammar.");
        return (formula, "Reconstructed shared formula; cached value is unverified.");
    }
    private static bool Address(string text, out int row, out int column)
    {
        row = 0; column = 0; var i = 0;
        while (i < text.Length && char.IsAsciiLetterUpper(text[i])) { if (i == 3) return false; column = column * 26 + text[i++] - 'A' + 1; }
        return column is >= 1 and <= 16384 && i < text.Length && text[i] != '0' && int.TryParse(text.AsSpan(i), out row) && row is >= 1 and <= 1048576;
    }
}
