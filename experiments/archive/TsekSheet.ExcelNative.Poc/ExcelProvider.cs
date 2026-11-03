using System.Diagnostics;
using System.Runtime.InteropServices;

namespace TsekSheet.ExcelNative.Poc;

// Instantiated only in the isolated experimental worker. No production registration.
public sealed class ExcelProvider : IExperimentalWorkbookProvider
{
    private object? application;
    private object? workbook;
    private ExcelOwnership? ownership;
    private string? expectedOpenPath;
    private readonly Dictionary<string,long> timings=[];
    private readonly Dictionary<string,object> originalSettings=[];
    private long peak;

    public WorkerResult Execute(ExperimentRequest request)
    {
        ExperimentPaths.Validate(request);
        if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)throw new ExperimentException("STA_REQUIRED");
        var total=Stopwatch.StartNew();var stage="preflight";var status="failed";var code="UNSTARTED";
        var cleanup=false;var verified=false;var restored=false;var activationAttempted=false;
        var unchanged=request.Operation=="probe";var exact=false;var reopened=false;
        string? version=null,build=null;FileStream? baseline=null;
        try
        {
            if(request.Operation=="noop")
            {
                var path=Path.Combine(request.Root,"baseline.xlsx");
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new ExperimentException("INPUT_REPARSE_REJECTED");
                baseline=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
                if(baseline.Length>4*1024*1024)throw new ExperimentException("PACKAGE_FILE_LIMIT");
                if(ExperimentPaths.Hash(baseline)!=request.SourceSha256)throw new ExperimentException("STALE_SOURCE");
                SyntheticFixture.RequireBaselineHash(request.SourceSha256!);
                PackageEvidence.Read(path).RequireSynthetic();
                var working=Path.Combine(request.Root,"working.xlsx");
                using(var copy=new FileStream(working,FileMode.CreateNew,FileAccess.Write,FileShare.None)){baseline.Position=0;baseline.CopyTo(copy);copy.Flush(true);}
                exact=ExperimentPaths.Hash(working)==request.SourceSha256;
                if(!exact)throw new ExperimentException("WORKING_COPY_MISMATCH");
                if(File.Exists(Path.Combine(request.Root,"candidate.xlsx")))throw new ExperimentException("CANDIDATE_EXISTS");
            }
            ExperimentPaths.CheckCancellation(request.Root);
            stage="activation";var watch=Stopwatch.StartNew();
            var existingIds=new List<int>();
            foreach(var existing in Process.GetProcessesByName("EXCEL")){using(existing){_=existing.StartTime;existingIds.Add(existing.Id);}}
            var activationStarted=DateTime.UtcNow;
            var type=Type.GetTypeFromProgID("Excel.Application",true)!;
            activationAttempted=true;
            application=Activator.CreateInstance(type)??throw new ExperimentException("ACTIVATION_FAILED");
            dynamic app=application;
            var hwnd=new IntPtr((int)app.Hwnd);GetWindowThreadProcessId(hwnd,out var pid);
            if(pid==0||existingIds.Contains(checked((int)pid)))throw new ExperimentException("EXCEL_ISOLATION_NOT_ESTABLISHED");
            using(var process=Process.GetProcessById(checked((int)pid)))
            using(var self=Process.GetCurrentProcess())
            {
                if(!string.Equals(process.ProcessName,"EXCEL",StringComparison.OrdinalIgnoreCase)||process.StartTime.ToUniversalTime()<activationStarted.AddSeconds(-1)||process.SessionId!=self.SessionId)
                    throw new ExperimentException("EXCEL_IDENTITY_NOT_ESTABLISHED");
                ownership=new(process.Id,process.StartTime.ToUniversalTime().Ticks,hwnd.ToInt64(),process.SessionId,existingIds.ToArray());
            }
            EnsureOwnership();verified=true;
            ExperimentPaths.WriteNew(Path.Combine(request.Root,"ownership.json"),ownership);
            timings["activationAndOwnership"]=watch.ElapsedMilliseconds;
            stage="security";
            originalSettings["Visible"]=(bool)app.Visible;originalSettings["AutomationSecurity"]=(int)app.AutomationSecurity;
            originalSettings["EnableEvents"]=(bool)app.EnableEvents;originalSettings["AskToUpdateLinks"]=(bool)app.AskToUpdateLinks;
            originalSettings["DisplayAlerts"]=(bool)app.DisplayAlerts;originalSettings["ScreenUpdating"]=(bool)app.ScreenUpdating;
            app.Visible=false;app.AutomationSecurity=3;app.EnableEvents=false;app.AskToUpdateLinks=false;
            app.DisplayAlerts=true;app.ScreenUpdating=false;
            if((int)app.AutomationSecurity!=3||(bool)app.EnableEvents||(bool)app.AskToUpdateLinks)throw new ExperimentException("SECURITY_CONFIGURATION_REJECTED");
            version=(string)app.Version;build=Convert.ToString(app.Build,System.Globalization.CultureInfo.InvariantCulture);
            Sample();ExperimentPaths.CheckCancellation(request.Root);
            if(request.Operation=="noop")
            {
                stage="open-working-copy";watch.Restart();Open(Path.Combine(request.Root,"working.xlsx"),false);
                timings["openWorkingCopy"]=watch.ElapsedMilliseconds;
                if((int)app.Calculation!=-4135)throw new ExperimentException("MANUAL_CALCULATION_NOT_RETAINED");
                stage="serialize-noop";ExperimentPaths.CheckCancellation(request.Root);watch.Restart();EnsureOwnership();
                var candidate=Path.Combine(request.Root,"candidate.xlsx");
                if(File.Exists(candidate))throw new ExperimentException("CANDIDATE_EXISTS");
                ((dynamic)workbook!).SaveCopyAs(candidate);
                timings["serializeCandidate"]=watch.ElapsedMilliseconds;Sample();ExperimentPaths.CheckCancellation(request.Root);
                Journal("before-close-working");stage="close-working-copy";CloseBook();Journal("after-close-working");
                stage="reopen-candidate";watch.Restart();Open(candidate,true);
                timings["reopenCandidate"]=watch.ElapsedMilliseconds;reopened=true;
                Journal("before-close-candidate");CloseBook();Journal("after-close-candidate");
                unchanged=ExperimentPaths.Hash(baseline!)==request.SourceSha256;
                if(!unchanged)throw new ExperimentException("SOURCE_CHANGED");
            }
            status="completed";code="COMPLETED";
        }
        catch(Exception error)
        {
            code=error is ExperimentException ex?ex.Code:error is COMException com?"COM_"+unchecked((uint)com.HResult).ToString("X8"):
                error is UnauthorizedAccessException?"ACCESS_DENIED":error is IOException?"IO_FAILED":error.GetType().Name;
        }
        finally
        {
            try
            {
                if(verified)
                {
                    if(workbook!=null)CloseBook();
                    EnsureOwnership();Sample();Journal("before-restore-settings");
                    if(originalSettings.Count==6)
                    {
                        dynamic app=application!;
                        app.Visible=(bool)originalSettings["Visible"];app.AutomationSecurity=(int)originalSettings["AutomationSecurity"];
                        app.EnableEvents=(bool)originalSettings["EnableEvents"];app.AskToUpdateLinks=(bool)originalSettings["AskToUpdateLinks"];
                        app.DisplayAlerts=(bool)originalSettings["DisplayAlerts"];app.ScreenUpdating=(bool)originalSettings["ScreenUpdating"];
                        restored=(bool)app.Visible==(bool)originalSettings["Visible"]&&(int)app.AutomationSecurity==(int)originalSettings["AutomationSecurity"]
                            &&(bool)app.EnableEvents==(bool)originalSettings["EnableEvents"]&&(bool)app.AskToUpdateLinks==(bool)originalSettings["AskToUpdateLinks"]
                            &&(bool)app.DisplayAlerts==(bool)originalSettings["DisplayAlerts"]&&(bool)app.ScreenUpdating==(bool)originalSettings["ScreenUpdating"];
                    }
                    Journal("before-quit");((dynamic)application!).Quit();Journal("after-quit");Release(application);application=null;Journal("after-release-application");
                    cleanup=WaitForOwnedExit(ownership!,5000);
                    Journal("after-wait-exit");
                }
                else if(!activationAttempted)cleanup=true;
            }
            catch(Exception){cleanup=false;}
            finally
            {
                Release(workbook);workbook=null;Release(application);application=null;
                if(baseline!=null){try{unchanged=ExperimentPaths.Hash(baseline)==request.SourceSha256;}catch{unchanged=false;}baseline.Dispose();}
            }
        }
        if(!cleanup){status="invalidated";if(code=="COMPLETED")code=verified?"EXCEL_CLEANUP_UNCONFIRMED":"ISOLATION_OR_CLEANUP_UNCONFIRMED";}
        if(code=="COMPLETED"&&!restored){status="invalidated";code="INSTANCE_SETTINGS_NOT_RESTORED";}
        return new(status,code,verified,cleanup,unchanged,exact,reopened,ownership,stage,total.ElapsedMilliseconds,peak,version,build,timings,restored);
        void Journal(string point)=>ExperimentPaths.WriteNew(Path.Combine(request.Root,"lifecycle-"+point+".json"),new{Point=point,ElapsedMilliseconds=total.ElapsedMilliseconds,Stage=stage});
    }
    private void Open(string path,bool readOnly)
    {
        EnsureOwnership();dynamic app=application!;object? books=null;
        try
        {
            app.AutomationSecurity=3;books=app.Workbooks;
            workbook=((dynamic)books).Open(Filename:path,UpdateLinks:0,ReadOnly:readOnly,Password:"",WriteResPassword:"",IgnoreReadOnlyRecommended:true,Notify:false,AddToMru:false,Local:false,CorruptLoad:0);
            expectedOpenPath=path;EnsureOwnership();dynamic book=workbook!;
            if((bool)book.ReadOnly!=readOnly||!string.Equals((string)book.FullName,path,StringComparison.OrdinalIgnoreCase))throw new ExperimentException("WORKBOOK_IDENTITY_MISMATCH");
            object? sheets=null;
            try
            {
                sheets=book.Worksheets;
                if((int)((dynamic)sheets).Count!=5)throw new ExperimentException("WORKSHEET_COUNT_MISMATCH");
                for(var i=1;i<=5;i++)
                {
                    object? sheet=null;
                    try{sheet=((dynamic)sheets).Item[i];if((string)((dynamic)sheet).Name!=SyntheticFixture.SheetNames[i-1])throw new ExperimentException("WORKSHEET_ORDER_MISMATCH");}
                    finally{Release(sheet);}
                }
            }
            finally{Release(sheets);}
            Sample();
        }
        finally{Release(books);}
    }
    private void CloseBook()
    {
        EnsureOwnership();((dynamic)workbook!).Close(SaveChanges:false);Release(workbook);workbook=null;expectedOpenPath=null;
    }
    private void EnsureOwnership()
    {
        if(ownership==null||application==null)throw new ExperimentException("OWNERSHIP_REQUIRED");
        dynamic app=application;var hwnd=new IntPtr((int)app.Hwnd);GetWindowThreadProcessId(hwnd,out var pid);
        using var process=Process.GetProcessById(ownership.ProcessId);
        if(hwnd.ToInt64()!=ownership.WindowHandle||pid!=ownership.ProcessId||process.StartTime.ToUniversalTime().Ticks!=ownership.StartUtcTicks||process.SessionId!=ownership.SessionId)
            throw new ExperimentException("OWNERSHIP_CHANGED");
        object? books=null;object? current=null;
        try
        {
            books=app.Workbooks;
            if((int)((dynamic)books).Count!=(workbook==null?0:1))throw new ExperimentException("UNEXPECTED_WORKBOOKS_IN_INSTANCE");
            if(workbook!=null)
            {
                current=((dynamic)books).Item[1];
                if(!string.Equals((string)((dynamic)current).FullName,expectedOpenPath,StringComparison.OrdinalIgnoreCase))throw new ExperimentException("UNEXPECTED_WORKBOOK_IN_INSTANCE");
            }
        }
        finally{Release(current);Release(books);}
    }
    private void Sample(){using var process=Process.GetProcessById(ownership!.ProcessId);peak=Math.Max(peak,process.PeakWorkingSet64);}
    private static bool WaitForOwnedExit(ExcelOwnership identity,int milliseconds)
    {
        var watch=Stopwatch.StartNew();
        while(watch.ElapsedMilliseconds<milliseconds)
        {
            try{using var process=Process.GetProcessById(identity.ProcessId);if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=identity.StartUtcTicks)return true;}
            catch(ArgumentException){return true;}
            System.Windows.Forms.Application.DoEvents();MsgWaitForMultipleObjectsEx(0,IntPtr.Zero,25,0x04FF,0x0004);
        }
        return false;
    }
    private static void Release(object? value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(IntPtr hwnd,out uint pid);
    [DllImport("user32.dll")]private static extern uint MsgWaitForMultipleObjectsEx(uint count,IntPtr handles,uint timeout,uint mask,uint flags);
}
