using System.Runtime.InteropServices;

namespace TsekSheet.Excel;

internal static class BusyRetryPolicy
{
    internal const int MaxMilliseconds = 10_000;
    internal static int RetryDelay(int elapsed, int rejectType) =>
        rejectType is 1 or 2 && elapsed >= 0 && elapsed < MaxMilliseconds
            ? Math.Min(100, MaxMilliseconds - elapsed) : -1;
    internal static bool IsBusy(int hr) => hr is unchecked((int)0x80010001) or unchecked((int)0x8001010A);
}

[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMessageFilter
{
    [PreserveSig] int HandleInComingCall(int callType, nint caller, int tickCount, nint info);
    [PreserveSig] int RetryRejectedCall(nint callee, int tickCount, int rejectType);
    [PreserveSig] int MessagePending(nint callee, int tickCount, int pendingType);
}

[ComVisible(true)]
internal sealed class BusyMessageFilter : IMessageFilter, IDisposable
{
    private readonly nint previous;
    public BusyMessageFilter() => Marshal.ThrowExceptionForHR(CoRegisterMessageFilter(this, out previous));
    public int HandleInComingCall(int callType, nint caller, int tickCount, nint info) => 0;
    public int RetryRejectedCall(nint callee, int tickCount, int rejectType) => BusyRetryPolicy.RetryDelay(tickCount, rejectType);
    public int MessagePending(nint callee, int tickCount, int pendingType) => 2;
    public void Dispose()
    {
        Marshal.ThrowExceptionForHR(RestoreMessageFilter(previous, out var current));
        if (current != 0) Marshal.Release(current);
        if (previous != 0) Marshal.Release(previous);
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter([MarshalAs(UnmanagedType.Interface)] IMessageFilter filter, out nint oldFilter);
    [DllImport("ole32.dll", EntryPoint = "CoRegisterMessageFilter")]
    private static extern int RestoreMessageFilter(nint filter, out nint oldFilter);
}
