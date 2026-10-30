using System.Globalization;
using TsekSheet.Application.Workbooks;

namespace TsekSheet.Workbooks;

internal sealed record InspectionNumberFormat(int Id, string Code, string Warning = "");

internal static class InspectionCellFormatting
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    internal static CellInterpretation Interpret(string raw, string kind, InspectionNumberFormat format, bool date1904, bool truncated, string formulaStatus)
    {
        CellInterpretation Result(string display, string type, string confidence, string warning = "") => new(raw, display, type, confidence, warning, formulaStatus);
        CellInterpretation Unsupported(string warning) => Result(raw, kind, "Unsupported", warning);
        if (truncated) return Unsupported("Stored text is truncated; no numeric interpretation attempted.");
        if (kind != "Number" || raw.Length == 0) return Result(raw, kind, "Stored");
        if (format.Warning.Length > 0) return Unsupported(format.Warning);
        var interpretedCode = UnescapeObservedLiterals(format.Code);
        var code = interpretedCode.ToLowerInvariant();
        var mode = format.Id switch { >= 14 and <= 17 => "Date", >= 18 and <= 21 => "Time", 22 => "DateTime", _ => "" };
        if (mode == "") mode = code switch
        {
            "yyyy-mm-dd" or "yyyy/mm/dd" or "dd/mm/yyyy" or "mm/dd/yyyy" or "d/m/yyyy" or "m/d/yyyy" or "dd-mmm-yyyy" => "Date",
            "yyyy-mm-dd hh:mm:ss" or "yyyy-mm-dd hh:mm" or "m/d/yy h:mm" => "DateTime",
            "hh:mm:ss" or "h:mm:ss" or "hh:mm" or "h:mm" or "h:mm am/pm" or "h:mm:ss am/pm" => "Time", _ => ""
        };
        if (mode == "" && code == "general") return Result(raw, "Number", "Stored");
        if (!decimal.TryParse(raw, NumberStyles.Float, Invariant, out var number)) return Unsupported("Invalid or out-of-range numeric value.");
        if (mode == "")
        {
            if (number.ToString("G29", Invariant).TrimStart('-').Replace(".", "").TrimStart('0').TrimEnd('0').Length > 15)
                return Unsupported("More than 15 significant stored digits; native display rounding is ambiguous. Raw value retained.");
            if (Accounting(number, interpretedCode, out var display)) return Result(display, "Number", "Supported subset", "Invariant accounting subset; native reference coverage is limited. Raw precision retained separately.");
            return Unsupported("Number format is outside the explicit display subset; raw value retained.");
        }
        if (number < 0 || number >= 2958466m) return Unsupported("Negative or out-of-range date/time serial; raw value retained.");
        var day = (int)decimal.Floor(number); var fraction = number - day;
        var millis = (long)decimal.Round(fraction * 86400000m, 0, MidpointRounding.AwayFromZero);
        if (millis == 86400000) { millis = 0; if (mode != "Date") day++; }
        var time = TimeSpan.FromMilliseconds(millis).ToString(@"hh\:mm\:ss\.fff", Invariant).TrimEnd('0').TrimEnd('.');
        var warning = "Normalized ISO/24-hour display, rounded to milliseconds; not Excel locale rendering.";
        if (mode == "Time") return Result(time, "Time", "Supported subset", warning + (day > 0 ? " Whole days omitted from time-only display." : ""));
        if (!date1904 && day == 0) return Unsupported("1900-system serial zero has no supported calendar date.");
        string date;
        if (!date1904 && day == 60) { date = "1900-02-29"; warning += " Fictitious Excel leap day; not a Gregorian date."; }
        else
        {
            try { date = (date1904 ? new DateTime(1904, 1, 1).AddDays(day) : new DateTime(1899, 12, 31).AddDays(day > 60 ? day - 1 : day)).ToString("yyyy-MM-dd", Invariant); }
            catch (ArgumentOutOfRangeException) { return Unsupported("Date serial exceeds the supported calendar range."); }
        }
        if (mode == "Date" && fraction != 0) warning += " Fractional time remains in the raw value.";
        return Result(mode == "DateTime" ? date + " " + time : date, mode, "Supported subset", warning);
    }

    private static string UnescapeObservedLiterals(string code)
    {
        var result = new System.Text.StringBuilder(); bool quoted = false;
        for (var i = 0; i < code.Length; i++)
        {
            var c = code[i];
            if (c == '"') quoted = !quoted;
            if (!quoted && c == '\\')
            {
                if (++i >= code.Length || code[i] is not ('(' or ')' or '-' or ' ')) return code;
                c = code[i];
            }
            result.Append(c);
        }
        return result.ToString();
    }

    private static bool Accounting(decimal number, string code, out string display)
    {
        display = ""; var sections = code.Split(';');
        if (sections.Length > 3) return false;
        // An exact grammar prevents passing arbitrary Excel syntax to the .NET formatter.
        const string pattern = "^(?:\\\"[$₱€£]\\\" ?|[$₱€£])?(?:#,##0|0)(?:\\.0{1,4})?%?$";
        bool Valid(string s) => System.Text.RegularExpressions.Regex.IsMatch(s, pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(20));
        if (!Valid(sections[0])) return false;
        if (sections.Length > 1)
        {
            var negative = sections[1];
            if (negative.StartsWith('(') && negative.EndsWith(')')) negative = negative[1..^1];
            else if (negative.StartsWith('-')) negative = negative[1..];
            if (!Valid(negative)) return false;
        }
        if (sections.Length == 3 && sections[2] is not ("" or "\"\"" or "\"-\"") && !Valid(sections[2])) return false;
        if (number == 0 && sections.Length == 3 && sections[2] is "" or "\"\"" or "\"-\"") { display = sections[2].Replace("\"", ""); return true; }
        var selected = number < 0 && sections.Length > 1 ? sections[1] : number == 0 && sections.Length == 3 ? sections[2] : sections[0];
        try
        {
            var magnitude = number < 0 && sections.Length > 1 ? decimal.Abs(number) : number;
            var percent = selected.Contains('%');
            var point = selected.IndexOf('.'); var places = 0;
            if (point >= 0) for (var i = point + 1; i < selected.Length && selected[i] == '0'; i++) places++;
            var rounded = decimal.Round(percent ? magnitude * 100 : magnitude, places, MidpointRounding.AwayFromZero);
            display = (percent ? rounded / 100 : rounded).ToString(selected, Invariant); return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException) { return false; }
    }
}
