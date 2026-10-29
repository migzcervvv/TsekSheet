using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

public sealed record StructuralValue(int Column, string Kind, string Value);
public sealed record StructuralRecord(IReadOnlyList<StructuralValue> Fields);
public sealed record StructuralInsertionManifest(string Worksheet, int Boundary, int TemplateRow, int FirstDataRow,
    int SubtotalRow, IReadOnlyList<StructuralRecord> Records)
{
    public int Count => Records.Count;
}
public sealed record StructuralDifference(string Category, string Location, string Reason);
public sealed record StructuralPreservationReport(bool Accepted, string Worksheet, int Boundary, int RowShift,
    IReadOnlyList<string> AllowedChanges, IReadOnlyList<StructuralDifference> Differences);

// Read-only OOXML oracle. No ClosedXML, editor invocation, or editor reference translator.
public static class StructuralPreservationOracle
{
    internal static readonly XNamespace S = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly Regex Address = new(@"^(\$?[A-Z]{1,3})(\$?)([1-9][0-9]{0,6})$", RegexOptions.CultureInvariant);
    private static readonly Regex Grammar = new(@"^(SUM\(\$?[A-Z]{1,3}\$?[1-9][0-9]*:\$?[A-Z]{1,3}\$?[1-9][0-9]*\)|\$?[A-Z]{1,3}\$?[1-9][0-9]*(\+\$?[A-Z]{1,3}\$?[1-9][0-9]*)*)$", RegexOptions.CultureInvariant);

