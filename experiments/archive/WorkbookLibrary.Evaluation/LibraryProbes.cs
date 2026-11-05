using ClosedXML.Excel;
using DocumentFormat.OpenXml.Packaging;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using S = DocumentFormat.OpenXml.Spreadsheet;

namespace WorkbookLibrary.Evaluation;

public static class LibraryProbes
{
    public static readonly string[] Libraries = ["ClosedXML", "NPOI", "OpenXmlSdk"];
    public static readonly string[] Operations = ["roundtrip", "edit", "deep-copy", "copy-sheet", "create-sheet", "replace-sheet", "insert-once", "insert-repeated", "insert-declared-ranges"];

    public static byte[] Run(string library, string operation, byte[] input, FixtureSpec spec)
    {
        if (operation == "insert-declared-ranges" && library != "ClosedXML") throw new NotSupportedException("Scoped range-repair prototype implemented only for ClosedXML.");
        if (library == "OpenXmlSdk" && operation is "copy-sheet" or "replace-sheet")
            throw new NotSupportedException("SDK has no high-level sheet copy/replacement/reference service; custom relationship surgery was not implemented.");
        var iterations = operation is "insert-repeated" or "insert-declared-ranges" ? 3 : 1;
        var current = input;
        for (var i = 0; i < iterations; i++)
            current = library switch
            {
                "ClosedXML" => Closed(current, operation, spec with { SummaryRow = spec.SummaryRow + i }),
                "NPOI" => Npoi(current, operation, spec with { SummaryRow = spec.SummaryRow + i }),
                "OpenXmlSdk" => Sdk(current, operation, spec with { SummaryRow = spec.SummaryRow + i }),
                _ => throw new ArgumentException("Unknown library.")
            };
        // Reopen with the candidate library as well as the independent verifier.
        using var stream = new MemoryStream(current);
        if (library == "ClosedXML") { using var reopened = new XLWorkbook(stream); _ = reopened.Worksheets.Count; }
        if (library == "NPOI") { using var reopened = new XSSFWorkbook(stream); _ = reopened.NumberOfSheets; }
        if (library == "OpenXmlSdk") { using var reopened = SpreadsheetDocument.Open(stream, false); _ = reopened.WorkbookPart!.WorksheetParts.Count(); }
        return current;
    }

    private static byte[] Closed(byte[] input, string operation, FixtureSpec spec)
    {
        using var sourceStream = new MemoryStream(input);
        using var workbook = new XLWorkbook(sourceStream);
        var sheet = workbook.Worksheet(spec.MainSheet);
        switch (operation)
        {
            case "edit": sheet.Cell("A5").Value = "Edited synthetic value"; break;
            case "deep-copy":
                using (var cloneStream = new MemoryStream(input))
                using (var clone = new XLWorkbook(cloneStream))
                {
                    clone.Worksheet(spec.MainSheet).Cell("A5").Value = "Edited synthetic value";
                    clone.Worksheet(spec.MainSheet).Cell("B5").Style.Fill.BackgroundColor = XLColor.Red;
                    if (sheet.Cell("A5").GetString() != "Sample-1" || sheet.Cell("B5").Style.Fill.BackgroundColor == XLColor.Red) throw new InvalidOperationException("Deep-copy isolation failed.");
                    using var clonedOutput = new MemoryStream(); clone.SaveAs(clonedOutput); return clonedOutput.ToArray();
                }
            case "copy-sheet":
                var copy = sheet.CopyTo("Working Copy"); copy.Cell("A5").Value = "Edited synthetic value"; break;
            case "create-sheet": workbook.AddWorksheet("New Sheet").Cell("A1").Value = "Fresh synthetic sheet"; break;
            case "replace-sheet":
                var replacement = sheet.CopyTo("Replacement Candidate"); replacement.Cell("A5").Value = "Edited synthetic value";
                var position = sheet.Position; sheet.Delete(); replacement.Name = spec.MainSheet; replacement.Position = position; break;
            case "insert-once": case "insert-repeated": case "insert-declared-ranges":
                var r = spec.SummaryRow;
                sheet.Row(r).InsertRowsAbove(1);
                sheet.Range(r - 1, 1, r - 1, 4).CopyTo(sheet.Cell(r, 1));
                sheet.Row(r).Height = sheet.Row(r - 1).Height;
                sheet.Cell(r, 1).Value = "Inserted synthetic row";
                sheet.Cell(r, 2).Value = 7;
                sheet.Cell(r, 4).Value = 43831 + r;
                if (operation == "insert-declared-ranges")
                {
                    // Fixture-scoped policy experiment, not a generic template engine.
                    sheet.Cell(r + 1, 2).FormulaA1 = $"SUM(B5:B{r})";
                    sheet.Cell(r + 1, 3).FormulaA1 = $"SUM(C5:C{r})";
                    sheet.Tables.Single().Resize(sheet.Range(4, 1, r, 4));
                    workbook.DefinedNames.Delete("DataValues");
                    workbook.DefinedNames.Add("DataValues", sheet.Range(5, 2, r, 2));
                    workbook.Worksheet("Overview").Cell("B4").FormulaA1 = $"SUM('{spec.MainSheet}'!B5:B{r})";
                }
                break;
        }
        using var output = new MemoryStream(); workbook.SaveAs(output); return output.ToArray();
    }

