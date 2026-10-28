using System.Diagnostics;
using System.Text.Json;

namespace WorkbookLibrary.Evaluation;

public sealed record Experiment(string Fixture, string Library, string Operation, string Status, bool SourceUnchanged, long Milliseconds, long AllocatedBytes, long ManagedBytesAfter, string? Output, string[] Differences, string[] SchemaErrors, string[] RemovedParts, string[] Observations, string? Error);

public static class Program
{
    public static int Main(string[] args)
    {
        var root = FindRoot();
        var fixtureDir = Path.Combine(root, "tests", "WorkbookLibrary.Evaluation", "Fixtures");
        Directory.CreateDirectory(fixtureDir);
        if (args.Length == 2 && args[0] == "--benchmark") return Benchmark(root, args[1]);
        if (args.Contains("--generate"))
        {
            foreach (var spec in FixtureFactory.Specs) FixtureFactory.Create(Path.Combine(fixtureDir, spec.FileName), spec);
            File.WriteAllText(Path.Combine(fixtureDir, "sha256.json"), JsonSerializer.Serialize(FixtureFactory.Specs.ToDictionary(s => s.FileName, s => PackageInspector.Hash(Path.Combine(fixtureDir, s.FileName))), new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine("Created artificial fixtures with CreateNew and SHA-256 manifest.");
            return 0;
        }
        var run = Path.Combine(root, "artifacts", "workbook-evaluation", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(run);
        var results = new List<Experiment>();
        foreach (var spec in FixtureFactory.Specs)
        {
            var source = Path.Combine(fixtureDir, spec.FileName);
            var sourceHash = PackageInspector.Hash(source); var bytes = File.ReadAllBytes(source);
            var manifest = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(Path.Combine(fixtureDir, "sha256.json")))!;
            if (sourceHash != manifest[spec.FileName]) throw new InvalidOperationException("Fixture hash differs from manifest.");
            var baseline = PackageInspector.Read(source);
            if (baseline.SchemaErrors.Length > 0) throw new InvalidOperationException("Invalid source fixture: " + string.Join(";", baseline.SchemaErrors));
            foreach (var library in LibraryProbes.Libraries.Append("EPPlus"))
            foreach (var operation in LibraryProbes.Operations)
            {
                var output = Path.Combine(run, Path.GetFileNameWithoutExtension(spec.FileName) + "-" + library + "-" + operation + ".xlsx");
                var timer = Stopwatch.StartNew(); var allocation = GC.GetTotalAllocatedBytes();
                try
                {
                    if (library == "EPPlus") throw new NotSupportedException("Not executed: no suitable EPPlus commercial/evaluation license configured. NonCommercial was not asserted.");
                    var result = LibraryProbes.Run(library, operation, bytes, spec);
                    using (var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write)) file.Write(result);
                    timer.Stop(); var allocated = GC.GetTotalAllocatedBytes() - allocation;
                    var actual = PackageInspector.Read(output);
                    var differences = IntegrityOracle.Check(baseline, actual, spec, operation, library);
                    var observations = actual.Values.Where(p => !baseline.Values.ContainsKey(p.Key) && (p.Key.StartsWith("custom/") || p.Key.StartsWith("column/"))).Select(p => $"Added {p.Key}: {p.Value}").ToArray();
                    results.Add(new(spec.FileName, library, operation, differences.Length == 0 ? "Passed" : "Failed", PackageInspector.Hash(source) == sourceHash, timer.ElapsedMilliseconds, allocated, GC.GetTotalMemory(false), Path.GetRelativePath(root, output), differences, actual.SchemaErrors, baseline.PartHashes.Keys.Except(actual.PartHashes.Keys).ToArray(), observations, null));
                }
                catch (NotSupportedException e) { results.Add(new(spec.FileName, library, operation, "NotExecuted", PackageInspector.Hash(source) == sourceHash, timer.ElapsedMilliseconds, 0, 0, null, [], [], [], [], e.Message)); }
                catch (Exception e) { results.Add(new(spec.FileName, library, operation, "Error", PackageInspector.Hash(source) == sourceHash, timer.ElapsedMilliseconds, GC.GetTotalAllocatedBytes() - allocation, GC.GetTotalMemory(false), File.Exists(output) ? Path.GetRelativePath(root, output) : null, [], [], [], [], e.ToString())); }
                Console.WriteLine($"{spec.FileName} {library} {operation}: {results[^1].Status} ({results[^1].Differences.Length} differences)");
                if (!results[^1].SourceUnchanged) throw new InvalidOperationException("Source fixture changed; aborting evaluation.");
            }
        }
        var report = Path.Combine(run, "results.json");
        File.WriteAllText(report, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Results: " + report);
        foreach (var group in results.GroupBy(r => r.Status)) Console.WriteLine($"{group.Key}: {group.Count()}");
        // Compatibility gaps are evidence, not harness crashes. Statuses must be reviewed individually.
        return results.Any(r => r.Status == "Error" || !r.SourceUnchanged) ? 1 : 0;
    }

    public static string FindRoot()
    {
        var folder = new DirectoryInfo(Environment.CurrentDirectory);
        while (folder != null && !File.Exists(Path.Combine(folder.FullName, "TsekSheet.sln"))) folder = folder.Parent;
        return folder?.FullName ?? throw new DirectoryNotFoundException("Run from repository root or a descendant.");
    }

    private static int Benchmark(string root, string library)
    {
        if (!LibraryProbes.Libraries.Contains(library)) throw new ArgumentException("Unrecognized benchmark library.");
        var folder = Path.Combine(root, "artifacts", "workbook-evaluation", "benchmark-" + library + "-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var spec = new FixtureSpec("large-synthetic.xlsx", "Large Runs", 10005, "Alternate Runs", 14);
        var input = Path.Combine(folder, spec.FileName); FixtureFactory.Create(input, spec);
        var sourceHash = PackageInspector.Hash(input); var bytes = File.ReadAllBytes(input);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var managedBefore = GC.GetTotalMemory(true); var allocatedBefore = GC.GetTotalAllocatedBytes(); var watch = Stopwatch.StartNew();
        var outputBytes = LibraryProbes.Run(library, "roundtrip", bytes, spec);
        watch.Stop(); var allocated = GC.GetTotalAllocatedBytes() - allocatedBefore;
        var managedAfter = GC.GetTotalMemory(false); var workingSetAfter = Process.GetCurrentProcess().WorkingSet64;
        var output = Path.Combine(folder, "output.xlsx"); using (var file = new FileStream(output, FileMode.CreateNew)) file.Write(outputBytes);
        var actual = PackageInspector.Read(output); var differences = IntegrityOracle.Check(PackageInspector.Read(input), actual, spec, "roundtrip", library);
        var result = new { Library = library, DataRowsOnMainSheet = 10000, InputBytes = bytes.Length, OutputBytes = outputBytes.Length, ElapsedMilliseconds = watch.ElapsedMilliseconds, AllocatedBytes = allocated, ManagedBefore = managedBefore, ManagedAfter = managedAfter, WorkingSetAfter = workingSetAfter, SourceUnchanged = PackageInspector.Hash(input) == sourceHash, Differences = differences, SchemaErrors = actual.SchemaErrors, Limitation = "Single cold library operation after SDK fixture generation in a fresh process; includes load/save/reopen. Not peak memory, steady state, or a fair speed ranking. Inspector excluded from timing." };
        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }); File.WriteAllText(Path.Combine(folder, "benchmark.json"), json); Console.WriteLine(json); Console.WriteLine(folder);
        return result.SourceUnchanged && differences.Length == 0 ? 0 : 1;
    }
}