    public static StructuralPreservationReport Evaluate(byte[] before, StructuralInsertionManifest manifest, byte[] candidate,
        CancellationToken token = default)
    {
        var differences = new List<StructuralDifference>();
        void Difference(string category, string location, string reason) => differences.Add(new(category, location, reason));
        try
        {
            // Existing bounded package/schema/security policy remains mandatory for both inputs.
            foreach (var bytes in new[] { before, candidate })
                if (PackageGuard.Inspect(bytes, new(), token).Findings.Any(f => f.Code is "ADVANCED_CONTENT" or "UNKNOWN_PARTS" or "OPAQUE_PARTS"))
                    throw PackageGuard.Reject("STRUCTURE_UNSUPPORTED", "Advanced, unknown or opaque package content cannot be structurally verified.");
            var original = Read(before, token); var after = Read(candidate, token);
            var path = InspectEnvelope(original, manifest);
            if (!original.Keys.Order().SequenceEqual(after.Keys.Order())) Difference("package", "entries", "Package parts were added or removed.");
            foreach (var (name, content) in original)
            {
                token.ThrowIfCancellationRequested();
                if (!after.TryGetValue(name, out var actual)) continue;
                if (name == path) CompareSheet(Xml(content), Xml(actual), manifest, differences, token);
                else if (name == "xl/workbook.xml")
                {
                    var expected = Xml(content); var calc = expected.Root!.Element(S + "calcPr");
                    if (calc == null) { calc = new(S + "calcPr"); expected.Root.Add(calc); }
                    calc.SetAttributeValue("fullCalcOnLoad", "1"); calc.SetAttributeValue("forceFullCalc", "1");
                    var actualWorkbook = Xml(actual);
                    if (Canonical(expected.Root!) != Canonical(actualWorkbook.Root!)) Difference(actualWorkbook.Descendants(S + "definedName").Any() ? "defined-names" : "references", name, "Unexpected workbook/name/calculation-property change.");
                }
                else if (!content.AsSpan().SequenceEqual(actual))
                    Difference(name.EndsWith(".rels") ? "relationships" : name.Contains("styles") ? "formatting" : "package", name, "An unaffected part changed.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        { Difference("unsupported-or-invalid", "package/envelope", ex is WorkbookException ? ex.Message : "Invalid or unverifiable OOXML package/manifest."); }
        return new(differences.Count == 0, manifest.Worksheet, manifest.Boundary, manifest.Count,
            ["Shift every existing row at/after boundary by approved count", "Insert only mapped literal values using uniform record styles and height",
             "Translate bounded same-sheet A1 references, extend enclosing SUM at subtotal edge", "Remove formula caches; request full calculation on load"], differences.AsReadOnly());
    }

    internal static Dictionary<string, byte[]> Read(byte[] bytes, CancellationToken token)
    {
        if (bytes.Length > 8 * 1024 * 1024) throw PackageGuard.Reject("STRUCTURAL_LIMIT", "Structural experiments require packages at most 8 MiB.");
        using var zip = new ZipArchive(new MemoryStream(bytes, false), ZipArchiveMode.Read);
        if (zip.Entries.Count > 128 || zip.Entries.Sum(e => e.Length) > 16 * 1024 * 1024) throw PackageGuard.Reject("STRUCTURAL_LIMIT", "Structural package exceeds entry/expanded bounds.");
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested(); using var stream = entry.Open(); using var output = new MemoryStream(); stream.CopyTo(output);
            if (!parts.TryAdd(entry.FullName, output.ToArray())) throw PackageGuard.Reject("INVALID_PACKAGE", "Duplicate package entry.");
        }
        return parts;
    }
    internal static XDocument Xml(byte[] bytes)
    {
        using var reader = XmlReader.Create(new MemoryStream(bytes, false), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16 * 1024 * 1024 });
        return XDocument.Load(reader);
    }
    internal static string InspectEnvelope(Dictionary<string, byte[]> parts, StructuralInsertionManifest m)
    {
        void Require(bool condition, string reason) { if (!condition) throw PackageGuard.Reject("STRUCTURE_UNSUPPORTED", reason); }
        Require(m.Count is >= 1 and <= 20 && m.FirstDataRow > 0 && m.FirstDataRow <= m.TemplateRow && m.TemplateRow < m.SubtotalRow
            && m.Boundary > m.FirstDataRow && m.Boundary <= m.SubtotalRow && m.SubtotalRow <= 2000, "Invalid bounded insertion or template region.");
        Require(parts.Keys.All(p => p is "[Content_Types].xml" or "_rels/.rels" or "xl/workbook.xml" or "xl/_rels/workbook.xml.rels" or "xl/styles.xml" or "xl/sharedStrings.xml"
            || Regex.IsMatch(p, @"^xl/worksheets/sheet[0-9]+\.xml$") || p.StartsWith("xl/theme/", StringComparison.Ordinal) || p.StartsWith("docProps/", StringComparison.Ordinal)
            || p.StartsWith("package/services/metadata/core-properties/", StringComparison.Ordinal)), "Unsupported package part (including tables, names dependencies, drawings, comments, calc chains or custom XML).");
        var wb = Xml(parts["xl/workbook.xml"]);
        Require(!wb.Descendants(S + "definedName").Any(), "All workbook/local defined names are rejected.");
        Require(wb.Root!.Element(S + "workbookPr")?.Attribute("date1904")?.Value is not ("1" or "true"), "1904 date system is outside the structural envelope.");
        var sheet = wb.Descendants(S + "sheet").Single(s => (string?)s.Attribute("name") == m.Worksheet);
        Require(sheet.Attribute("state")?.Value is null or "visible", "Select a visible worksheet.");
        var rels = Xml(parts["xl/_rels/workbook.xml.rels"]);
        var target = (string)rels.Root!.Elements().Single(e => (string?)e.Attribute("Id") == (string?)sheet.Attribute(R + "id")).Attribute("Target")!;
        var path = new Uri(new Uri("http://package/xl/workbook.xml"), target).AbsolutePath.TrimStart('/');
        foreach (var (name, bytes) in parts.Where(p => p.Key.StartsWith("xl/worksheets/", StringComparison.Ordinal)))
        {
            var doc = Xml(bytes);
            var allowed = new[] { "sheetPr", "dimension", "sheetViews", "sheetFormatPr", "cols", "sheetData", "printOptions", "pageMargins", "pageSetup", "headerFooter", "tableParts" };
            Require(!doc.Descendants(S + "tablePart").Any(), "Table relationships are rejected; an empty tableParts marker is permitted.");
            Require(doc.Root!.Elements().All(e => e.Name.Namespace == S && allowed.Contains(e.Name.LocalName)), "Unsupported worksheet elements: " + string.Join(", ", doc.Root.Elements().Where(e => e.Name.Namespace != S || !allowed.Contains(e.Name.LocalName)).Select(e => e.Name.LocalName)));
            Require(!doc.Descendants().Attributes().Any(a => a.Name.LocalName is "hidden" or "outlineLevel" or "collapsed" && a.Value is not ("0" or "false")), "Hidden/grouped rows or columns are rejected.");
            Require(doc.Descendants(S + "row").All(r => int.Parse(r.Attribute("r")!.Value, CultureInfo.InvariantCulture) <= 2000), "Stored rows exceed structural bounds.");
            foreach (var cell in doc.Descendants(S + "c"))
            {
                var address = Address.Match(cell.Attribute("r")?.Value ?? "");
                Require(address.Success && Column(address.Groups[1].Value.Replace("$", "")) <= 32, "Stored cell exceeds structural bounds.");
                Require(cell.Attributes().All(a => a.Name.LocalName is "r" or "s" or "t") && cell.Elements().All(e => e.Name == S + "f" || e.Name == S + "v" || e.Name == S + "is"), "Cell metadata is unsupported.");
                Require(cell.Attribute("t")?.Value is null or "n" or "s" or "inlineStr" or "b", "Unsupported cell type.");
                if (cell.Element(S + "f") is { } f)
                {
                    Require(name == path && !f.HasAttributes && f.Value.Length <= 128 && Grammar.IsMatch(f.Value)
                        && Regex.Matches(f.Value, @"\$?([A-Z]+)\$?([0-9]+)").All(x => Column(x.Groups[1].Value) <= 32 && int.Parse(x.Groups[2].Value, CultureInfo.InvariantCulture) <= 2000), "Only bounded same-sheet SUM ranges or A1 addition formulas on the selected sheet are supported.");
                    if (f.Value.StartsWith("SUM(", StringComparison.Ordinal))
                    {
                        var endpoints = Regex.Matches(f.Value, @"\$?([A-Z]+)\$?([0-9]+)");
                        var start = int.Parse(endpoints[0].Groups[2].Value, CultureInfo.InvariantCulture);
                        var end = int.Parse(endpoints[1].Groups[2].Value, CultureInfo.InvariantCulture);
                        Require(endpoints[0].Groups[1].Value == endpoints[1].Groups[1].Value && start <= end,
                            "Only ordered single-column SUM ranges are supported.");
                        Require(end != m.Boundary - 1 || (int)cell.Parent!.Attribute("r")! == m.SubtotalRow,
                            "A non-section SUM ending immediately above insertion has ambiguous expansion semantics.");
                    }
                }
            }
            foreach (var row in doc.Descendants(S + "row"))
            {
                var addresses = row.Elements(S + "c").Select(c => (string)c.Attribute("r")!).ToArray();
                Require(addresses.Distinct().Count() == addresses.Length && addresses.All(a => Address.Match(a).Groups[3].Value == row.Attribute("r")!.Value), "Duplicate cells or inconsistent row coordinates are rejected.");
            }
        }
        var selected = Xml(parts[path]); var rows = selected.Descendants(S + "row").ToDictionary(r => (int)r.Attribute("r")!);
        Require(rows.ContainsKey(m.TemplateRow), "Missing explicit template row.");
        var template = rows[m.TemplateRow];
        Require(!template.Descendants(S + "f").Any(), "Formula-bearing templates are rejected.");
        Require(template.Attributes().All(a => a.Name.LocalName is "r" or "spans" or "ht" or "customHeight"), "Template row attributes are unsupported.");
        foreach (var record in m.Records)
            Require(record.Fields.Count is >= 1 and <= 16 && record.Fields.Select(f => f.Column).Distinct().Count() == record.Fields.Count
                && record.Fields.All(f => f.Column is >= 1 and <= 32 && f.Kind is "text" or "decimal" or "date" && f.Value.Length <= 256), "Invalid mapped fields.");
        foreach (var field in m.Records.SelectMany(r => r.Fields).Where(f => f.Kind == "decimal"))
        {
            var col = Letters(field.Column);
            Require(rows.TryGetValue(m.SubtotalRow, out var subtotal) && subtotal.Elements(S + "c")
                .SingleOrDefault(c => (string?)c.Attribute("r") == col + m.SubtotalRow)?.Element(S + "f")?.Value == $"SUM({col}{m.FirstDataRow}:{col}{m.SubtotalRow - 1})",
                "Every mapped decimal column needs a complete, explicit section SUM.");
        }
        foreach (var field in m.Records.SelectMany(r => r.Fields).Where(f => f.Kind == "date" && f.Value.Length > 0))
        {
            Require(DateOnly.TryParseExact(field.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                && date >= new DateOnly(1900, 3, 1), "Dates must be on or after 1900-03-01.");
            var cell = template.Elements(S + "c").SingleOrDefault(c => (string?)c.Attribute("r") == Letters(field.Column) + m.TemplateRow);
            var style = int.Parse(cell?.Attribute("s")?.Value ?? "0", CultureInfo.InvariantCulture);
            var styles = Xml(parts["xl/styles.xml"]);
            var formatId = (string?)styles.Root!.Element(S + "cellXfs")!.Elements(S + "xf").ElementAt(style).Attribute("numFmtId");
            Require(styles.Descendants(S + "numFmt").Any(f => (string?)f.Attribute("numFmtId") == formatId && (string?)f.Attribute("formatCode") == "yyyy-mm-dd"),
                "Mapped dates require the proven yyyy-mm-dd template number format.");
        }
        return path;
    }

    private static void CompareSheet(XDocument before, XDocument after, StructuralInsertionManifest m, List<StructuralDifference> diffs, CancellationToken token)
    {
        void Difference(string category, string location, string reason) => diffs.Add(new(category, location, reason));
        var rows = before.Root!.Element(S + "sheetData")!.Elements(S + "row").ToArray();
        var actual = after.Root!.Element(S + "sheetData")!.Elements(S + "row").ToArray();
        var template = rows.Single(r => (int)r.Attribute("r")! == m.TemplateRow);
        var expected = new SortedDictionary<int, XElement>();
        foreach (var old in rows)
        {
            token.ThrowIfCancellationRequested(); var oldRow = (int)old.Attribute("r")!; var newRow = oldRow < m.Boundary ? oldRow : oldRow + m.Count;
            var row = new XElement(old); row.SetAttributeValue("r", newRow);
            foreach (var cell in row.Elements(S + "c"))
            {
                var a = Address.Match((string)cell.Attribute("r")!); cell.SetAttributeValue("r", a.Groups[1].Value + newRow);
                if (cell.Element(S + "f") is { } formula)
                {
                    var text = formula.Value;
                    // Independent positional scan: each A1 coordinate denotes an original row identity.
                    // Dollar markers affect copying, not movement of referenced cells during insertion.
                    var matches = Regex.Matches(text, @"\$?[A-Z]+\$?([0-9]+)");
                    for (var i = matches.Count - 1; i >= 0; i--)
                    {
                        var coordinate = matches[i].Groups[1]; var original = int.Parse(coordinate.Value, CultureInfo.InvariantCulture);
                        var rangeEnd = text.StartsWith("SUM(", StringComparison.Ordinal) && i == 1;
                        var extend = rangeEnd && original == m.Boundary - 1 && oldRow == m.SubtotalRow;
                        if (original >= m.Boundary || extend) text = text.Remove(coordinate.Index, coordinate.Length).Insert(coordinate.Index, (original + m.Count).ToString(CultureInfo.InvariantCulture));
                    }
                    formula.Value = text; cell.Elements(S + "v").Remove();
                }
            }
            expected.Add(newRow, row);
        }
        for (var i = 0; i < m.Count; i++)
        {
            var index = m.Boundary + i;
            var row = new XElement(S + "row", template.Attributes().Where(a => a.Name.LocalName is not ("r" or "spans")).Select(a => new XAttribute(a)), new XAttribute("r", index));
            var columns = template.Elements(S + "c").Select(c => Address.Match((string)c.Attribute("r")!).Groups[1].Value).Concat(m.Records[i].Fields.Select(f => Letters(f.Column))).Distinct().OrderBy(Column);
            foreach (var col in columns)
            {
                var source = template.Elements(S + "c").SingleOrDefault(c => Address.Match((string)c.Attribute("r")!).Groups[1].Value == col);
                var cell = new XElement(S + "c", new XAttribute("r", col + index));
                if (source?.Attribute("s") is { } style) cell.Add(new XAttribute(style));
                var value = m.Records[i].Fields.SingleOrDefault(f => Letters(f.Column) == col);
                if (value != null && value.Value.Length > 0)
                {
                    if (value.Kind == "text") { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(S + "is", new XElement(S + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value.Value))); }
                    else cell.Add(new XElement(S + "v", value.Kind == "date" ? DateTime.ParseExact(value.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToOADate().ToString("R", CultureInfo.InvariantCulture) : decimal.Parse(value.Value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)));
                }
                row.Add(cell);
            }
            expected.Add(index, row);
        }
        if (actual.Length != expected.Count || actual.Select(r => (int)r.Attribute("r")!).Distinct().Count() != actual.Length) Difference("rows", "sheetData", "Unexpected missing/duplicate rows.");
        foreach (var row in actual)
        {
            var index = (int)row.Attribute("r")!;
            if (!expected.TryGetValue(index, out var wanted)) { Difference("rows", index.ToString(), "Unexpected row."); continue; }
            if (Canonical(new XElement("row", row.Attributes())) != Canonical(new XElement("row", wanted.Attributes()))) Difference("formatting", $"row {index}", "Row height/style/metadata changed.");
            var cells = row.Elements(S + "c").ToArray(); var wantedCells = wanted.Elements(S + "c").ToArray();
            if (!cells.Select(c => (string)c.Attribute("r")!).SequenceEqual(wantedCells.Select(c => (string)c.Attribute("r")!)))
                Difference("cells", $"row {index}", "Unexpected stored cell identities/order/count (including duplicates).");
            foreach (var c in cells)
            {
                var location = (string)c.Attribute("r")!; var w = wantedCells.SingleOrDefault(x => (string?)x.Attribute("r") == location);
                if (w == null || Canonical(c) != Canonical(w)) Difference(c.Element(S + "f") != null || w?.Element(S + "f") != null ? "formulas" : "content-or-formatting", location, "Cell differs from independently expected value, style, attributes or reference.");
            }
        }
        var left = new XElement(before.Root); var right = new XElement(after.Root);
        left.Element(S + "sheetData")!.Remove(); right.Element(S + "sheetData")!.Remove();
        // The optional used dimension must move exactly; every other worksheet feature is immutable.
        if (left.Element(S + "dimension") is { } dimension)
        {
            var range = dimension.Attribute("ref")!.Value.Split(':');
            var end = Address.Match(range[^1]); var endRow = int.Parse(end.Groups[3].Value, CultureInfo.InvariantCulture);
            range[^1] = end.Groups[1].Value + (endRow >= m.Boundary ? endRow + m.Count : endRow);
            dimension.SetAttributeValue("ref", string.Join(':', range));
        }
        if (Canonical(left) != Canonical(right)) Difference("worksheet-structure", m.Worksheet, "Dimension, merges, ranges or other worksheet metadata changed unexpectedly.");
    }
    internal static int Column(string letters) => letters.Aggregate(0, (n, c) => checked(n * 26 + c - 'A' + 1));
    internal static string Letters(int column) { var s = ""; while (column > 0) { column--; s = (char)('A' + column % 26) + s; column /= 26; } return s; }
    internal static string Canonical(XElement element)
    {
        // Expanded names and length-prefixed values avoid namespace-prefix, indentation and delimiter ambiguity.
        static string Atom(string text) => text.Length.ToString(CultureInfo.InvariantCulture) + ":" + text;
        return Atom(element.Name.ToString()) + "[" + string.Concat(element.Attributes().Where(a => !a.IsNamespaceDeclaration)
            .OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).Select(a => Atom(a.Name.ToString()) + Atom(a.Value))) + "]"
            + "{" + string.Concat(element.Nodes().Select(n => n is XElement child ? Canonical(child)
                : n is XText t && (!element.HasElements || !string.IsNullOrWhiteSpace(t.Value)) ? Atom(t.Value) : "")) + "}";
    }
}
