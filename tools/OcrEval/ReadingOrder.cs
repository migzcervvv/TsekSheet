namespace TsekSheet.OcrEval;

internal static class ReadingOrder
{
    public static string Text(IEnumerable<WordBox> words)
    {
        var rows = new List<List<WordBox>>();
        foreach (var word in words.OrderBy(w => w.Y + w.Height / 2))
        {
            var center = word.Y + word.Height / 2;
            var row = rows.FirstOrDefault(r => Math.Abs(r.Average(w => w.Y + w.Height / 2) - center) <= Math.Max(8, Math.Max(word.Height, r.Average(w => w.Height)) * .6));
            if (row is null) rows.Add(row = []);
            row.Add(word);
        }
        return string.Join('\n', rows.Select(row => string.Join(' ', row.OrderBy(w => w.X).Select(w => w.Text))));
    }
}
