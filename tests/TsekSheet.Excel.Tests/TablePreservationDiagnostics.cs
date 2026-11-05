using System.Collections;
using System.Reflection;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TsekSheet.Application.Excel;

namespace TsekSheet.Excel.Tests;

// Test-only diagnostic. The pending plan contains plain DTOs, not RCWs. Reading it
// after preview avoids an additional success-path Excel capture that could mask a
// timing-sensitive failure. No assertion or production expectation is changed.
internal sealed class TablePreservationDiagnostics(object pending)
{
    internal static TablePreservationDiagnostics? Remember(ExcelHost host, string token)
    {
        try
        {
            var plans = (IDictionary)typeof(ExcelHost).GetField("insertPlans", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(host)!;
            return new(plans[token] ?? throw new InvalidOperationException("Preview plan unavailable."));
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Managed table diagnostic context unavailable: {error}");
            return null;
        }
    }

    private T Read<T>(string property) => (T)pending.GetType().GetProperty(property)!.GetValue(pending)!;

    internal async Task<string> WriteAsync(ExcelHost host, SectionInsertPreview preview, int iteration,
        IReadOnlyList<string> differences, TestContext context)
    {
        var before = Read<PreservationSnapshot>("Before");
        var after = await host.CaptureAsync(preview.WorkbookId);
        var section = Read<ExcelSectionDefinition>("Section");
        var left = Read<int>("Left");
        var rewritten = Read<string?[]>("Rewritten");
        var target = before.Sheets.Single(s => s.Name == preview.Sheet);
        var table = target.Tables.Single(t => t.Name == section.TableName);
        // ListRows.Add extends this fixture's table by exactly one data row.
        var lastDigits = new string(table.Address[(table.Address.LastIndexOf(':') + 1)..].SkipWhile(char.IsAsciiLetter).ToArray());
        var expectedTableAddress = table.Address[..^lastDigits.Length] + (int.Parse(lastDigits, System.Globalization.CultureInfo.InvariantCulture) + 1);
        var record = new
        {
            Iteration = iteration, preview.Sheet, preview.Range, preview.InsertRow,
            Differences = differences,
            TableRangeDifferences = target.Tables.Select(expected => new
            {
                Worksheet = target.Name,
                Range = expected.Name == section.TableName ? expectedTableAddress : expected.Address,
                Property = $"ListObject[{expected.Name}].Range",
                Expected = expected.Name == section.TableName ? expectedTableAddress : expected.Address,
                Actual = after.Sheets.Single(s => s.Name == target.Name).Tables.SingleOrDefault(t => t.Name == expected.Name)?.Address
            }).Where(d => d.Expected != d.Actual),
            Expected = new
            {
                TableName = table.Name, TableRange = expectedTableAddress,
                Inputs = preview.Values,
                FormulaColumns = section.FormulaColumns,
                SubtotalFormulas = rewritten.Select((formula, index) => new
                {
                    Range = new CellRange(preview.InsertRow + 1, left + index, 1, 1).Address,
                    Formula = formula
                }).Where(c => c.Formula is not null)
            },
            Before = Flatten(before), After = Flatten(after)
        };
        var directory = Environment.GetEnvironmentVariable("TSEKSHEET_LIFECYCLE_OUTPUT") ?? context.ResultsDirectory!;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"table-preservation-{iteration}-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true }));
        context.AddResultFile(path);
        Console.Error.WriteLine($"Table preservation evidence: {path}");
        return path;
    }

    private static object Flatten(PreservationSnapshot snapshot) => new
    {
        snapshot.Date1904, snapshot.Names,
        Sheets = snapshot.Sheets.Select(sheet => new
        {
            sheet.Name, sheet.Tables, sheet.MergedAreas, sheet.ValidationCells,
            sheet.ValidationAreas, sheet.ConditionalFormats,
            UsedRange = sheet.Data.Range.Address,
            Cells = Enumerable.Range(0, sheet.Data.Range.Rows).SelectMany(row =>
                Enumerable.Range(0, sheet.Data.Range.Columns).Select(column => new
                {
                    Range = new CellRange(sheet.Data.Range.Row + row, sheet.Data.Range.Column + column, 1, 1).Address,
                    Value = sheet.Data.Values[row, column], Formula = sheet.Data.Formulas[row, column],
                    NumberFormat = sheet.Data.NumberFormats[row, column]
                }))
        })
    };
}
