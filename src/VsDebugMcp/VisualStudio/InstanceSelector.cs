using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

/// <summary>Chooses which running Visual Studio instance the tools act on.</summary>
public static class InstanceSelector
{
    public static VsMoniker Select(IReadOnlyList<VsMoniker> running, int? selectedProcessId)
    {
        if (selectedProcessId is { } processId)
        {
            return running.FirstOrDefault(moniker => moniker.ProcessId == processId)
                ?? throw new McpException(
                    $"The selected Visual Studio instance (pid {processId}) is no longer running. " +
                    "Call vs_list_instances and vs_select_instance.");
        }

        return running.Count switch
        {
            0 => throw new McpException(
                "No running Visual Studio 2022/2026 instance was found. Open the solution in Visual Studio first. " +
                "A Visual Studio started as administrator is not visible to a non-elevated MCP server."),
            1 => running[0],
            _ => throw new McpException(
                $"Several Visual Studio instances are running: {string.Join(", ", running)}. " +
                "Call vs_select_instance with the pid of the one to debug."),
        };
    }
}
