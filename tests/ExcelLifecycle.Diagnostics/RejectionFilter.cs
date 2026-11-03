using System.Runtime.InteropServices;

namespace ExcelLifecycle.Diagnostics;

[ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMessageFilter
{
    [PreserveSig] int HandleInComingCall(int type, nint caller, int tick, nint info);
    [PreserveSig] int RetryRejectedCall(nint callee, int tick, int rejection);
    [PreserveSig] int MessagePending(nint callee, int tick, int type);
}

[ComVisible(true), ClassInterface(ClassInterfaceType.None)]
internal sealed class RejectionFilter : IMessageFilter, IDisposable
{
    private readonly nint previous;
    public RejectionFilter() => Marshal.ThrowExceptionForHR(Register(this, out previous));
    public int HandleInComingCall(int type, nint caller, int tick, nint info) => 0;
    public int RetryRejectedCall(nint callee, int tick, int rejection) => rejection is 1 or 2 && tick < 10_000 ? Math.Min(100, 10_000 - tick) : -1;
    public int MessagePending(nint callee, int tick, int type) => 2;
    public void Dispose()
    {
        Marshal.ThrowExceptionForHR(Restore(previous, out var current));
        if (current != 0) Marshal.Release(current);
        if (previous != 0) Marshal.Release(previous);
    }
    [DllImport("ole32.dll", EntryPoint = "CoRegisterMessageFilter")] private static extern int Register([MarshalAs(UnmanagedType.Interface)] IMessageFilter filter, out nint old);
    [DllImport("ole32.dll", EntryPoint = "CoRegisterMessageFilter")] private static extern int Restore(nint filter, out nint old);
}
