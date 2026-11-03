using System.Runtime.InteropServices;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace TsekSheet.ExcelNative.Poc;

public static class WorkerHost
{
    public static int Run(string root)
    {
        ExperimentPaths.ValidateRoot(root);
        var requestPath=Path.Combine(root,"request.json");
        if(new FileInfo(requestPath).Length>16384)throw new ExperimentException("REQUEST_LIMIT");
        var request=JsonSerializer.Deserialize<ExperimentRequest>(File.ReadAllText(requestPath))??throw new ExperimentException("REQUEST_INVALID");
        if(request.Root!=root)throw new ExperimentException("REQUEST_ROOT_MISMATCH");
        ExperimentPaths.Validate(request);
        using var mutex=new Mutex(false,"Local\\TsekSheet.ExcelNative.Poc");
        if(!mutex.WaitOne(0))throw new ExperimentException("EXPERIMENT_BUSY");
        try
        {
            Forms.Application.OleRequired();
            var filter=new NoRetryMessageFilter();
            Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(filter,out var previous));
            try
            {
                WorkerResult? result=null;
                using var context=new Forms.ApplicationContext();
                EventHandler? idle=null;
                idle=(_,_)=>
                {
                    Forms.Application.Idle-=idle;
                    try{result=new ExcelProvider().Execute(request);ExperimentPaths.WriteNew(Path.Combine(root,"worker-provider-returned.json"),result);}
                    finally{context.ExitThread();}
                };
                Forms.Application.Idle+=idle;
                Forms.Application.Run(context);
                var json=JsonSerializer.Serialize(result??throw new ExperimentException("WORKER_NO_RESULT"));
                if(json.Length>262144)throw new ExperimentException("RESPONSE_LIMIT");
                Console.WriteLine(json);
                return result.Status=="completed"&&result.CleanupConfirmed?0:2;
            }
            finally{Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(previous,out _));GC.KeepAlive(filter);}
        }
        finally{mutex.ReleaseMutex();}
    }
    [ComImport,Guid("00000016-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMessageFilter
    {
        [PreserveSig]int HandleInComingCall(int type,IntPtr caller,int tick,IntPtr info);
        [PreserveSig]int RetryRejectedCall(IntPtr callee,int tick,int reject);
        [PreserveSig]int MessagePending(IntPtr callee,int tick,int pending);
    }
    [ComVisible(true)]
    private sealed class NoRetryMessageFilter : IMessageFilter
    {
        public int HandleInComingCall(int type,IntPtr caller,int tick,IntPtr info)=>0;
        public int RetryRejectedCall(IntPtr callee,int tick,int reject)=>-1;
        public int MessagePending(IntPtr callee,int tick,int pending)=>2;
    }
    [DllImport("ole32.dll")]private static extern int CoRegisterMessageFilter(IMessageFilter? filter,out IMessageFilter? previous);
}
