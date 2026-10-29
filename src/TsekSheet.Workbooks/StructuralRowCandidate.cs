using System.Globalization;
using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using TsekSheet.Application.Workbooks;
using TsekSheet.Application.Workflows;

namespace TsekSheet.Workbooks;

internal enum StructuralStage { BeforeCreation, AfterShift, FieldMapping, FormulaAdjustment, Serialization, Reopen, Oracle, BeforeAdoption }
internal sealed record StructuralExperimentResult(WorkbookInfo Workbook, StructuralInsertionManifest Manifest, StructuralPreservationReport Report);

internal static class StructuralRowCandidate
{
    // Mutation implementation intentionally does not call the oracle's expected-row/reference code.
    internal static byte[] Build(byte[] source, StructuralInsertionManifest m, Action<StructuralStage> stage, CancellationToken token)
    {
        var parts = StructuralPreservationOracle.Read(source, token);
        var path = StructuralPreservationOracle.InspectEnvelope(parts, m);
        var s = StructuralPreservationOracle.S; var sheet = StructuralPreservationOracle.Xml(parts[path]);
        var data = sheet.Root!.Element(s + "sheetData")!;
        var template = new XElement(data.Elements(s + "row").Single(r => (int)r.Attribute("r")! == m.TemplateRow));
        foreach (var row in data.Elements(s + "row"))
        {
            token.ThrowIfCancellationRequested(); var old = (int)row.Attribute("r")!;
            if (old >= m.Boundary)
            {
                row.SetAttributeValue("r", old + m.Count);
                foreach (var c in row.Elements(s + "c")) c.SetAttributeValue("r", Regex.Replace((string)c.Attribute("r")!, "[0-9]+$", (old + m.Count).ToString(CultureInfo.InvariantCulture)));
            }
        }
        stage(StructuralStage.AfterShift);
        for (var i = 0; i < m.Count; i++)
        {
            var row = new XElement(template); row.SetAttributeValue("r", m.Boundary + i); row.Attribute("spans")?.Remove();
            foreach (var cell in row.Elements(s + "c"))
            {
                cell.RemoveNodes(); cell.Attribute("t")?.Remove();
                cell.SetAttributeValue("r", Regex.Replace((string)cell.Attribute("r")!, "[0-9]+$", (m.Boundary + i).ToString(CultureInfo.InvariantCulture)));
            }
            foreach (var field in m.Records[i].Fields)
            {
                var address = StructuralPreservationOracle.Letters(field.Column) + (m.Boundary + i);
                var cell = row.Elements(s + "c").SingleOrDefault(c => (string?)c.Attribute("r") == address);
                if (cell == null) { cell = new(s + "c", new XAttribute("r", address)); row.Add(cell); }
                if (field.Value.Length == 0) continue;
                switch (field.Kind)
                {
                    case "text": cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(s + "is", new XElement(s + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), field.Value))); break;
                    case "decimal": cell.Add(new XElement(s + "v", field.Value)); break;
                    case "date": cell.Add(new XElement(s + "v", DateOnly.ParseExact(field.Value, "yyyy-MM-dd", CultureInfo.InvariantCulture).ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture))); break;
                    default: throw PackageGuard.Reject("INVALID_RECORD", "Unsupported field kind.");
                }
                stage(StructuralStage.FieldMapping);
            }
            row.ReplaceNodes(row.Elements().OrderBy(c => StructuralPreservationOracle.Column(Regex.Match((string)c.Attribute("r")!, "^[A-Z]+").Value)).ToArray());
            data.Add(row);
        }
        foreach (var formula in data.Descendants(s + "f"))
        {
            var owner = (int)formula.Parent!.Parent!.Attribute("r")!;
            var sum = formula.Value.StartsWith("SUM(", StringComparison.Ordinal); var endpoint = 0;
            formula.Value = Regex.Replace(formula.Value, @"(\$?[A-Z]+\$?)([0-9]+)", match =>
            {
                var reference = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture); endpoint++;
                if (reference >= m.Boundary || (sum && endpoint == 2 && reference + 1 == m.Boundary && owner == m.SubtotalRow + m.Count)) reference += m.Count;
                return match.Groups[1].Value + reference.ToString(CultureInfo.InvariantCulture);
            });
            formula.Parent.Elements(s + "v").Remove();
            stage(StructuralStage.FormulaAdjustment);
        }
        data.ReplaceNodes(data.Elements().OrderBy(r => (int)r.Attribute("r")!).ToArray());
        if (sheet.Root.Element(s + "dimension") is { } dimension)
        {
            var endpoints = dimension.Attribute("ref")!.Value.Split(':');
            endpoints[^1] = Regex.Replace(endpoints[^1], "[0-9]+$", match =>
            {
                var r = int.Parse(match.Value, CultureInfo.InvariantCulture); return (r >= m.Boundary ? r + m.Count : r).ToString(CultureInfo.InvariantCulture);
            });
            dimension.SetAttributeValue("ref", string.Join(':', endpoints));
        }
        var wb = StructuralPreservationOracle.Xml(parts["xl/workbook.xml"]); var calc = wb.Root!.Element(s + "calcPr");
        if (calc == null) { calc = new(s + "calcPr"); wb.Root.Add(calc); }
        calc.SetAttributeValue("fullCalcOnLoad", "1"); calc.SetAttributeValue("forceFullCalc", "1");
        using var output = new MemoryStream();
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var (name, bytes) in parts)
            {
                token.ThrowIfCancellationRequested(); using var stream = zip.CreateEntry(name).Open();
                if (name == path) sheet.Save(stream, SaveOptions.DisableFormatting);
                else if (name == "xl/workbook.xml") wb.Save(stream, SaveOptions.DisableFormatting);
                else stream.Write(bytes);
                stage(StructuralStage.Serialization);
            }
        return output.ToArray();
    }
}

