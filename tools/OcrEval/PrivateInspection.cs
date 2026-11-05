using System.Text.Json;
using OpenCvSharp;
using TsekSheet.Application.Documents;
namespace TsekSheet.OcrEval;

// Explicit local review only. Never used by the metrics path. Output must remain
// inside fixtures/redacted beside the input directory, which the repository ignores.
internal static class PrivateInspection
{
    public static async Task<int> Run(string input,string configPath,string output,string selected,CancellationToken token)
    {
        input=Configuration.Local(input); output=Configuration.Local(output); configPath=Configuration.Local(configPath);
        var privateRoot=Path.GetFullPath(Path.Combine(input,".."));
        var repo=new DirectoryInfo(input);while(repo is not null && !File.Exists(Path.Combine(repo.FullName,"GOALS.md")))repo=repo.Parent;
        if (repo is null || !privateRoot.Equals(Path.Combine(repo.FullName,"fixtures","redacted"),StringComparison.OrdinalIgnoreCase) || !output.StartsWith(privateRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || output.StartsWith(input+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || Directory.Exists(output)) throw new IOException("Private inspection requires a new directory under fixtures/redacted outside docs.");
        for(var parent=Path.GetDirectoryName(output);parent is not null && parent.Length>=privateRoot.Length;parent=Path.GetDirectoryName(parent))
            if(Directory.Exists(parent) && (File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked private-output folders are unavailable.");
        var config=Configuration.Load(configPath); var rows=Csv.Read(Path.Combine(input,"truth.csv")).Skip(1).Where(r=>r.Length==16 && r[0].Length>0).ToArray();
        var indices=selected.Split(',').Select(int.Parse).Distinct().ToArray();
        if(indices.Length!=3 || indices.Any(i=>i<1 || i>rows.Length))throw new ArgumentException("Select three packet ordinals.");
        Directory.CreateDirectory(output);
        var renderer=new LocalDocumentRenderer(config.Renderer,TimeSpan.FromSeconds(config.TimeoutSeconds));
        await using var orientation=new EngineClient(configPath,"tesseract-psm6",config);
        await using var tess=new EngineClient(configPath,"tesseract-psm6",config);
        await using var paddle=new EngineClient(configPath,"paddle-v5-mobile",config);
        foreach(var index in indices)
        {
            var row=rows[index-1]; var bytes=File.ReadAllBytes(Csv.InputPath(input,row[0]));var count=1;
            for(var p=1;p<=count;p++)
            {
                var page=await renderer.RenderAtDpiAsync(bytes,LocalDocumentKind.Pdf,p,300,token);count=page.PageCount;
                var png=Convert.FromBase64String(page.PngBase64);await orientation.Recognize(png,"orientation",token);
                using var source=Cv2.ImDecode(png,ImreadModes.Color); using var rotated=PacketRules.Rotate(source,orientation.LastRotation);
                png=rotated.ToBytes(".png");
                using var preview=new Mat();Cv2.Resize(rotated,preview,new(),Math.Min(1,1800d/rotated.Width),Math.Min(1,1800d/rotated.Width));
                File.WriteAllBytes(Path.Combine(output,$"packet-{index}-page-{p}.png"),preview.ToBytes(".png"));
                foreach(var (name,client) in new[]{("tess",tess),("paddle",paddle)})
                {
                    var result=await client.Recognize(png,"none",token);
                    File.WriteAllText(Path.Combine(output,$"packet-{index}-page-{p}-{name}.json"),JsonSerializer.Serialize(result.Result,Configuration.Json));
                }
                Console.WriteLine($"Private review packet {index}, page {p}/{count}.");
            }
        }
        return 0;
    }
}
