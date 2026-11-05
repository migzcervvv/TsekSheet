namespace WorkbookLibrary.Evaluation;

public static class IntegrityOracle
{
    public static string[] Check(Snapshot baseline, Snapshot actual, FixtureSpec spec, string operation, string library)
    {
        var expected = new SortedDictionary<string, string>(baseline.Values, StringComparer.Ordinal);
        var main = spec.MainSheet;
        if (operation is "edit" or "deep-copy" or "replace-sheet") expected[$"cell/{main}/A5/content"] = "text:Edited synthetic value";
        if (operation == "deep-copy" && library != "OpenXmlSdk") expected[$"cell/{main}/B5/style"] = "number=0.00;bold=False;fill=solid:FFFF0000";
        if (operation == "create-sheet")
        {
            expected["workbook/sheetOrder"] += ";New Sheet";
            expected["sheet/New Sheet/state"] = "visible"; expected["sheet/New Sheet/merges"] = "";
            expected["cell/New Sheet/A1/content"] = "text:Fresh synthetic sheet";
            expected["cell/New Sheet/A1/style"] = baseline.Values[$"cell/{main}/A5/style"];
        }
        if (operation == "copy-sheet")
        {
            expected["workbook/sheetOrder"] += ";Working Copy";
            foreach (var (key, value) in baseline.Values.Where(p => p.Key.StartsWith($"cell/{main}/") || p.Key.StartsWith($"sheet/{main}/") || p.Key.StartsWith($"height/{main}/") || p.Key.StartsWith($"column/{main}/")))
                expected[key.Replace($"/{main}/", "/Working Copy/")] = value.Replace($"'{main}'!", "'Working Copy'!");
            expected["cell/Working Copy/A5/content"] = "text:Edited synthetic value";
        }
        if (operation.StartsWith("insert-"))
        {
            var count = operation == "insert-once" ? 1 : 3;
            var moving = baseline.Values.Where(p => p.Key.StartsWith($"cell/{main}/") || p.Key.StartsWith($"height/{main}/")).ToArray();
            foreach (var (key, _) in moving) expected.Remove(key);
            foreach (var (key, value) in moving)
            {
                if (key.StartsWith($"cell/{main}/") || key.StartsWith($"height/{main}/"))
                {
                    var parts = key.Split('/'); var address = parts[2];
                    var moved = PackageInspector.ShiftAddress(address, spec.SummaryRow, count);
                    expected[parts[0] == "height" ? $"height/{main}/{moved}" : $"cell/{main}/{moved}/{parts[3]}"] = value;
                }
            }
            var summary = spec.SummaryRow + count; var last = summary - 1;
            expected[$"cell/{main}/B{summary}/content"] = $"formula:SUM(B5:B{last})";
            expected[$"cell/{main}/C{summary}/content"] = $"formula:SUM(C5:C{last})";
            expected[$"cell/{main}/B{summary + 5}/content"] = $"formula:C{summary}+1";
            expected[$"cell/Overview/B2/content"] = $"formula:'{main}'!C{summary}";
            expected[$"cell/Overview/B4/content"] = $"formula:SUM('{main}'!B5:B{last})";
            expected["name/SummaryCell/global"] = $"'{main}'!$C${summary}";
            expected["name/DataValues/global"] = $"'{main}'!$B$5:$B${last}";
            expected[$"table/{main}/SyntheticTable1"] = $"A4:D{last}";
            expected[$"sheet/{main}/merges"] = string.Join(";", new[] { "A1:D1", $"A{summary + 3}:D{summary + 3}" }.Order());
            for (var row = spec.SummaryRow; row < summary; row++)
            {
                var contents = new[] { "text:Inserted synthetic row", "number:7", $"formula:B{row}*Lookup!$B$2", $"number:{43831 + row}" };
                for (var column = 0; column < 4; column++)
                {
                    var letter = (char)('A' + column);
                    expected[$"cell/{main}/{letter}{row}/content"] = contents[column];
                    expected[$"cell/{main}/{letter}{row}/style"] = baseline.Values[$"cell/{main}/{letter}{spec.SummaryRow - 1}/style"];
                }
                expected[$"height/{main}/{row}"] = "21";
            }
        }
        var failures = expected.Where(p => !actual.Values.TryGetValue(p.Key, out var found) || found != p.Value)
            .Select(p => $"{p.Key}: expected [{p.Value}], actual [{actual.Values.GetValueOrDefault(p.Key, "MISSING")}]").ToList();
        // Legitimate extra table/name records are allowed for copied/replacement worksheets only.
        // Added default widths/custom generator metadata are observations, not lost fixture content.
        // Pixel-level rendering of previously implicit widths is explicitly outside this oracle.
        failures.AddRange(actual.Values.Keys.Except(expected.Keys).Where(k => !k.StartsWith("column/") && !k.StartsWith("custom/") && !(operation is "copy-sheet" or "replace-sheet" && (k.StartsWith("table/") || k.StartsWith("name/"))))
            .Select(k => $"Unexpected {k}: [{actual.Values[k]}]"));
        if (operation is "copy-sheet" or "replace-sheet")
        {
            var target = operation == "copy-sheet" ? "Working Copy" : main;
            if (!actual.Values.Any(p => p.Key.StartsWith($"table/{target}/") && p.Value == $"A4:D{spec.SummaryRow - 1}")) failures.Add("Copied/replacement table missing or wrong extent.");
        }
        failures.AddRange(actual.SchemaErrors.Select(e => "Schema: " + e));
        return failures.ToArray();
    }
}
