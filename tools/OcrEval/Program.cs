using TsekSheet.OcrEval;

try
{
    if (args.Length == 3 && args[0] == "--worker") { await Worker.Run(args[1], args[2]); return 0; }
    var options = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i += 2)
    {
        if (i + 1 == args.Length || !new[] { "--generate", "--input", "--config", "--output", "--report", "--inspect" }.Contains(args[i]) || !options.TryAdd(args[i], args[i + 1]))
            throw new ArgumentException("Use --generate N [--input new-folder], or --input folder --config local.json [--output new-run-folder] [--report summary.md].");
    }
    if (options.TryGetValue("--generate", out var count))
    {
        var directory = Configuration.Local(options.GetValueOrDefault("--input", Path.Combine("artifacts", "ocr-eval", "synthetic-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ"))));
        Synthetic.Generate(directory, int.Parse(count, System.Globalization.CultureInfo.InvariantCulture)); Console.WriteLine(directory); return 0;
    }
    if (!options.TryGetValue("--input", out var input) || !options.TryGetValue("--config", out var config)) throw new ArgumentException("An input folder and local config are required.");
    using var cancelled = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancelled.Cancel(); };
    var output = options.GetValueOrDefault("--output", Path.Combine("artifacts", "ocr-eval", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N")[..8]));
    var report = options.GetValueOrDefault("--report", Path.Combine("docs", "ocr-bakeoff.md"));
    if (options.TryGetValue("--inspect", out var selected)) return await PrivateInspection.Run(input, config, output, selected, cancelled.Token);
    var header = Csv.Read(Path.Combine(input, "truth.csv"))[0];
    return header.Contains("page") ? await RealEvaluation.Run(input, config, output, report, cancelled.Token)
        : await Evaluation.Run(input, config, output, report, cancelled.Token);
}
catch (OperationCanceledException) { Console.Error.WriteLine("Evaluation cancelled; partial raw results are retained."); return 2; }
catch (Exception error) { Console.Error.WriteLine(error.ToString()); return 1; }
