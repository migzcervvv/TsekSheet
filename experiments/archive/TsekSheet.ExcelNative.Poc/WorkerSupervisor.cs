using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace TsekSheet.ExcelNative.Poc;

public static class WorkerSupervisor
{
    public static async Task<SupervisorResult> Run(ExperimentRequest request,TimeSpan? timeout=null,CancellationToken token=default)
    {
        ExperimentPaths.Validate(request);
        token.ThrowIfCancellationRequested();
        ExperimentPaths.WriteNew(Path.Combine(request.Root,"request.json"),request);
        var runtimeDirectory=new DirectoryInfo(Path.GetDirectoryName(typeof(object).Assembly.Location)!);
        var host=Path.Combine(runtimeDirectory.Parent!.Parent!.Parent!.FullName,"dotnet.exe");
        if(!File.Exists(host))throw new ExperimentException("FIXED_RUNTIME_HOST_UNAVAILABLE");
        var start=new ProcessStartInfo(host) { UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=ExperimentPaths.RepositoryRoot };
        start.ArgumentList.Add(typeof(WorkerSupervisor).Assembly.Location);start.ArgumentList.Add("--worker");start.ArgumentList.Add(request.Root);
        using var process=new Process { StartInfo=start };
        var watch=Stopwatch.StartNew();
        if(!process.Start())return new(null,false,"WORKER_START_FAILED",null,watch.ElapsedMilliseconds,0);
        using var reading=new CancellationTokenSource();
        var output=ReadBounded(process.StandardOutput,262144,reading.Token);
        var errors=ReadBounded(process.StandardError,4096,reading.Token);
        var metrics=Monitor(process,reading.Token);
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(timeout??TimeSpan.FromSeconds(45));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            var text=await output;var stderr=await errors;
            var result=JsonSerializer.Deserialize<WorkerResult>(text);
            ExperimentPaths.WriteNew(Path.Combine(request.Root,"native-worker-response.json"),result);
            var code=result==null?"INVALID_WORKER_RESPONSE":result.Code;
            if(stderr.Length>0 && result==null)code="WORKER_DIAGNOSTIC_FAILURE";
            return new(result,result==null||!result.CleanupConfirmed,code,process.ExitCode,watch.ElapsedMilliseconds,await metrics);
        }
        catch(Exception error) when(error is OperationCanceledException or JsonException or ExperimentException or IOException)
        {
            // No automatic retry. Never terminate Excel from this supervisor.
            // Only this exact child worker can be stopped after a cleanup grace.
            File.WriteAllText(Path.Combine(request.Root,"cancel"),"cancel");
            using var grace=new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try{await process.WaitForExitAsync(grace.Token);}catch(OperationCanceledException){if(!process.HasExited)process.Kill(false);await process.WaitForExitAsync();}
            reading.Cancel();
            try{await output;}catch(Exception){}try{await errors;}catch(Exception){}
            return new(null,true,error is OperationCanceledException?(token.IsCancellationRequested?"CANCELLED_OUTCOME_UNKNOWN":"TIMEOUT_OUTCOME_UNKNOWN"):"INVALID_RESPONSE_OUTCOME_UNKNOWN",process.ExitCode,watch.ElapsedMilliseconds,await metrics);
        }
    }
    private static async Task<long> Monitor(Process process,CancellationToken token)
    {
        long peak=0;
        while(!token.IsCancellationRequested)
        {
            try{if(process.HasExited)break;process.Refresh();peak=Math.Max(peak,process.PeakWorkingSet64);}
            catch(Exception error)when(error is InvalidOperationException or System.ComponentModel.Win32Exception){break;}
            try{await Task.Delay(25,token);}catch(OperationCanceledException){break;}
        }
        return peak;
    }
    private static async Task<string> ReadBounded(StreamReader reader,int maximum,CancellationToken token)
    {
        var result=new StringBuilder();var buffer=new char[4096];int count;
        while((count=await reader.ReadAsync(buffer,token))>0){if(result.Length+count>maximum)throw new ExperimentException("WORKER_OUTPUT_LIMIT");result.Append(buffer,0,count);}
        return result.ToString();
    }
}
