using TsekSheet.Application.Excel;

namespace TsekSheet.Excel;

public sealed partial class ExcelHost
{
    private static IReadOnlyList<string> CheckInsertion(PendingInsert plan, PreservationSnapshot after)
    {
        var errors = new List<string>();
        if (plan.Before.Date1904 != after.Date1904) errors.Add("Workbook date system changed during insertion.");
        var affected = plan.Section.SheetName;
        var left = plan.Section.InsertMode == "entireRow" ? 1 : plan.Left;
        var right = plan.Section.InsertMode == "entireRow" ? 16384 : plan.Right;
        string ShiftFormula(string formula, string formulaSheet) => A1Formula.TryShift(formula, formulaSheet, affected, plan.Row, left, right, out var shifted) ? shifted : formula;
        string ShiftAddress(string address, string sheet)
        {
            var qualified = $"='{sheet.Replace("'", "''")}'!{address}";
            var shifted = ShiftFormula(qualified, sheet);
            return shifted[(shifted.LastIndexOf('!') + 1)..];
        }
        if (!plan.Before.Sheets.Select(s => s.Name).SequenceEqual(after.Sheets.Select(s => s.Name))) errors.Add("Worksheet identities changed.");
        var names = plan.Before.Names.Select(n => n with { RefersTo = ShiftFormula(n.RefersTo, affected) }).ToArray();
        if (!names.SequenceEqual(after.Names)) errors.Add("Defined names differ from the expected insertion shift.");
        foreach (var first in plan.Before.Sheets)
        {
            var second = after.Sheets.SingleOrDefault(s => s.Name == first.Name);
            if (second is null) continue;
            var changedSheet = first.Name == affected;
            var merges = first.MergedAreas.Select(a => ShiftAddress(a, first.Name)).Order(StringComparer.Ordinal).ToArray();
            if (!merges.SequenceEqual(second.MergedAreas)) errors.Add($"{first.Name}: merged areas changed unexpectedly.");
            var tables = first.Tables.Select(t => t with { Address = changedSheet && t.Name == plan.Section.TableName && plan.Section.InsertMode == "tableRow"
                ? ExtendTable(t.Address) : ShiftAddress(t.Address, first.Name) }).ToArray();
            if (!tables.SequenceEqual(second.Tables)) errors.Add($"{first.Name}: table ranges changed unexpectedly.");
            if (second.ValidationCells != first.ValidationCells + (changedSheet ? plan.AddedValidation : 0)
                || second.ValidationAreas != first.ValidationAreas || second.ConditionalFormats != first.ConditionalFormats)
                errors.Add($"{first.Name}: validation/conditional-format counts changed unexpectedly.");
            var bottom = Math.Max(second.Data.Range.Row + second.Data.Range.Rows, first.Data.Range.Row + first.Data.Range.Rows + (changedSheet ? 1 : 0));
            var endColumn = Math.Max(second.Data.Range.Column + second.Data.Range.Columns, first.Data.Range.Column + first.Data.Range.Columns);
            for (var row = Math.Min(first.Data.Range.Row, second.Data.Range.Row); row < bottom; row++)
                for (var column = Math.Min(first.Data.Range.Column, second.Data.Range.Column); column < endColumn; column++)
                {
                    var inBand = changedSheet && column >= left && column <= right;
                    var inserted = inBand && row == plan.Row;
                    var sourceRow = inBand && row > plan.Row ? row - 1 : row;
                    object? expectedValue = inserted ? null : Cell(first.Data, sourceRow, column);
                    var expectedFormula = inserted ? null : Formula(first.Data, sourceRow, column);
                    var expectedFormat = NumberFormat(first.Data, inserted ? plan.Row - 1 : sourceRow, column);
                    if (expectedFormula is not null) expectedFormula = ShiftFormula(expectedFormula, first.Name);
                    if (inserted)
                    {
                        expectedValue = plan.Values.SingleOrDefault(v => v.Column == column)?.Value;
                        if (plan.Section.FormulaColumns.Any(c => A1Formula.ColumnNumber(c) == column))
                        {
                            var template = Formula(first.Data, plan.Row - 1, column)!;
                            template = ShiftFormula(template, first.Name);
                            expectedFormula = A1Formula.TryFillDown(template, out var filled) ? filled : template;
                        }
                    }
                    if (changedSheet && row == plan.Row + 1 && column >= plan.Left && column <= plan.Right && plan.Rewritten[column - plan.Left] is string subtotal)
                        expectedFormula = subtotal;
                    var actualFormula = Formula(second.Data, row, column);
                    if (actualFormula != expectedFormula || (expectedFormula is null && !Equals(expectedValue, Cell(second.Data, row, column))))
                        errors.Add($"{first.Name}!R{row}C{column}: unexpected value/formula after insertion.");
                    if (NumberFormat(second.Data, row, column) != expectedFormat) errors.Add($"{first.Name}!R{row}C{column}: unexpected number format after insertion.");
                }
        }
        return errors;
    }

    private static string ExtendTable(string address)
    {
        var index = address.LastIndexOf(':');
        var end = address[(index + 1)..];
        var digits = new string(end.SkipWhile(char.IsAsciiLetter).ToArray());
        return address[..(address.Length - digits.Length)] + (int.Parse(digits, System.Globalization.CultureInfo.InvariantCulture) + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
