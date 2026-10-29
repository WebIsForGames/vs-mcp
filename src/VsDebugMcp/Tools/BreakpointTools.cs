using System.ComponentModel;
using ModelContextProtocol.Server;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tools;

[McpServerToolType]
public sealed class BreakpointTools(DebuggerService debugger)
{
    [McpServerTool(Name = "vs_set_breakpoint")]
    [Description(
        "Sets a line breakpoint, or a function breakpoint when function is given, in Visual Studio. Works before and during debugging. " +
        "If the breakpoint already exists, returns it with alreadyExisted = true. " +
        "bindState tells whether the debugger found code for it: notDebugging, bound or unbound; " +
        "boundLines are the lines it actually bound to.")]
    public Task<BreakpointInfo> SetBreakpoint(
        [Description("Absolute path of the source file; omit for a function breakpoint.")] string? file = null,
        [Description("1-based line number; use a line with executable code. Omit for a function breakpoint.")] int? line = null,
        [Description("Optional C++ condition; the breakpoint stops only when it is true, e.g. \"i == 42\".")] string? condition = null,
        [Description(
            "Function name for a function breakpoint instead of file and line, e.g. \"_wassert\" to stop on a failed " +
            "assert before the C runtime shows its dialog.")] string? function = null) =>
        debugger.SetBreakpointAsync(file, line, function, condition);

    [McpServerTool(Name = "vs_remove_breakpoint", Destructive = true)]
    [Description("Removes the breakpoint(s) at a file line, or a function breakpoint, and returns what was removed.")]
    public Task<List<BreakpointInfo>> RemoveBreakpoint(
        [Description("Absolute path of the source file; omit for a function breakpoint.")] string? file = null,
        [Description("1-based line number the breakpoint was set on; omit for a function breakpoint.")] int? line = null,
        [Description("Function name of a function breakpoint, instead of file and line.")] string? function = null) =>
        debugger.RemoveBreakpointAsync(file, line, function);

    [McpServerTool(Name = "vs_list_breakpoints", ReadOnly = true)]
    [Description("Lists all breakpoints in Visual Studio, including ones the user set.")]
    public Task<List<BreakpointInfo>> ListBreakpoints() => debugger.ListBreakpointsAsync();
}
