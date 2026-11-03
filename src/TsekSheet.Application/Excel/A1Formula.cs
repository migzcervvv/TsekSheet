using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace TsekSheet.Application.Excel;

// A deliberately limited expression parser, not a text substitution over formulas.
// Names, structured/external/3-D references, unions and dynamic-array syntax fail closed.
public static class A1Formula
{
    private sealed record Cell(int Column, int Row, bool AbsoluteRow, int RowStart, int RowLength);
    private sealed record Reference(string? Sheet, Cell First, Cell? Last);
    private sealed record Token(string Kind, string Text);
    private sealed class Parser(string formula)
    {
        internal readonly List<Reference> References = [];
        private int position;
        private int depth;
        private Token token = new("end", "");
        internal void Parse()
        {
            if (!formula.StartsWith('=')) throw new FormatException();
            position = 1; Next(); Expression(0);
            if (token.Kind != "end") throw new FormatException();
        }
        private static int Precedence(string op) => op switch { "=" or "<>" or "<" or ">" or "<=" or ">=" => 1, "&" => 2, "+" or "-" => 3, "*" or "/" => 4, "^" => 5, _ => -1 };
        private void Expression(int minimum)
        {
            if (++depth > 128) throw new FormatException();
            if (token.Text is "+" or "-") { Next(); Expression(6); }
            else if (token.Kind is "ref" or "number" or "string") Next();
            else if (token.Kind == "identifier")
            {
                var name = token.Text; Next();
                if (name is "TRUE" or "FALSE" && token.Text != "(") { }
                else
                {
                    Require("(");
                    if (token.Text != ")") { Expression(0); while (token.Text == ",") { Next(); Expression(0); } }
                    Require(")");
                }
            }
            else if (token.Text == "(") { Next(); Expression(0); Require(")"); }
            else throw new FormatException();
            while (token.Text == "%") Next();
            while (Precedence(token.Text) >= minimum)
            {
                var precedence = Precedence(token.Text);
                var rightAssociative = token.Text == "^";
                Next(); Expression(precedence + (rightAssociative ? 0 : 1));
            }
            depth--;
        }
        private void Require(string text) { if (token.Text != text) throw new FormatException(); Next(); }
        private void Next()
        {
            while (position < formula.Length && char.IsWhiteSpace(formula[position])) position++;
            if (position == formula.Length) { token = new("end", ""); return; }
            var start = position;
            if (formula[position] == '"')
            {
                position++;
                while (position < formula.Length)
                {
                    if (formula[position++] != '"') continue;
                    if (position < formula.Length && formula[position] == '"') { position++; continue; }
                    token = new("string", formula[start..position]); return;
                }
                throw new FormatException();
            }
            string? sheet = null;
            var qualifier = Regex.Match(formula[position..], @"^(?:'((?:[^']|'')+)'|([A-Za-z_][A-Za-z0-9_.]*))!", RegexOptions.CultureInvariant);
            if (qualifier.Success)
            {
                sheet = qualifier.Groups[1].Success ? qualifier.Groups[1].Value.Replace("''", "'") : qualifier.Groups[2].Value;
                position += qualifier.Length;
            }
            var first = ReadCell();
            if (first is not null)
            {
                Cell? last = null;
                if (position < formula.Length && formula[position] == ':') { position++; last = ReadCell() ?? throw new FormatException(); }
                References.Add(new(sheet, first, last)); token = new("ref", formula[start..position]); return;
            }
            if (sheet is not null) throw new FormatException();
            var number = Regex.Match(formula[position..], @"^(?:[0-9]+(?:\.[0-9]*)?|\.[0-9]+)(?:[eE][+-]?[0-9]+)?", RegexOptions.CultureInvariant);
            if (number.Success) { position += number.Length; token = new("number", number.Value); return; }
            var identifier = Regex.Match(formula[position..], @"^[A-Za-z_][A-Za-z0-9_.]*", RegexOptions.CultureInvariant);
            if (identifier.Success) { position += identifier.Length; token = new("identifier", identifier.Value.ToUpperInvariant()); return; }
            var op = formula[position++].ToString();
            if (position < formula.Length && (op + formula[position]) is "<=" or ">=" or "<>") op += formula[position++];
            if (!new[] { "+", "-", "*", "/", "^", "&", "=", "<", ">", "<=", ">=", "<>", "%", "(", ")", "," }.Contains(op)) throw new FormatException();
            token = new("operator", op);
        }
        private Cell? ReadCell()
        {
            var match = Regex.Match(formula[position..], @"^(\$?[A-Za-z]{1,3})(\$?)([1-9][0-9]{0,6})(?![A-Za-z0-9_.(])", RegexOptions.CultureInvariant);
            if (!match.Success) return null;
            var column = ColumnNumber(match.Groups[1].Value.TrimStart('$'));
            var row = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (row > 1_048_576) throw new FormatException();
            var cell = new Cell(column, row, match.Groups[2].Length != 0, position + match.Groups[3].Index, match.Groups[3].Length);
            position += match.Length; return cell;
        }
    }

    public static int ColumnNumber(string text)
    {
        if (text.Length is < 1 or > 3 || text.Any(c => !char.IsAsciiLetter(c))) throw new FormatException("Invalid column letter.");
        var value = 0;
        foreach (var c in text.ToUpperInvariant()) value = checked(value * 26 + c - 'A' + 1);
        if (value > 16384) throw new FormatException("Column exceeds Excel limits.");
        return value;
    }

    public static bool TryExtendSubtotal(string formula, string sheet, int oldLastRow, int newLastRow, out string result) =>
        Rewrite(formula, (reference, cell) => reference.Last == cell && cell.Row == oldLastRow ? newLastRow : cell.Row,
            reference => reference.Sheet is null || reference.Sheet.Equals(sheet, StringComparison.OrdinalIgnoreCase), out result);

    public static bool TryShift(string formula, string formulaSheet, string affectedSheet, int row, int left, int right, out string result) =>
        Rewrite(formula, (reference, cell) => (reference.Sheet ?? formulaSheet).Equals(affectedSheet, StringComparison.OrdinalIgnoreCase)
            && cell.Column >= left && cell.Column <= right && cell.Row >= row ? cell.Row + 1 : cell.Row, _ => true, out result);

    public static bool TryFillDown(string formula, out string result) =>
        Rewrite(formula, (_, cell) => cell.AbsoluteRow ? cell.Row : cell.Row + 1, _ => true, out result);

    private static bool Rewrite(string formula, Func<Reference, Cell, int> transform, Func<Reference, bool> accept, out string result)
    {
        result = formula;
        if (formula.Length > 8192) return false;
        try
        {
            var parser = new Parser(formula); parser.Parse();
            if (parser.References.Any(r => !accept(r))) return false;
            var replacements = new List<(Cell Cell, int Row)>();
            foreach (var reference in parser.References)
            {
                replacements.Add((reference.First, transform(reference, reference.First)));
                if (reference.Last is not null) replacements.Add((reference.Last, transform(reference, reference.Last)));
            }
            var output = new StringBuilder(formula);
            foreach (var item in replacements.OrderByDescending(i => i.Cell.RowStart))
            {
                if (item.Row is < 1 or > 1_048_576) return false;
                output.Remove(item.Cell.RowStart, item.Cell.RowLength).Insert(item.Cell.RowStart, item.Row.ToString(CultureInfo.InvariantCulture));
            }
            result = output.ToString(); return true;
        }
        catch (Exception e) when (e is FormatException or OverflowException) { return false; }
    }
}
