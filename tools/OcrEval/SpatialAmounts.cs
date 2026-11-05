using System.Text.RegularExpressions;
namespace TsekSheet.OcrEval;

// Associate labels and values by row/column structure, never value magnitude.
public static class SpatialAmounts
{
    private sealed record Span(int Start,int End,double Left,double Right,string Field,string Label,string Value);
    private static readonly (string Field,string Label,string Pattern)[] Patterns = [
        ("amount_due","total_amount_due",@"\bTOTAL\s*AMOUNT\s*DUE\b"),
        ("amount_due","net_amount_due",@"\bNET(?:\s+(?:AMOUNT|PAYMENT))?(?:\s+DUE)?\b"),
        ("amount_due","amount_due",@"\b(?:AMOUNT|BALANCE)\s+DUE\b"),
        ("vatable","vatable_sales",@"\bVAT[ -]?ABLE(?:\s*(?:SALES|AMOUNT))?\b"),
        ("__boundary","ignored_column",@"\b(?:VAT[ -]*EXEMPT(?:\s*SALES?)?|(?:VAT\s*)?ZERO\s*RATED(?:\s*SALES?)?)\b"),
        ("wht","withholding_tax",@"\b(?:TOTAL\s+)?(?:WITHHOLDING\s+TAX|TAX\s+WITHHELD|CREDITABLE\s+WITHHOLDING\s+TAX|WHT|EWT|CWT)\b"),
        ("vat","vat_amount",@"\b(?:VAT(?:\s*AMOUNT)?|VALUE[ -]ADDED\s+TAX)\b"),
        ("total","total",@"\b(?:TOTAL\s+SALES(?:\s*\(VAT\s+INCLUSIVE\))?|GRAND\s+TOTAL|GROSS\s+(?:AMOUNT|TOTAL)|TOTAL(?:\s+AMOUNT)?)(?!\s+(?:QTY|QUANTITY|DUE|ITEMS))\b") ];
    public static void Apply(OcrResult result,Dictionary<string,string> fields,Dictionary<string,string> labels)
    {
        var rows=new List<List<WordBox>>();
        var words=result.Words.Count>0 ? result.Words : TextWords(result.Text);
        foreach(var word in words.OrderBy(w=>w.Y+w.Height/2))
        {
            var center=word.Y+word.Height/2;
            var row=rows.FirstOrDefault(r=>Math.Abs(r.Average(w=>w.Y+w.Height/2)-center)<=Math.Max(3,.45*Math.Min(word.Height,r.Average(w=>w.Height))));
            if(row is null)rows.Add(row=[]);row.Add(word);
        }
        var proposals=new Dictionary<string,List<(string Value,string Label)>>(); var ambiguous=new HashSet<string>();
        var parsed=rows.Select(r=>Parse(r.OrderBy(w=>w.X).ToArray())).ToArray();
        void Add(Span label,Span cash){if(!proposals.TryGetValue(label.Field,out var values))proposals[label.Field]=values=[];values.Add((cash.Value,label.Label));}
        for(var n=0;n<parsed.Length;n++)
        {
            var (ls,cs)=parsed[n]; if(ls.Length==0)continue;
            if(cs.Length>0)
            {
                var sequence=ls.Concat(cs).OrderBy(s=>s.Left).ToArray();
                if(ls.Length==cs.Length && sequence.Select((s,i)=>(s.Field.Length>0)==(i%2==0)).All(x=>x))
                    for(var i=0;i<sequence.Length;i+=2)Add(sequence[i],sequence[i+1]);
                else if(ls.Length==cs.Length && sequence.Select((s,i)=>(s.Field.Length==0)==(i%2==0)).All(x=>x))
                    for(var i=0;i<sequence.Length;i+=2)Add(sequence[i+1],sequence[i]);
                else foreach(var l in ls)ambiguous.Add(l.Field);
                continue;
            }
            // A header row may label the numeric row immediately underneath.
            var next=n+1;
            while(next<parsed.Length && next<=n+4 && parsed[next].Cash.Length==0 && parsed[next].Labels.Length==0)next++;
            if(next>=parsed.Length || next>n+4 || parsed[next].Labels.Length>0)continue;
            var below=rows[next];var above=rows[n];
            if(below.Min(w=>w.Y)-above.Max(w=>w.Y+w.Height)>5*above.Average(w=>w.Height))continue;
            var last=next;
            while(last+1<parsed.Length && parsed[last+1].Cash.Length>0
                && (parsed[last+1].Labels.Length==0 || parsed[last+1].Labels.All(l=>l.Field=="total"))
                && rows[last+1].Min(w=>w.Y)-rows[last].Max(w=>w.Y+w.Height)<=3*above.Average(w=>w.Height))
            {
                last++;
            }
            for(var j=0;j<ls.Length;j++)
            {
                var left=j==0?double.NegativeInfinity:(ls[j-1].Right+ls[j].Left)/2;
                var right=j==ls.Length-1?double.PositiveInfinity:(ls[j].Right+ls[j+1].Left)/2;
                var dataRows=Enumerable.Range(next,last-next+1).ToArray();
                // Multiple body rows require an explicit aggregate row, even when
                // their numbers happen to be equal. Never take the first item.
                if(dataRows.Length>1)
                {
                    var totals=dataRows.Where(r=>parsed[r].Labels.Any(l=>l.Field=="total")).ToArray();
                    if(totals.Length!=1){ambiguous.Add(ls[j].Field);continue;}
                    dataRows=totals;
                }
                var candidates=dataRows.SelectMany(r=>parsed[r].Cash).Where(c=>(c.Left+c.Right)/2>left && (c.Left+c.Right)/2<right).ToArray();
                if(candidates.Length==1)Add(ls[j],candidates[0]);else if(candidates.Length>1)ambiguous.Add(ls[j].Field);
            }
        }
        foreach(var field in Patterns.Select(p=>p.Field).Where(f=>!f.StartsWith("__",StringComparison.Ordinal)).Distinct())
        {
            fields[field]=""; labels.Remove(field);
            if(ambiguous.Contains(field)){labels[field]="ambiguous";continue;}
            if(!proposals.TryGetValue(field,out var values))continue;
            if(values.Select(v=>v.Value).Distinct().Count()!=1 || values.Select(v=>v.Label).Distinct().Count()!=1){labels[field]="ambiguous";continue;}
            fields[field]=values[0].Value;labels[field]=values[0].Label;
        }
    }
    private static (Span[] Labels,Span[] Cash) Parse(WordBox[] words)
    {
        var text="";var offsets=new List<(int Start,WordBox Word)>();
        foreach(var word in words)
        {
            if(text.Length>0)
            {
                var previous=offsets[^1].Word;
                text+=word.X-(previous.X+previous.Width)>3*Math.Max(previous.Height,word.Height)?" | ":" ";
            }
            offsets.Add((text.Length,word));text+=word.Text;
        }
        double X(int index){var p=offsets.LastOrDefault(o=>o.Start<=index);return p.Word is null?0:p.Word.X+Math.Clamp((index-p.Start)/(double)Math.Max(1,p.Word.Text.Length),0,1)*p.Word.Width;}
        var labels=new List<Span>();
        foreach(var p in Patterns)foreach(Match match in Regex.Matches(text,p.Pattern,RegexOptions.IgnoreCase))
            if(!labels.Any(l=>match.Index<l.End && match.Index+match.Length>l.Start))labels.Add(new(match.Index,match.Index+match.Length,X(match.Index),X(match.Index+match.Length),p.Field,p.Label,""));
        var cash=new List<Span>();
        foreach(Match m in Regex.Matches(text,@"(?<![\d.])(?:PHP|P|₱)?\s*(?<number>\(?-?\d[\d,]*\.\d{2}\)?)(?![\d%])",RegexOptions.IgnoreCase))
        {
            if(labels.Any(l=>m.Index<l.End && m.Index+m.Length>l.Start))continue;
            var raw=m.Groups["number"].Value;
            var value=raw.StartsWith('(')?"-"+raw.Trim('(',')'):raw;
            var number=m.Groups["number"];
            var amount=Extraction.NormalizeAmount(value);if(amount.Length>0)cash.Add(new(number.Index,number.Index+number.Length,X(number.Index),X(number.Index+number.Length),"","",amount));
        }
        return(labels.OrderBy(l=>l.Left).ToArray(),cash.OrderBy(c=>c.Left).ToArray());
    }
    private static IReadOnlyList<WordBox> TextWords(string text)=>text.Split('\n').SelectMany((line,y)=>Regex.Matches(line,@"\S+").Select(m=>new WordBox(m.Value,1,m.Index*10,y*30,m.Length*10,20))).ToArray();
}
