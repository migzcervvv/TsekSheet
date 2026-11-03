using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace TsekSheet.Application.Documents;

public sealed record RenderedDocumentPage(int PageNumber, int PageCount, int Width, int Height, string PngBase64);
public sealed record SourceRenderedPage(LocalDocumentVersion Source, RenderedDocumentPage Page);
public interface ILocalDocumentRenderer
{
    Task<RenderedDocumentPage> RenderAsync(byte[] bytes, LocalDocumentKind kind, int page, CancellationToken token);
}

// One process per page, no shell, paths, temporary files, extraction or workbook access.
public sealed class LocalDocumentRenderer(string workerPath, TimeSpan? timeout = null) : ILocalDocumentRenderer
{
    public async Task<RenderedDocumentPage> RenderAsync(byte[] bytes, LocalDocumentKind kind, int page, CancellationToken token)
    {
        if(bytes is not {Length:>=8 and <=33554432} || page is <1 or >1000 || !Enum.IsDefined(kind))
            throw Error("RENDER_LIMIT","Document or page exceeds the supported rendering limits.");
        token.ThrowIfCancellationRequested();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout??TimeSpan.FromSeconds(20));
        using var process=new Process{StartInfo=new ProcessStartInfo(Path.GetFullPath(workerPath)){
            UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true}};
        using var job=new RenderJob();
        Task<string>? output=null,error=null;
        try
        {
            if(!process.Start())throw Error("RENDER_UNAVAILABLE","Local renderer could not start.");
            job.Attach(process);
            output=ReadBounded(process.StandardOutput,16780000,deadline.Token);
            error=ReadBounded(process.StandardError,2048,deadline.Token);
            var header=new byte[12];
            BitConverter.GetBytes(page).CopyTo(header,0);BitConverter.GetBytes((int)kind).CopyTo(header,4);BitConverter.GetBytes(bytes.Length).CopyTo(header,8);
            await process.StandardInput.BaseStream.WriteAsync(header,deadline.Token);
            await process.StandardInput.BaseStream.WriteAsync(bytes,deadline.Token);
            process.StandardInput.Close();
            await process.WaitForExitAsync(deadline.Token);
            var json=await output;await error;
            if(process.ExitCode!=0)throw Error("RENDER_REJECTED","Document is malformed, encrypted, unsupported or exceeds rendering limits.");
            var result=JsonSerializer.Deserialize<RenderedDocumentPage>(json,new JsonSerializerOptions(JsonSerializerDefaults.Web){UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow});
            if(result==null || result.PageNumber!=page || result.PageCount is <1 or >1000 || page>result.PageCount
                || result.Width is <1 or >1600 || result.Height is <1 or >1600 || result.PngBase64.Length>16777216)
                throw Error("RENDER_REJECTED","Local renderer returned an invalid page.");
            var png=Convert.FromBase64String(result.PngBase64);
            if(png.Length<24 || !png.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})
                || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16,4))!=result.Width
                || System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20,4))!=result.Height)
                throw Error("RENDER_REJECTED","Local renderer returned an invalid PNG.");
            deadline.Token.ThrowIfCancellationRequested();return result;
        }
        catch(OperationCanceledException)
        {
            if(token.IsCancellationRequested)throw;
            throw Error("RENDER_TIMEOUT","Local rendering timed out. The worker was stopped.");
        }
        catch(DocumentSourceException){throw;}
        catch{throw Error("RENDER_REJECTED","Local rendering failed. No document or workbook was changed.");}
        finally
        {
            deadline.Cancel();
            try{if(!process.HasExited)process.Kill(true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
            try{await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3));}catch{}
            if(output!=null && error!=null){try{await Task.WhenAll(output,error).WaitAsync(TimeSpan.FromSeconds(1));}catch{}}
        }
    }
    private static async Task<string> ReadBounded(StreamReader reader,int limit,CancellationToken token)
    {
        var text=new StringBuilder();var buffer=new char[16384];int n;
        while((n=await reader.ReadAsync(buffer,token))!=0)
        {if(text.Length+n>limit)throw Error("RENDER_LIMIT","Renderer output exceeds its limit.");text.Append(buffer,0,n);}
        return text.ToString();
    }
    private static DocumentSourceException Error(string code,string message)=>new(code,message);

    // Assignment occurs before any source bytes are sent. Fail closed if limits cannot be set.
    private sealed class RenderJob : SafeHandleZeroOrMinusOneIsInvalid
    {
        public RenderJob():base(true)
        {
            SetHandle(CreateJobObject(IntPtr.Zero,null));
            if(IsInvalid)throw Error("RENDER_UNAVAILABLE","Renderer resource limits are unavailable.");
            var limits=new Extended{Basic=new Basic{Flags=0x2000|0x100|0x8,ActiveProcesses=1},ProcessMemory=(UIntPtr)(512*1024*1024)};
            if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<Extended>()))
            {Dispose();throw Error("RENDER_UNAVAILABLE","Renderer resource limits could not be set.");}
        }
        public void Attach(Process process)
        {if(!AssignProcessToJobObject(handle,process.Handle))throw Error("RENDER_UNAVAILABLE","Renderer could not enter its bounded job.");}
        protected override bool ReleaseHandle()=>CloseHandle(handle);
        [StructLayout(LayoutKind.Sequential)] private struct Basic
        {public long ProcessTime,JobTime;public uint Flags;public UIntPtr MinWorking,MaxWorking;public uint ActiveProcesses;public UIntPtr Affinity;public uint Priority,Scheduling;}
        [StructLayout(LayoutKind.Sequential)] private struct Io {public ulong A,B,C,D,E,F;}
        [StructLayout(LayoutKind.Sequential)] private struct Extended
        {public Basic Basic;public Io Io;public UIntPtr ProcessMemory,JobMemory,PeakProcess,PeakJob;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern IntPtr CreateJobObject(IntPtr attributes,string? name);
        [DllImport("kernel32.dll")]private static extern bool SetInformationJobObject(IntPtr job,int information,ref Extended limits,uint length);
        [DllImport("kernel32.dll")]private static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
        [DllImport("kernel32.dll")]private static extern bool CloseHandle(IntPtr handle);
    }
}
