using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using S = DocumentFormat.OpenXml.Spreadsheet;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed record PackageFacts(int Cells, long ExpandedBytes, string Creator, string Title, IReadOnlyList<CompatibilityFinding> Findings, Dictionary<string, string> OpaqueHashes, SortedDictionary<string, string> StructuralFacts);

internal static class PackageGuard
{
    internal static WorkbookException Reject(string code, string message) => new(code, message);

    internal static PackageFacts Inspect(byte[] bytes, WorkbookLimits limits, CancellationToken token)
    {
        if (bytes.LongLength > limits.MaxFileBytes) throw Reject("LIMIT_EXCEEDED", "Workbook exceeds the configured compressed-file limit.");
        if (bytes.Length >= 2 && bytes[0] == 0xD0 && bytes[1] == 0xCF) throw Reject("PROTECTED_WORKBOOK", "Encrypted or legacy compound workbooks are unsupported. Supply an unencrypted .xlsx copy.");
        var findings = new Dictionary<string, CompatibilityFinding>(StringComparer.Ordinal)
        { ["EDIT_SCOPE"] = new("EDIT_SCOPE", "Only validated single-cell in-memory edits are supported. Integration and saving are unavailable; complete Excel preservation is not certified.") };
        var opaque = new Dictionary<string, string>(StringComparer.Ordinal);
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var input = new MemoryStream(bytes, false);
        using var zip = new ZipArchive(input, ZipArchiveMode.Read);
        if (zip.Entries.Count > limits.MaxEntries) throw Reject("LIMIT_EXCEEDED", "Workbook has too many package entries.");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expanded = 0, actualExpanded = 0; var cells = 0; var sheets = 0; var styles = 0; var elements = 0;
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            var name = Uri.UnescapeDataString(entry.FullName);
            if (!names.Add(name) || name.StartsWith('/') || name.Contains('\\') || name.Split('/').Any(p => p is ".." or ".") || name.Contains(':'))
                throw Reject("INVALID_PACKAGE", "Workbook contains duplicate or unsafe package paths.");
            expanded = checked(expanded + entry.Length);
            if (expanded > limits.MaxExpandedBytes) throw Reject("LIMIT_EXCEEDED", "Workbook exceeds the expanded-package limit.");
            if (name.EndsWith('/')) continue;
            var lower = name.ToLowerInvariant();
            if (lower == "xl/calcchain.xml") findings["ADVANCED_CONTENT"] = new("ADVANCED_CONTENT", "A calculation chain is present. Snapshot preservation has not been established.");
            if (lower.Contains("vbaproject") || lower.Contains("activex") || lower.Contains("macrosheets") || lower.Contains("externallinks") || lower.Contains("connections") || lower.StartsWith("_xmlsignatures/"))
                throw Reject("UNSUPPORTED_FEATURE", "Macros, active content, signatures and external data links are unsupported. Supply a plain .xlsx copy.");
            if (!lower.EndsWith(".xml") && !lower.EndsWith(".rels") && !lower.EndsWith(".psmdcp"))
                throw Reject("UNSUPPORTED_FEATURE", "Editing rejects non-XML binary or embedded parts, including images, because preservation is unverified. Use Inspect workbook read-only for categorized metadata and bounded cells.");
            using var stream = entry.Open();
            using var bounded = new CountingStream(stream, count =>
            {
                actualExpanded = checked(actualExpanded + count);
                if (actualExpanded > limits.MaxExpandedBytes) throw Reject("LIMIT_EXCEEDED", "Workbook exceeds the expanded-package limit.");
            });
            using var reader = XmlReader.Create(bounded, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = limits.MaxExpandedBytes, IgnoreComments = true });
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                if (reader.Depth > 64) throw Reject("LIMIT_EXCEEDED", "Workbook XML nesting exceeds the safe limit.");
                if (reader.NodeType != XmlNodeType.Element) continue;
                var element = reader.LocalName;
                if (++elements > 500000) throw Reject("LIMIT_EXCEEDED", "Workbook contains too many XML elements.");
                if (element == "Relationship")
                {
                    var target = reader.GetAttribute("Target") ?? "";
                    if (string.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase) || target.Contains("://") || target.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                        throw Reject("EXTERNAL_RESOURCE", "External workbook resources are not supported. Remove links in a separate copy before opening.");
                    var sourcePart = entry.FullName == "_rels/.rels" ? "" : entry.FullName.Replace("/_rels/", "/")[..^5];
                    if (!Uri.TryCreate(new Uri("http://package/" + sourcePart), target, out var resolved) || resolved.Host != "package" || resolved.Query.Length > 0 || resolved.Fragment.Length > 0
                        || zip.GetEntry(resolved.AbsolutePath.TrimStart('/')) == null)
                        throw Reject("INVALID_PACKAGE", "Workbook contains a dangling or unsafe package relationship.");
                    targets.Add(Uri.UnescapeDataString(resolved.AbsolutePath.TrimStart('/')));
                    var relationshipType = (reader.GetAttribute("Type") ?? "").Split('/').Last();
                    if (relationshipType is not ("officeDocument" or "worksheet" or "styles" or "sharedStrings" or "theme" or "table" or "calcChain" or "core-properties" or "extended-properties" or "custom-properties" or "customXml" or "customXmlProps"))
                        findings["UNKNOWN_PARTS"] = new("UNKNOWN_PARTS", "Unrecognized package relationships restrict this workbook to inspection; snapshots are disabled.");
                }
                if (element is "workbookProtection" or "sheetProtection" or "protectedRanges") throw Reject("PROTECTED_WORKBOOK", "Protected workbooks or worksheets are unsupported. Supply an authorized unprotected copy.");
                if (element == "Override")
                {
                    var type = reader.GetAttribute("ContentType") ?? "";
                    if (type.Contains("macro", StringComparison.OrdinalIgnoreCase) || type.Contains("chartsheet", StringComparison.OrdinalIgnoreCase))
                        throw Reject("UNSUPPORTED_FEATURE", "Macro or chart-sheet content is outside the read-only compatibility scope.");
                }
                if (element == "c" && reader.NamespaceURI == "http://schemas.openxmlformats.org/spreadsheetml/2006/main" && ++cells > limits.MaxCells)
                    throw Reject("LIMIT_EXCEEDED", $"Editable validation stopped at {cells} stored cell elements (including styled empty cells); the limit is {limits.MaxCells}. Use Inspect workbook read-only. The full workbook count is not measured.");
                if (element == "sheet" && ++sheets > limits.MaxWorksheets) throw Reject("LIMIT_EXCEEDED", $"Editable validation stopped at {sheets} worksheet declarations; the limit is {limits.MaxWorksheets}. Use Inspect workbook read-only for metadata and bounded browsing.");
                if (element == "xf" && ++styles > limits.MaxStyles) throw Reject("LIMIT_EXCEEDED", "Workbook has too many styles.");
                if (element is "extLst" or "conditionalFormatting" or "dataValidations" or "drawing" or "pivotTable" || (element == "f" && reader.GetAttribute("t") is "array" or "shared"))
                    findings["ADVANCED_CONTENT"] = new("ADVANCED_CONTENT", "Advanced or extension content is present. Only basic cell inspection is available; future mutations require separate compatibility tests.");
            }
            if (bounded.BytesRead != entry.Length) throw Reject("INVALID_PACKAGE", "Workbook has inconsistent package entry sizes.");
            if (lower.StartsWith("customxml/"))
            {
                using var content = entry.Open(); opaque[entry.FullName] = Convert.ToHexString(SHA256.HashData(content));
                findings["OPAQUE_PARTS"] = new("OPAQUE_PARTS", "Custom XML is retained in the immutable baseline. Arbitrary extension preservation is not certified.");
            }
            else if (!IsKnownPart(lower))
                findings["UNKNOWN_PARTS"] = new("UNKNOWN_PARTS", "Unrecognized package parts restrict this workbook to inspection; snapshots are disabled.");
        }
        if (names.Any(name => !name.EndsWith('/') && !name.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) && !name.Equals("[Content_Types].xml", StringComparison.OrdinalIgnoreCase) && !targets.Contains(name)))
            findings["UNKNOWN_PARTS"] = new("UNKNOWN_PARTS", "Unreferenced package parts restrict this workbook to inspection; snapshots are disabled.");
        using var packageStream = new MemoryStream(bytes, false);
        using var package = SpreadsheetDocument.Open(packageStream, false, new OpenSettings { AutoSave = false, MaxCharactersInPart = limits.MaxExpandedBytes });
        var wb = package.WorkbookPart ?? throw Reject("INVALID_PACKAGE", "The package does not contain an Excel workbook.");
        var validation = new OpenXmlValidator().Validate(package, token).Take(1).ToArray();
        if (validation.Length > 0) throw Reject("INVALID_PACKAGE", "Workbook failed Open XML structural validation. Repair a separate copy in Excel before opening.");
        if (wb.Workbook?.Sheets == null || !wb.Workbook.Sheets.Any()) throw Reject("INVALID_PACKAGE", "Workbook has no worksheets.");
        var structural = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["date1904"] = (wb.Workbook.WorkbookProperties?.Date1904?.Value ?? false).ToString()
        };
        foreach (var defined in wb.Workbook.DefinedNames?.Elements<S.DefinedName>() ?? [])
            structural[$"name/{defined.Name?.Value}/{defined.LocalSheetId?.Value.ToString() ?? "global"}"] = defined.Text;
        foreach (var sheet in wb.Workbook.Sheets.Elements<S.Sheet>())
        {
            if (wb.GetPartById(sheet.Id!) is not WorksheetPart part) throw Reject("UNSUPPORTED_FEATURE", "Only worksheet tabs are supported.");
            var name = sheet.Name!.Value!;
            var worksheet = part.Worksheet ?? throw Reject("INVALID_PACKAGE", "A worksheet has no content.");
            structural[$"merges/{name}"] = string.Join(";", worksheet.Descendants<S.MergeCell>().Select(m => m.Reference!.Value).Order(StringComparer.Ordinal));
            foreach (var table in part.TableDefinitionParts.Select(p => p.Table ?? throw Reject("INVALID_PACKAGE", "A table has no definition.")))
                structural[$"table/{name}/{table.Name!.Value}"] = $"{table.Reference!.Value}|{table.AutoFilter?.Reference?.Value}";
        }
        if (package.CustomFilePropertiesPart?.Properties != null)
            foreach (var property in package.CustomFilePropertiesPart.Properties.Elements<DocumentFormat.OpenXml.CustomProperties.CustomDocumentProperty>())
                structural[$"custom/{property.Name!.Value}"] = property.InnerText;
        return new(cells, expanded, package.PackageProperties.Creator ?? "", package.PackageProperties.Title ?? "", Array.AsReadOnly(findings.Values.ToArray()), opaque, structural);
    }

    private static bool IsKnownPart(string name) => name is "[content_types].xml" or "_rels/.rels" or "xl/workbook.xml" or "xl/_rels/workbook.xml.rels" or "xl/styles.xml" or "xl/sharedstrings.xml" or "xl/calcchain.xml"
        || name.StartsWith("xl/worksheets/") || name.StartsWith("xl/tables/") || name.StartsWith("xl/theme/")
        || name.StartsWith("docprops/") || name.StartsWith("package/services/metadata/core-properties/");

    private sealed class CountingStream(Stream inner, Action<int> count) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int length) { var n = inner.Read(buffer, offset, length); BytesRead += n; count(n); return n; }
        public override int Read(Span<byte> buffer) { var n = inner.Read(buffer); BytesRead += n; count(n); return n; }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