    private static byte[] Npoi(byte[] input, string operation, FixtureSpec spec)
    {
        using var source = new MemoryStream(input);
        using var workbook = new XSSFWorkbook(source);
        var sheet = (XSSFSheet)workbook.GetSheet(spec.MainSheet);
        switch (operation)
        {
            case "edit": sheet.GetRow(4).GetCell(0).SetCellValue("Edited synthetic value"); break;
            case "deep-copy":
                using (var clonedSource = new MemoryStream(input))
                using (var clone = new XSSFWorkbook(clonedSource))
                {
                    var cell = clone.GetSheet(spec.MainSheet).GetRow(4).GetCell(1);
                    var style = clone.CreateCellStyle(); style.CloneStyleFrom(cell.CellStyle);
                    style.FillForegroundColor = IndexedColors.Red.Index; style.FillPattern = FillPattern.SolidForeground; cell.CellStyle = style;
                    clone.GetSheet(spec.MainSheet).GetRow(4).GetCell(0).SetCellValue("Edited synthetic value");
                    if (sheet.GetRow(4).GetCell(0).StringCellValue != "Sample-1" || sheet.GetRow(4).GetCell(1).CellStyle.FillForegroundColor == IndexedColors.Red.Index) throw new InvalidOperationException("Deep-copy isolation failed.");
                    using var clonedOutput = new MemoryStream(); clone.Write(clonedOutput, true); return clonedOutput.ToArray();
                }
            case "copy-sheet":
                workbook.CloneSheet(workbook.GetSheetIndex(sheet), "Working Copy").GetRow(4).GetCell(0).SetCellValue("Edited synthetic value"); break;
            case "create-sheet": workbook.CreateSheet("New Sheet").CreateRow(0).CreateCell(0).SetCellValue("Fresh synthetic sheet"); break;
            case "replace-sheet":
                var copy = workbook.CloneSheet(workbook.GetSheetIndex(sheet), "Replacement Candidate"); copy.GetRow(4).GetCell(0).SetCellValue("Edited synthetic value");
                var index = workbook.GetSheetIndex(sheet); workbook.RemoveSheetAt(index); workbook.SetSheetName(workbook.GetSheetIndex(copy), spec.MainSheet); workbook.SetSheetOrder(spec.MainSheet, index); break;
            case "insert-once": case "insert-repeated":
                var row = spec.SummaryRow - 1;
                sheet.ShiftRows(row, sheet.LastRowNum, 1, true, false);
                sheet.CopyRows(row - 1, row - 1, row, new CellCopyPolicy());
                sheet.GetRow(row).GetCell(0).SetCellValue("Inserted synthetic row");
                sheet.GetRow(row).GetCell(1).SetCellValue(7);
                sheet.GetRow(row).GetCell(3).SetCellValue(43831 + spec.SummaryRow);
                break;
        }
        using var output = new MemoryStream(); workbook.Write(output, true); return output.ToArray();
    }

    private static byte[] Sdk(byte[] input, string operation, FixtureSpec spec)
    {
        var originalHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        using var memory = new MemoryStream(); memory.Write(input); memory.Position = 0;
        using (var doc = SpreadsheetDocument.Open(memory, true))
        {
            var wb = doc.WorkbookPart!;
            var sheet = wb.Workbook!.Sheets!.Elements<S.Sheet>().Single(s => s.Name!.Value == spec.MainSheet);
            var part = (WorksheetPart)wb.GetPartById(sheet.Id!);
            if (operation is "edit" or "deep-copy")
            {
                var cell = part.Worksheet!.Descendants<S.Cell>().Single(c => c.CellReference!.Value == "A5");
                cell.RemoveAllChildren(); cell.DataType = S.CellValues.InlineString; cell.Append(new S.InlineString(new S.Text("Edited synthetic value")));
                // Independent editable byte buffer. The caller's source bytes remain immutable.
            }
            if (operation == "create-sheet")
            {
                var newPart = wb.AddNewPart<WorksheetPart>();
                newPart.Worksheet = new S.Worksheet(new S.SheetData(new S.Row(new S.Cell(new S.InlineString(new S.Text("Fresh synthetic sheet"))) { CellReference = "A1", DataType = S.CellValues.InlineString }) { RowIndex = 1 }));
                wb.Workbook.Sheets!.Append(new S.Sheet { Name = "New Sheet", Id = wb.GetIdOfPart(newPart), SheetId = wb.Workbook.Sheets.Elements<S.Sheet>().Max(s => s.SheetId!.Value) + 1 });
            }
            if (operation.StartsWith("insert-"))
            {
                // Deliberate negative control: SDK DOM edits do not supply Excel reference mechanics.
                // Move addresses only, to prove schema-valid output can still be semantically wrong.
                foreach (var row in part.Worksheet!.Descendants<S.Row>().Where(r => r.RowIndex!.Value >= spec.SummaryRow).OrderByDescending(r => r.RowIndex!.Value))
                {
                    row.RowIndex = row.RowIndex!.Value + 1;
                    foreach (var cell in row.Elements<S.Cell>()) cell.CellReference = PackageInspector.ShiftAddress(cell.CellReference!, spec.SummaryRow, 1);
                }
            }
            part.Worksheet!.Save(); wb.Workbook.Save();
        }
        if (originalHash != Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input))) throw new InvalidOperationException("Byte-copy isolation failed.");
        return memory.ToArray();
    }
}
