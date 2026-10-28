using System.Runtime.InteropServices;

namespace VsDebugMcp.Com;

/// <summary>
/// OLE message filter that retries calls Visual Studio rejects while it is busy
/// (RPC_E_CALL_REJECTED with SERVERCALL_RETRYLATER). Must be registered on an STA thread.
/// </summary>
[ComVisible(true)]
public sealed class MessageFilter : IOleMessageFilter
{
    private const int ServerCallIsHandled = 0;
    private const int ServerCallRetryLater = 2;
    private const int PendingMsgWaitDefProcess = 2;
    private const int CancelCall = -1;
    private const int RetryDelayMs = 100;
    private const int MaxRetryMs = 60_000;

    public static void Register() => CoRegisterMessageFilter(new MessageFilter(), out _);

    public static void Revoke() => CoRegisterMessageFilter(null, out _);

    int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) =>
        ServerCallIsHandled;

    int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) =>
        dwRejectType == ServerCallRetryLater && dwTickCount < MaxRetryMs ? RetryDelayMs : CancelCall;

    int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) =>
        PendingMsgWaitDefProcess;

    [DllImport("ole32.dll")]
    private static extern int CoRegisterMessageFilter(IOleMessageFilter? newFilter, out IOleMessageFilter? oldFilter);
}

[ComImport]
[Guid("00000016-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IOleMessageFilter
{
    [PreserveSig]
    int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);

    [PreserveSig]
    int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);

    [PreserveSig]
    int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
}
