using System.ComponentModel;
using ModelContextProtocol.Server;
using VsDebugMcp.VisualStudio;

namespace VsDebugMcp.Tools;

[McpServerToolType]
public sealed class ExecutionTools(DebuggerService debugger)
{
    [McpServerTool(Name = "vs_start_debugging")]
    [Description(
        "Starts debugging the solution's startup project (like F5). By default builds the solution first and " +
        "returns the build errors instead of starting when the build fails. Returns the debugger state once it has started; " +
        "then call vs_wait_for_break.")]
    public Task<DebuggerState> StartDebugging(
        [Description("Build the solution before starting. With false, Visual Studio may show an 'out of date' dialog that blocks automation.")] bool build = true,
        CancellationToken cancellationToken = default) =>
        debugger.StartAsync(build, cancellationToken);

    [McpServerTool(Name = "vs_continue")]
    [Description(
        "Resumes execution from break mode (like F5 while stopped). Returns after Visual Studio has left the current break; " +
        "then call vs_wait_for_break to wait for the next stop.")]
    public Task<DebuggerState> Continue(CancellationToken cancellationToken = default) =>
        debugger.ContinueAsync(cancellationToken);

    [McpServerTool(Name = "vs_wait_for_break", ReadOnly = true)]
    [Description(
        "Waits until the debugger stops (breakpoint, exception, __debugbreak, step) or the program exits. Returns immediately if already stopped. " +
        "Result mode: break (with breakReason, location and breakpoint), design (not debugging: the program exited) or " +
        "run with timedOut = true (no stop yet; call again). Exception stops add exceptionMessage; stoppedIn is the frame " +
        "without source the program stopped in, when location is its caller.")]
    public Task<DebuggerState> WaitForBreak(
        [Description("Seconds to wait, 1-300. Default 60.")] int timeoutSeconds = DebuggerService.DefaultWaitSeconds,
        CancellationToken cancellationToken = default) =>
        debugger.WaitForBreakAsync(timeoutSeconds, cancellationToken);

    [McpServerTool(Name = "vs_get_state", ReadOnly = true)]
    [Description("Returns the current debugger mode (design, run, break) and, in break mode, the reason and source location.")]
    public Task<DebuggerState> GetState(CancellationToken cancellationToken = default) => debugger.GetStateAsync(cancellationToken);

    [McpServerTool(Name = "vs_get_call_stack", ReadOnly = true)]
    [Description(
        "Returns the call stack of the current thread in break mode: function, file, line and module of each frame, " +
        "the current frame first, then its callers. totalFrames larger than the number of frames returned means the list was cut at maxFrames.")]
    public Task<CallStack> GetCallStack(
        [Description("Frames to return, 1-100. Default 20.")] int maxFrames = DebuggerService.DefaultStackFrames) =>
        debugger.GetCallStackAsync(maxFrames);

    [McpServerTool(Name = "vs_stop_debugging", Destructive = true)]
    [Description("Stops debugging (like Shift+F5); a launched program is terminated.")]
    public Task<DebuggerState> StopDebugging(CancellationToken cancellationToken = default) =>
        debugger.StopAsync(cancellationToken);
}
