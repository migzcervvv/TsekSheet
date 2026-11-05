using System.Text;
using System.Text.RegularExpressions;

namespace TsekSheet.OcrEval;

public static class Metrics
{
    public static string Normalize(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormKC).ToUpperInvariant().Trim(), @"\s+", " ");
    public static int Distance(string reference, string actual)
    {
        var a = reference.EnumerateRunes().ToArray(); var b = actual.EnumerateRunes().ToArray();
        var previous = Enumerable.Range(0, b.Length + 1).ToArray(); var current = new int[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++) current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
    public static double Cer(string reference, string actual) => (double)Distance(reference, actual) / Math.Max(1, reference.EnumerateRunes().Count());
    public static string FieldText(ReceiptFields fields) => string.Join("\n", fields.Values.Select(Normalize));
}
