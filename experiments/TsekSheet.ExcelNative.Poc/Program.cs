using System.Text.Json;

namespace TsekSheet.ExcelNative.Poc;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            if(args.Length==2&&args[0]=="--worker")return WorkerHost.Run(args[1]);
            if(args.Length==2&&args[0]=="review")
            {
                ExperimentPaths.ValidateRoot(args[1]);
                var source=Path.Combine(args[1],"baseline.xlsx");SyntheticFixture.RequireBaselineHash(ExperimentPaths.Hash(source));
                var before=PackageEvidence.Read(source);before.RequireSynthetic();
                var after=PackageEvidence.Read(Path.Combine(args[1],"candidate.xlsx"));after.RequireSynthetic();
                ExperimentPaths.WriteNew(Path.Combine(args[1],"comparison-v3.json"),PackageEvidence.Compare(before,after));
                ExperimentPaths.WriteNew(Path.Combine(args[1],"semantics-v3.json"),SemanticEvidence.Compare(before,after));
                var review=PreservationReview.Compare(before,after,ReadNative(args[1],"before"),ReadNative(args[1],"after"));
                ExperimentPaths.WriteNew(Path.Combine(args[1],"preservation-review-v3.json"),review);
                Console.WriteLine(JsonSerializer.Serialize(new{review.Qualified,review.AcceptedSerialization,review.RequiresReview,review.AlwaysFail,InsertionAuthorized=false}));return 0;
            }
            if(args.Length==2&&args[0]=="analyze")
            {
                ExperimentPaths.ValidateRoot(args[1]);
                var before=PackageEvidence.Read(Path.Combine(args[1],"baseline.xlsx"));before.RequireSynthetic();
                var after=PackageEvidence.Read(Path.Combine(args[1],"candidate.xlsx"));after.RequireSynthetic();
                var semantics=SemanticEvidence.Compare(before,after);
                ExperimentPaths.WriteNew(Path.Combine(args[1],"semantics.json"),semantics);
                Console.WriteLine(JsonSerializer.Serialize(new{semantics.Differences,semantics.FormulaTextPreserved,semantics.CachedResultsPreserved,semantics.WorksheetIdentityPreserved,semantics.RowLayoutPreserved,InsertionAuthorized=false}));return 0;
            }
            if(args.Length==1&&args[0]=="gate-a")return Run("gate-a").GetAwaiter().GetResult();
            if(args.Length is 2 or 3&&args[0]=="gate-b"){GateProgression.RequireGateA(args[1]);return Run("gate-b",args.Length==3?args[2]:SyntheticFixture.IncompleteTheme).GetAwaiter().GetResult();}
            throw new ExperimentException("USE_GATE_A_OR_GATE_B_WITH_GATE_A_ROOT");
        }
        catch(Exception error){Console.Error.WriteLine(error is ExperimentException ex?ex.Code:error.ToString());return 1;}
    }
    private static async Task<int> Run(string gate,string variant=SyntheticFixture.IncompleteTheme)
    {
        var root=ExperimentPaths.CreateRun();
        if(gate=="gate-b")SyntheticFixture.Create(root,variant);
        ExperimentPaths.WriteNew(Path.Combine(root,"fixture-manifest.json"),new{Variant=variant,Gate=gate});
        var request=new ExperimentRequest(1,gate=="gate-a"?"probe":"noop",root,gate=="gate-b"?ExperimentPaths.Hash(Path.Combine(root,"baseline.xlsx")):null);
        var outcome=await WorkerSupervisor.Run(request);
        ExperimentPaths.WriteNew(Path.Combine(root,"worker-result.json"),outcome);
        ComparisonReport? comparison=null;
        PreservationReviewReport? review=null;
        if(gate=="gate-b"&&outcome.Worker is { Status:"completed",CleanupConfirmed:true,SourceUnchanged:true,ExactWorkingCopy:true,CandidateReopened:true })
        {
            var before=PackageEvidence.Read(Path.Combine(root,"baseline.xlsx"));var after=PackageEvidence.Read(Path.Combine(root,"candidate.xlsx"));
            comparison=PackageEvidence.Compare(before,after);
            ExperimentPaths.WriteNew(Path.Combine(root,"comparison.json"),comparison);
            ExperimentPaths.WriteNew(Path.Combine(root,"semantics.json"),SemanticEvidence.Compare(before,after));
            review=PreservationReview.Compare(before,after);
            ExperimentPaths.WriteNew(Path.Combine(root,"preservation-review-v3.json"),review);
        }
        var passed=gate=="gate-a"?GateProgression.AllowsNoop(outcome):GateProgression.AllowsPreservedNoop(outcome,comparison,review);
        ExperimentPaths.WriteNew(Path.Combine(root,"gate-result.json"),new GateResult(gate,passed,false,outcome,comparison,review));
        Console.WriteLine(JsonSerializer.Serialize(new{Gate=gate,Passed=passed,Root=root,Code=outcome.Code,ComparisonQualified=comparison?.Qualified,InsertionAuthorized=false}));
        return passed?0:2;
    }
    private static NativeAppearanceReport? ReadNative(string root,string suffix)
    {
        var path=Path.Combine(root,"native-appearance-"+suffix+".json");if(!File.Exists(path))return null;
        if(new FileInfo(path).Length>65536||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ExperimentException("NATIVE_EVIDENCE_LIMIT");
        return JsonSerializer.Deserialize<NativeAppearanceReport>(File.ReadAllText(path))??throw new ExperimentException("NATIVE_EVIDENCE_INVALID");
    }
}
