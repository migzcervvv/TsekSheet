using System.Globalization;
using System.Text;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using S = DocumentFormat.OpenXml.Spreadsheet;
using C = DocumentFormat.OpenXml.CustomProperties;
using V = DocumentFormat.OpenXml.VariantTypes;

namespace WorkbookLibrary.Evaluation;

public sealed record FixtureSpec(string FileName, string MainSheet, int SummaryRow, string AlternateSheet, int AlternateSummary);

public static class FixtureFactory
{
    public static readonly FixtureSpec[] Specs =
    [
        new("reimbursement-synthetic.xlsx", "Expense Draft", 10, "Second Batch", 14),
        new("laboratory-synthetic.xlsx", "Lab Runs", 16, "Instrument B", 22)
    ];

    public static void Create(string path, FixtureSpec spec)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite);
        using var doc = SpreadsheetDocument.Create(output, SpreadsheetDocumentType.Workbook);
        doc.PackageProperties.Creator = "SyntheticFixtureGenerator";
        doc.PackageProperties.Title = "Artificial workbook integrity fixture";
        doc.PackageProperties.Created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var wb = doc.AddWorkbookPart();
        wb.Workbook = new S.Workbook(new S.WorkbookProperties { Date1904 = false });
        var styles = wb.AddNewPart<WorkbookStylesPart>();
        styles.Stylesheet = new S.Stylesheet(
            new S.NumberingFormats(new S.NumberingFormat { NumberFormatId = 164, FormatCode = "yyyy-mm-dd" }) { Count = 1 },
            new S.Fonts(new S.Font(new S.FontSize { Val = 11 }, new S.FontName { Val = "Calibri" }),
                new S.Font(new S.Bold(), new S.FontSize { Val = 11 }, new S.FontName { Val = "Calibri" })) { Count = 2 },
            new S.Fills(new S.Fill(new S.PatternFill { PatternType = S.PatternValues.None }),
                new S.Fill(new S.PatternFill { PatternType = S.PatternValues.Gray125 }),
                new S.Fill(new S.PatternFill(new S.ForegroundColor { Rgb = "FFDDEBF7" }, new S.BackgroundColor { Indexed = 64 }) { PatternType = S.PatternValues.Solid })) { Count = 3 },
            new S.Borders(new S.Border()) { Count = 1 },
            new S.CellStyleFormats(new S.CellFormat { FontId = 0, FillId = 0, BorderId = 0 }) { Count = 1 },
            new S.CellFormats(new S.CellFormat { FontId = 0, FillId = 0, BorderId = 0, FormatId = 0 },
                new S.CellFormat { FontId = 1, FillId = 2, BorderId = 0, FormatId = 0, ApplyFont = true, ApplyFill = true },
                new S.CellFormat { FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, NumberFormatId = 2, ApplyNumberFormat = true },
                new S.CellFormat { FontId = 0, FillId = 0, BorderId = 0, FormatId = 0, NumberFormatId = 164, ApplyNumberFormat = true }) { Count = 4 },
            new S.CellStyles(new S.CellStyle { Name = "Normal", FormatId = 0, BuiltinId = 0 }) { Count = 1 });
        var sheets = wb.Workbook.AppendChild(new S.Sheets());
        AddData(wb, sheets, spec.MainSheet, spec.SummaryRow, 1);
        AddData(wb, sheets, spec.AlternateSheet, spec.AlternateSummary, 2);
        var lookup = AddSheet(wb, sheets, "Lookup", S.SheetStateValues.Hidden);
        lookup.Worksheet = new S.Worksheet(new S.SheetData(Row(1, Text("A1", "Synthetic multiplier")), Row(2, Number("B2", 2))));
        var overview = AddSheet(wb, sheets, "Overview");
        overview.Worksheet = new S.Worksheet(new S.SheetData(
            Row(1, Text("A1", "Synthetic overview")),
            Row(2, Formula("B2", $"'{spec.MainSheet}'!C{spec.SummaryRow}")),
            Row(3, Formula("B3", $"'{spec.MainSheet}'!B5")),
            Row(4, Formula("B4", $"SUM('{spec.MainSheet}'!B5:B{spec.SummaryRow - 1})")),
            Row(5, Formula("B5", "SUM(DataValues)"))));
        wb.Workbook.AppendChild(new S.DefinedNames(
            new S.DefinedName { Name = "DataValues", Text = $"'{spec.MainSheet}'!$B$5:$B${spec.SummaryRow - 1}" },
            new S.DefinedName { Name = "SummaryCell", Text = $"'{spec.MainSheet}'!$C${spec.SummaryRow}" },
            new S.DefinedName { Name = "LocalInput", LocalSheetId = 0, Text = $"'{spec.MainSheet}'!$B$5" }));
        wb.Workbook.AppendChild(new S.CalculationProperties { CalculationId = 191029, FullCalculationOnLoad = true });
        var custom = doc.AddCustomFilePropertiesPart();
        custom.Properties = new C.Properties(new C.CustomDocumentProperty(new V.VTLPWSTR("artificial-only"))
            { Name = "FixtureClassification", PropertyId = 2, FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" });
        // Opaque extension probe: a high-level library must not silently discard unmodelled parts.
        var opaque = wb.AddCustomXmlPart(CustomXmlPartType.CustomXml);
        using (var stream = opaque.GetStream()) stream.Write(Encoding.UTF8.GetBytes("<probe xmlns=\"urn:tseksheet:synthetic\">preserve-opaque-payload</probe>"));
    }

    private static WorksheetPart AddSheet(WorkbookPart wb, S.Sheets sheets, string name, S.SheetStateValues? state = null)
    {
        var part = wb.AddNewPart<WorksheetPart>();
        var sheet = new S.Sheet { Id = wb.GetIdOfPart(part), SheetId = (uint)sheets.ChildElements.Count + 1, Name = name };
        if (state.HasValue) sheet.State = state.Value;
        sheets.Append(sheet);
        return part;
    }

    private static void AddData(WorkbookPart wb, S.Sheets sheets, string name, int summary, uint tableId)
    {
        var part = AddSheet(wb, sheets, name);
        var data = new S.SheetData();
        data.Append(Row(1, Text("A1", "Artificial data — no personal or financial records", 1)));
        data.Append(Row(4, Text("A4", "Label", 1), Text("B4", "Input", 1), Text("C4", "Calculated", 1), Text("D4", "Date", 1)));
        for (var r = 5; r < summary; r++)
            data.Append(new S.Row(Text($"A{r}", $"Sample-{r - 4}"), Number($"B{r}", r - 4, 2),
                Formula($"C{r}", $"B{r}*'Lookup'!$B$2", 2), Number($"D{r}", 43831 + r, 3)) { RowIndex = (uint)r, Height = 21, CustomHeight = true });
        data.Elements<S.Row>().Single(r => r.RowIndex!.Value == 5).Append(Formula("E5", $"'{name}'!$B$5+B$5+$B5", 2));
        data.Append(Row(summary, Text($"A{summary}", "Summary", 1), Formula($"B{summary}", $"SUM(B5:B{summary - 1})", 2), Formula($"C{summary}", $"SUM(C5:C{summary - 1})", 2)));
        data.Append(Row(summary + 3, Text($"A{summary + 3}", "Below-summary marker", 1)));
        data.Append(Row(summary + 5, Text($"A{summary + 5}", "Dependent content"), Formula($"B{summary + 5}", $"C{summary}+1", 2)));
        part.Worksheet = new S.Worksheet(new S.Columns(new S.Column { Min = 1, Max = 1, Width = 54, CustomWidth = true },
            new S.Column { Min = 2, Max = 4, Width = 18, CustomWidth = true }), data,
            new S.MergeCells(new S.MergeCell { Reference = "A1:D1" }, new S.MergeCell { Reference = $"A{summary + 3}:D{summary + 3}" }),
            new S.PageMargins { Left = .7, Right = .7, Top = .75, Bottom = .75, Header = .3, Footer = .3 });
        var table = part.AddNewPart<TableDefinitionPart>();
        var range = $"A4:D{summary - 1}";
        table.Table = new S.Table(new S.AutoFilter { Reference = range }, new S.TableColumns(
            new S.TableColumn { Id = 1, Name = "Label" }, new S.TableColumn { Id = 2, Name = "Input" },
            new S.TableColumn { Id = 3, Name = "Calculated" }, new S.TableColumn { Id = 4, Name = "Date" }) { Count = 4 },
            new S.TableStyleInfo { Name = "TableStyleMedium2", ShowRowStripes = true })
        { Id = tableId, Name = $"SyntheticTable{tableId}", DisplayName = $"SyntheticTable{tableId}", Reference = range, TotalsRowShown = false };
        part.Worksheet.AppendChild(new S.TableParts(new S.TablePart { Id = part.GetIdOfPart(table) }) { Count = 1 });
    }

    private static S.Row Row(int index, params S.Cell[] cells) => new(cells) { RowIndex = (uint)index };
    private static S.Cell Text(string address, string text, uint style = 0) => new(new S.InlineString(new S.Text(text))) { CellReference = address, DataType = S.CellValues.InlineString, StyleIndex = style };
    private static S.Cell Number(string address, double value, uint style = 0) => new(new S.CellValue(value.ToString(CultureInfo.InvariantCulture))) { CellReference = address, StyleIndex = style };
    private static S.Cell Formula(string address, string formula, uint style = 0) => new(new S.CellFormula(formula)) { CellReference = address, StyleIndex = style };
}
