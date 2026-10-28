using EnvDTE;
using EnvDTE90a;

namespace VsDebugMcp.VisualStudio;

/// <summary>Reads the debugger state. Call only on the STA thread.</summary>
internal static class DebuggerSnapshot
{
    public static DebuggerState Capture(Debugger debugger)
    {
        var mode = debugger.CurrentMode;
        var process = ComValue.NullIfEmpty(ComValue.Try(() => debugger.CurrentProcess?.Name));
        if (mode != dbgDebugMode.dbgBreakMode)
        {
            return new DebuggerState(DebuggerText.Mode(mode), BreakReason: null, Location: null, Breakpoint: null, process);
        }

        var reason = debugger.LastBreakReason;
        return new DebuggerState(
            DebuggerText.Break,
            DebuggerText.Reason(reason),
            ComValue.Try(() => CurrentLocation(debugger)),
            reason == dbgEventReason.dbgEventReasonBreakpoint ? ComValue.Try(() => LastHit(debugger)) : null,
            process);
    }

    private static SourceLocation? CurrentLocation(Debugger debugger)
    {
        if (debugger.CurrentStackFrame is not StackFrame2 frame)
        {
            return null;
        }

        var line = (int)ComValue.Try(() => frame.LineNumber);
        return new SourceLocation(
            ComValue.NullIfEmpty(ComValue.Try(() => frame.FileName)),
            line > 0 ? line : null,
            ComValue.NullIfEmpty(ComValue.Try(() => frame.FunctionName)),
            ComValue.NullIfEmpty(ComValue.Try(() => frame.Module)));
    }

    private static BreakpointHit? LastHit(Debugger debugger) =>
        debugger.BreakpointLastHit is { } breakpoint
            ? new BreakpointHit(ComValue.NullIfEmpty(breakpoint.File), breakpoint.FileLine, breakpoint.CurrentHits)
            : null;
}
