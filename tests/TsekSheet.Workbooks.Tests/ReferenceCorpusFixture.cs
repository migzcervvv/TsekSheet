using ClosedXML.Excel;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace TsekSheet.Workbooks.Tests;

internal sealed class ReferenceCorpusFixture : IDisposable
{
    private readonly bool temporary;
    internal string Path { get; }
    internal ReferenceCorpusFixture(string feature = "plain", string? path = null)
    {
        temporary = path == null;
        Path = path ?? System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TsekSheet-reference-" + Guid.NewGuid().ToString("N") + ".xlsx");
        using (var wb = new XLWorkbook())
        {
            var sheet = wb.AddWorksheet("Ledger");
            var headers = new[] { "Vendor name", "Particulars", "RFP number", "Invoice number", "Amount", "Record date" };
            for (var c = 1; c <= headers.Length; c++) sheet.Cell(2, c).Value = headers[c - 1];
            sheet.Cell("A5").Value = "2025 PAYABLES (INTERNAL)"; sheet.Cell("A15").Value = "2025 PAYABLES (EXTERNAL)";
            Add(6, "Acme Trading Incorporated", "R1"); Add(7, feature == "vendor-group" ? "Acme Trading Incorporated" : "Other Tools Ltd", "R2");
            Add(16, "Cross Vendor Ltd", "R3"); Add(17, "Acme Trading Incorporated", "R4");
            sheet.Cell("A10").Value = "INTERNAL SUBTOTAL"; sheet.Cell("E10").FormulaA1 = "SUM(E6:E9)";
            sheet.Cell("A22").Value = "EXTERNAL SUBTOTAL"; sheet.Cell("E22").FormulaA1 = "SUM(E16:E21)";
            sheet.Cell("A25").Value = "Summary"; sheet.Cell("E25").FormulaA1 = "E10+E22";
            sheet.Cell("E26").FormulaA1 = "$E$10+E$22+$E6"; sheet.Cell("E27").FormulaA1 = "E6+E7";
            sheet.Cell("A28").Value = "Unrelated lower content";
            wb.AddWorksheet("Context").Cell("A1").Value = "Artificial constant-only context";
            switch (feature)
            {
                case "merge": sheet.Range("A1:F1").Merge().FirstCell().Value = "Artificial heading"; break;
                case "name": wb.DefinedNames.Add("Amounts", sheet.Range("E16:E21")); break;
                case "table": sheet.Cell("J1").Value = "Key"; sheet.Cell("K1").Value = "Value"; sheet.Cell("J2").Value = "Artificial"; sheet.Cell("K2").Value = 1; sheet.Range("J1:K2").CreateTable("ArtificialMetadata"); break;
                case "filter": sheet.Range("A2:F7").SetAutoFilter(); break;
                case "cross-sheet": wb.Worksheet("Context").Cell("A2").FormulaA1 = "Ledger!E22"; break;
            }
            using var output = new FileStream(Path, FileMode.CreateNew, FileAccess.ReadWrite);
            wb.SaveAs(output, new ClosedXML.Excel.SaveOptions { GenerateCalculationChain = false });
            void Add(int row, string vendor, string rfp)
            {
                sheet.Cell(row, 1).Value = vendor; sheet.Cell(row, 2).Value = "Artificial description";
                sheet.Cell(row, 3).Value = rfp; sheet.Cell(row, 4).Value = "INV-" + rfp; sheet.Cell(row, 5).Value = 100;
                sheet.Cell(row, 6).Value = new DateTime(2024, 1, 2); sheet.Cell(row, 7).Value = "PAID"; sheet.Cell(row, 8).Value = "OLD-VOUCHER";
                sheet.Row(row).Height = 22; var range = sheet.Range(row, 1, row, 8);
                range.Style.Font.FontName = "Arial"; range.Style.Font.FontColor = XLColor.Black;
                range.Style.Border.BottomBorder = XLBorderStyleValues.Thin; range.Style.Border.BottomBorderColor = XLColor.Black;
                range.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                sheet.Cell(row, 5).Style.NumberFormat.Format = "#,##0.00"; sheet.Cell(row, 5).Style.Fill.BackgroundColor = XLColor.LightBlue;
                sheet.Cell(row, 6).Style.NumberFormat.Format = "yyyy-mm-dd";
            }
        }
        if (feature is "shared" or "unknown")
        {
            using var zip = ZipFile.Open(Path, ZipArchiveMode.Update);
            if (feature == "unknown") { using var writer = new StreamWriter(zip.CreateEntry("xl/unknown.xml").Open()); writer.Write("<unknown/>"); }
            else
            {
                var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!; XDocument doc;
                using (var stream = entry.Open()) doc = XDocument.Load(stream);
                var formula = doc.Descendants(StructuralPreservationOracle.S + "f").First(); formula.SetAttributeValue("t", "shared"); formula.SetAttributeValue("si", "0");
                entry.Delete(); using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); doc.Save(output);
            }
        }
    }
    public void Dispose() { if (temporary) File.Delete(Path); }
    internal static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    // Read-only semantic cross-check. Excel may change package bytes, style IDs and shared-string representation.
    internal static SortedDictionary<string, string> Semantics(string path)
    {
        using var wb = new XLWorkbook(path); var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var sheet in wb.Worksheets)
        {
            result[$"sheet/{sheet.Position}"] = $"{sheet.Name}|{sheet.Visibility}";
            foreach (var cell in sheet.CellsUsed(XLCellsUsedOptions.All))
            {
                if (cell.Address.RowNumber > 2048 || cell.Address.ColumnNumber > 32) throw new InvalidDataException("Reference exceeds semantic scan bounds.");
                var style = cell.Style;
                var content = cell.HasFormula ? "formula:" + cell.FormulaA1 : cell.DataType + ":" + cell.Value.ToString(CultureInfo.InvariantCulture);
                // Exact known equivalent encodings only; arbitrary format normalization is forbidden.
                var format = style.NumberFormat.Format;
                if (format.Length == 0 && style.NumberFormat.NumberFormatId == 4) format = "#,##0.00";
                if (format == "yyyy\\-mm\\-dd") format = "yyyy-mm-dd";
                result[$"cell/{sheet.Name}/{cell.Address}"] = content + "|" + format + "|" + style.Font.FontName + "|" + style.Font.FontSize + "|" + style.Font.Bold + "|" + style.Font.Italic
                    + "|" + style.Font.FontColor + "|" + style.Fill.BackgroundColor + "|" + style.Border.BottomBorder + "|" + style.Border.BottomBorderColor + "|" + style.Alignment.Horizontal + "|" + style.Alignment.Vertical;
                result[$"height/{sheet.Name}/{cell.Address.RowNumber}"] = sheet.Row(cell.Address.RowNumber).Height.ToString(CultureInfo.InvariantCulture);
            }
            result[$"merges/{sheet.Name}"] = string.Join(";", sheet.MergedRanges.Select(r => r.RangeAddress.ToString()));
            result[$"tables/{sheet.Name}"] = string.Join(";", sheet.Tables.Select(t => t.Name + ":" + t.RangeAddress));
            result[$"filter/{sheet.Name}"] = sheet.AutoFilter.IsEnabled ? sheet.AutoFilter.Range.RangeAddress.ToString() ?? "" : "";
        }
        result["names"] = string.Join(";", wb.DefinedNames.Select(n => n.Name + ":" + n.RefersTo));
        return result;
    }
}