internal sealed partial class WorkbookSession
{
    private bool structuralExperiment;

    public WorkflowPlan InspectStructuralInsertion(WorkflowPlan plan, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested();
            if (plan.SessionId != info.SessionId || plan.SourceSha256 != info.SourceSha256 || plan.Revision != info.Revision)
                return plan with { StructuralStatus = "stale-proposal", InsertionBlockedReason = "Proposal identity or revision is stale. Generate a fresh preview." };
            if (!plan.ReadyForReview) return plan with { StructuralStatus = "ambiguous-destination" };
            try
            {
                EnsureEditable();
                if (plan.Records.Select(r => r.BaseInsertionRow).Distinct().Count() != 1 || plan.Records.Select(r => r.SectionId).Distinct().Count() != 1)
                    throw PackageGuard.Reject("STRUCTURE_UNSUPPORTED", "Only one insertion boundary and section per experimental batch are supported.");
                var analysis = WorksheetAnalysisReader.Read(this, plan.WorksheetId, plan.Revision, WorkflowProfiles.Get(plan.ProfileId), token);
                var section = analysis.Sections.Single(s => s.Id == plan.Records[0].SectionId);
                if (section.Records.Count == 0) throw PackageGuard.Reject("UNSAFE_TEMPLATE", "No existing record style template is available.");
                var manifest = new StructuralInsertionManifest(analysis.WorksheetName, plan.Records[0].BaseInsertionRow!.Value, section.Records[0].Row,
                    section.FirstDataRow, section.SubtotalRow, plan.Records.Select(r => new StructuralRecord(r.Fields.Select(f => new StructuralValue(f.Column, f.Kind, f.Value)).ToArray())).ToArray());
                StructuralPreservationOracle.InspectEnvelope(StructuralPreservationOracle.Read(currentBytes, token), manifest);
                return plan with { StructuralStatus = "experimental-envelope-only", InsertionBlockedReason = "Plain-row package preflight passed. Candidate styling, serialization, reopening and preservation still require validation. Production Apply and structural Merge remain disabled." };
            }
            catch (WorkbookException ex) { return plan with { StructuralStatus = "unsupported-structure", InsertionBlockedReason = "Apply is disabled: " + ex.Message }; }
        }
    }

    // No interface, bridge, frontend or public engine entry point enables this experiment.
    internal StructuralExperimentResult ExperimentInsert(WorkflowPlanRequest request, WorkflowPlan reviewed,
        bool approved, bool duplicatesReviewed, CancellationToken token = default, bool prepareOnly = false)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this); token.ThrowIfCancellationRequested(); EnsureEditable();
            if ((!engine.StructuralExperimentsEnabled && !(prepareOnly && reviewConstruction)) || workingSheet != request.WorksheetId || info.ParentSessionId == null)
                throw PackageGuard.Reject("ROW_INSERTION_DISABLED", "Structural experiments require the internal capability and selected working copy.");
            if (info.Revision == int.MaxValue) throw PackageGuard.Reject("LIMIT_EXCEEDED", "Revision limit reached.");
            if (reviewed.SourceSha256 != info.SourceSha256) throw PackageGuard.Reject("INVALID_PROPOSAL", "Source provenance differs.");
            var fresh = WorkflowPlanning.Prepare(this, request, token);
            if (JsonSerializer.Serialize(fresh) != JsonSerializer.Serialize(reviewed)) throw PackageGuard.Reject("INVALID_PROPOSAL", "The reviewed proposal differs from current re-analysis.");
            if (!approved || !fresh.ReadyForReview || (fresh.Records.Any(r => r.Duplicates.Count > 0) && !duplicatesReviewed))
                throw PackageGuard.Reject("REVIEW_REQUIRED", "Explicit approval and resolved choices/duplicate review are required.");
            if (fresh.Records.Select(r => r.BaseInsertionRow).Distinct().Count() != 1 || fresh.Records.Select(r => r.SectionId).Distinct().Count() != 1)
                throw PackageGuard.Reject("STRUCTURE_UNSUPPORTED", "The experiment supports one insertion boundary and section per batch.");
            var profile = WorkflowProfiles.Get(fresh.ProfileId);
            var analysis = WorksheetAnalysisReader.Read(this, request.WorksheetId, info.Revision, profile, token);
            var section = analysis.Sections.Single(s => s.Id == fresh.Records[0].SectionId);
            if (section.Records.Count == 0) throw PackageGuard.Reject("UNSAFE_TEMPLATE", "A nonempty uniform record template is required.");
            var manifest = new StructuralInsertionManifest(analysis.WorksheetName, fresh.Records[0].BaseInsertionRow!.Value,
                section.Records[0].Row, section.FirstDataRow, section.SubtotalRow,
                fresh.Records.Select(r => new StructuralRecord(r.Fields.Select(f => new StructuralValue(f.Column, f.Kind, f.Value)).ToArray())).ToArray());
            var parts = StructuralPreservationOracle.Read(currentBytes, token);
            var path = StructuralPreservationOracle.InspectEnvelope(parts, manifest);
            var s = StructuralPreservationOracle.S; var xml = StructuralPreservationOracle.Xml(parts[path]);
            var rows = xml.Descendants(s + "row").ToDictionary(r => (int)r.Attribute("r")!);
            string Pattern(XElement row) => string.Join("|", row.Attributes().Where(a => a.Name.LocalName is not ("r" or "spans")).OrderBy(a => a.Name.ToString()).Select(a => a.ToString())) + ";"
                + string.Join("|", row.Elements(s + "c").Select(c => Regex.Match((string)c.Attribute("r")!, "^[A-Z]+").Value + ":" + ((string?)c.Attribute("s") ?? "0")));
            var template = rows[manifest.TemplateRow];
            if (section.Records.Any(r => rows[r.Row].Descendants(s + "f").Any() || Pattern(rows[r.Row]) != Pattern(template)))
                throw PackageGuard.Reject("UNSAFE_TEMPLATE", "Neighboring record styles/heights or formula patterns are inconsistent.");
            foreach (var field in fresh.Records[0].Fields.Where(f => f.Kind == "decimal"))
            {
                var col = StructuralPreservationOracle.Letters(field.Column);
                var formula = rows[section.SubtotalRow].Elements(s + "c").SingleOrDefault(c => (string?)c.Attribute("r") == col + section.SubtotalRow)?.Element(s + "f")?.Value;
                if (formula != $"SUM({col}{section.FirstDataRow}:{col}{section.SubtotalRow - 1})")
                    throw PackageGuard.Reject("UNVERIFIED_SUBTOTAL", "Every mapped decimal field requires a complete section SUM subtotal.");
            }
            void Stage(StructuralStage stage) { engine.StructuralFaultForTesting?.Invoke(stage); token.ThrowIfCancellationRequested(); }
            using var reservation = engine.ReserveSnapshotWork(estimate);
            WorkbookSession? candidate = null;
            try
            {
                Stage(StructuralStage.BeforeCreation);
                var bytes = StructuralRowCandidate.Build(currentBytes, manifest, Stage, token);
                if (engine.CandidateFaultForTesting != null) bytes = engine.CandidateFaultForTesting(bytes);
                Stage(StructuralStage.Reopen);
                candidate = engine.Load(bytes, info.DisplayName, info.SourceSha256, info.ParentSessionId, info.Worksheets, token);
                Stage(StructuralStage.Oracle);
                var report = StructuralPreservationOracle.Evaluate(currentBytes, manifest, bytes, token);
                if (!report.Accepted) throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", string.Join("; ", report.Differences.Select(d => d.Category + ": " + d.Reason)));
                var next = info with { Revision = info.Revision + 1, IsDirty = true, Worksheets = candidate.info.Worksheets };
                var result = new StructuralExperimentResult(next, manifest, report);
                if (prepareOnly)
                {
                    token.ThrowIfCancellationRequested();
                    StorePreparedCandidate(fresh, manifest, bytes, report);
                    candidate.Dispose(); candidate = null;
                    return result with { Workbook = info };
                }
                Stage(StructuralStage.BeforeAdoption);
                var oldWorkbook = workbook; var oldStream = inputStream; var oldRelease = release;
                workbook = candidate.workbook; inputStream = candidate.inputStream; currentBytes = candidate.currentBytes;
                facts = candidate.facts; cellsBySheet = candidate.cellsBySheet; estimate = candidate.estimate; release = candidate.release;
                info = next; structuralExperiment = true;
                candidate.disposed = true; candidate.workbook = null; candidate.inputStream = null; candidate = null;
                try { oldWorkbook?.Dispose(); } catch { }
                try { oldStream?.Dispose(); } catch { }
                finally { oldRelease(); }
                return result;
            }
            catch (WorkbookException) { candidate?.Dispose(); throw; }
            catch (OperationCanceledException) { candidate?.Dispose(); throw; }
            catch { candidate?.Dispose(); throw PackageGuard.Reject("STRUCTURAL_VALIDATION_FAILED", "Candidate failed; accepted state retained."); }
        }
    }
}
