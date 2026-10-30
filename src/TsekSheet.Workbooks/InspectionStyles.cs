using System.Xml;

namespace TsekSheet.Workbooks;

// Resolves number-format facts only. Font/fill/protection inheritance is not rendered.
internal static class InspectionStyles
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private sealed record Xf(int? Id, int? Parent, string? Apply, string Error);
    private sealed record Definition(string Code, string Error);

    internal static string? FixedBuiltIn(int id) => id switch
    { 0 => "General", 1 => "0", 2 => "0.00", 3 => "#,##0", 4 => "#,##0.00", 9 => "0%", 10 => "0.00%", _ => null };

    internal static IReadOnlyList<InspectionNumberFormat> Read(XmlReader reader, Action<XmlReader> node)
    {
        var definitions = new Dictionary<int, Definition>();
        var cells = new List<Xf>(); var bases = new List<Xf>();
        string container = "", topLevel = ""; var seen = new HashSet<string>(); int xfCount = 0, definitionCount = 0;
        while (reader.Read())
        {
            node(reader);
            if (reader.Depth == 0 && reader.NodeType == XmlNodeType.Element && (reader.LocalName != "styleSheet" || reader.NamespaceURI != Ns))
                throw WorkbookInspection.Error("Invalid stylesheet root.");
            if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == 1) { container = ""; topLevel = ""; }
            if (reader.NodeType != XmlNodeType.Element) continue;
            if (reader.Depth == 1) topLevel = reader.LocalName;
            if (reader.NamespaceURI != Ns)
            {
                // Qualified native captures contain these empty, non-cell style selectors.
                // Do not ignore arbitrary foreign number-format or inheritance extensions.
                if (topLevel == "extLst" && reader.Depth == 3 && reader.IsEmptyElement
                    && (reader.LocalName == "slicerStyles" && reader.NamespaceURI == "http://schemas.microsoft.com/office/spreadsheetml/2009/9/main"
                        || reader.LocalName == "timelineStyles" && reader.NamespaceURI == "http://schemas.microsoft.com/office/spreadsheetml/2010/11/main")) continue;
                throw WorkbookInspection.Error("Unknown stylesheet namespace; effective style resolution is unverified.");
            }
            if (reader.LocalName is "numFmts" or "cellXfs" or "cellStyleXfs")
            {
                if (reader.Depth != 1 || !seen.Add(reader.LocalName)) throw WorkbookInspection.Error("Duplicate or malformed style containers.");
                container = reader.IsEmptyElement ? "" : reader.LocalName;
            }
            if (reader.LocalName == "numFmt")
            {
                if (topLevel == "dxfs") continue; // Conditional/differential formatting is not rendered or used as a cell format.
                if (container != "numFmts" || reader.Depth != 2 || !int.TryParse(reader.GetAttribute("numFmtId"), out var id) || id < 0)
                    throw WorkbookInspection.Error("Unassignable number-format definition.");
                if (++definitionCount > 1024) throw WorkbookInspection.Error("Number-format definition budget exceeded.");
                var code = reader.GetAttribute("formatCode");
                var error = code == null || code.Length == 0 ? "Missing number-format code." : code.Length > 256 ? "Number-format code exceeds the supported length; shown code is truncated." : "";
                var definition = new Definition(code?[..Math.Min(code.Length, 256)] ?? "", error);
                if (!definitions.TryAdd(id, definition)) definitions[id] = definitions[id] with { Error = "Duplicate/conflicting number-format definitions; no definition was selected." };
            }
            if (reader.LocalName == "xf")
            {
                if (++xfCount > 4096) throw WorkbookInspection.Error("Style record budget exceeded.");
                if (reader.Depth != 2 || container is not ("cellXfs" or "cellStyleXfs")) throw WorkbookInspection.Error("Invalid style record nesting.");
                var idText = reader.GetAttribute("numFmtId"); var parentText = reader.GetAttribute("xfId"); var apply = reader.GetAttribute("applyNumberFormat");
                int? id = int.TryParse(idText, out var n) && n >= 0 ? n : null;
                int? parent = parentText == null ? null : int.TryParse(parentText, out var p) && p >= 0 ? p : -1;
                var error = id == null ? "Missing or invalid number-format ID." : apply is not (null or "0" or "1" or "true" or "false") ? "Invalid applyNumberFormat flag." : "";
                (container == "cellXfs" ? cells : bases).Add(new(id, parent, apply, error));
            }
        }
        InspectionNumberFormat Resolve(Xf cell)
        {
            var originalId = cell.Id ?? -1;
            var originalCode = definitions.GetValueOrDefault(originalId)?.Code ?? (originalId < 164 ? BuiltIn(originalId) : "");
            InspectionNumberFormat Reject(string reason, string category, string resolution = "Unresolved number format")
                => new(originalId, originalCode, reason + " Raw value retained.", category, null, resolution);
            if (cell.Error.Length > 0) return Reject(cell.Error, "Malformed style definition");
            Xf? parent = null;
            if (cell.Parent != null)
            {
                if (cell.Parent < 0 || cell.Parent >= bases.Count) return Reject("Missing cell-style inheritance reference.", "Unresolved inheritance");
                if (cell.Parent < bases.Count) parent = bases[cell.Parent.Value];
            }
            if (parent?.Error.Length > 0 || parent?.Parent != null) return Reject("Malformed or recursive base style.", "Unresolved inheritance");
            bool explicitApply = cell.Apply is "1" or "true";
            bool disabled = cell.Apply is "0" or "false";
            var resolution = "Direct cell number format";
            if (disabled || cell.Parent > 0 && !explicitApply || parent != null && parent.Id != 0 && !explicitApply)
            {
                // Agreement is independent of which side wins the apply flag. Divergence is not guessed.
                if (parent == null || parent.Id != cell.Id) return Reject("Cell/base number formats differ or inheritance is unresolved.", "Unresolved inheritance");
                resolution = "Cell and base style agree on number-format ID; no other inheritance inferred";
            }
            else if (explicitApply && cell.Parent > 0) resolution = "Explicit cell number format; base style reference validated";
            if (definitions.TryGetValue(originalId, out var def))
            {
                if (def.Error.Length > 0) return Reject(def.Error, def.Error.StartsWith("Duplicate", StringComparison.Ordinal) ? "Ambiguous format" : "Malformed format definition", resolution);
                if (originalId < 164)
                {
                    var fixedCode = FixedBuiltIn(originalId);
                    if (fixedCode == null) return Reject("Explicit low-ID format is outside the independently qualified fixed built-in subset.", "Unsupported low-ID definition", resolution);
                    if (def.Code != fixedCode) return Reject("Explicit format conflicts with the qualified built-in meaning.", "Ambiguous format", resolution);
                    resolution += "; explicit code agrees exactly with fixed built-in";
                }
                return new(originalId, def.Code, "", "Format candidate", originalId, resolution);
            }
            if (originalId >= 164) return Reject("Referenced custom format is missing.", "Missing format definition", resolution);
            return new(originalId, BuiltIn(originalId), "", "Format candidate", originalId, resolution);
        }
        return cells.Select(Resolve).ToArray();
    }

    private static string BuiltIn(int id) => FixedBuiltIn(id) ?? (id is >= 14 and <= 22 ? "Built-in date/time " + id : "Unsupported built-in " + id);
}
