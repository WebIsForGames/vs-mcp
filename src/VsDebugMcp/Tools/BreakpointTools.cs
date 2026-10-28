using System.ComponentModel;
using ModelContextProtocol.Server;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tools;

[McpServerToolType]
public sealed class BreakpointTools(DebuggerService debugger)
{
    [McpServerTool(Name = "vs_set_breakpoint")]
    [Description(
        "Sets a line breakpoint in Visual Studio. Works before and during debugging. " +
        "If a breakpoint already exists at the line, returns it with alreadyExisted = true. " +
        "bindState tells whether the debugger found code for the line: notDebugging, bound or unbound; " +
        "boundLines are the lines it actually bound to.")]
    public Task<BreakpointInfo> SetBreakpoint(
        [Description("Absolute path of the source file.")] string file,
        [Description("1-based line number; use a line with executable code.")] int line,
        [Description("Optional C++ condition; the breakpoint stops only when it is true, e.g. \"i == 42\".")] string? condition = null) =>
        debugger.SetBreakpointAsync(file, line, condition);

    [McpServerTool(Name = "vs_remove_breakpoint", Destructive = true)]
    [Description("Removes the breakpoint(s) at a file line and returns what was removed.")]
    public Task<List<BreakpointInfo>> RemoveBreakpoint(
        [Description("Absolute path of the source file.")] string file,
        [Description("1-based line number the breakpoint was set on.")] int line) =>
        debugger.RemoveBreakpointAsync(file, line);

    [McpServerTool(Name = "vs_list_breakpoints", ReadOnly = true)]
    [Description("Lists all breakpoints in Visual Studio, including ones the user set.")]
    public Task<List<BreakpointInfo>> ListBreakpoints() => debugger.ListBreakpointsAsync();
}
