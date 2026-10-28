using System.ComponentModel;
using ModelContextProtocol.Server;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tools;

[McpServerToolType]
public sealed class InstanceTools(VsSession session)
{
    [McpServerTool(Name = "vs_list_instances", ReadOnly = true)]
    [Description(
        "Lists running Visual Studio 2022/2026 instances: pid, version, open solution, debugger mode " +
        "(design = not debugging, run, break) and whether the instance was selected with vs_select_instance. " +
        "When exactly one instance is running, the other tools use it automatically.")]
    public Task<List<InstanceInfo>> ListInstances() => session.ListAsync();

    [McpServerTool(Name = "vs_select_instance")]
    [Description("Selects the Visual Studio instance the other tools act on. Needed only when several instances are running.")]
    public Task<InstanceInfo> SelectInstance(
        [Description("Process id (pid) from vs_list_instances.")] int pid) =>
        session.SelectAsync(pid);
}
