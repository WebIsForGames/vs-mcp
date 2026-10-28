using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

/// <summary>Turns automation failures into messages the model can act on.</summary>
internal static class ComErrors
{
    private const int RpcDisconnected = unchecked((int)0x80010108);
    private const int RpcServerUnavailable = unchecked((int)0x800706BA);
    private const int RpcCallRejected = unchecked((int)0x80010001);

    public static McpException ToToolError(Exception ex, VsMoniker instance) => ex.HResult switch
    {
        RpcDisconnected or RpcServerUnavailable =>
            new McpException($"{instance} is no longer available. Call vs_list_instances.", ex),
        RpcCallRejected =>
            new McpException($"{instance} is busy and rejected the call; a modal dialog may be open in Visual Studio. Close it and retry.", ex),
        _ =>
            new McpException($"{instance} automation call failed: {ex.Message} (HRESULT 0x{ex.HResult:X8}).", ex),
    };
}
