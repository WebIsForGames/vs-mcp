using EnvDTE;
using EnvDTE90a;

namespace VsDebugMcp.VisualStudio;

/// <summary>Reads the debugger state. Call <see cref="Capture"/> only on the STA thread.</summary>
public static class DebuggerSnapshot
{
    // A stop without source deeper than this is reported without a source location.
    private const int MaxSourceFrameDepth = 50;

    public static DebuggerState Capture(Debugger debugger)
    {
        var mode = debugger.CurrentMode;
        var process = ComValue.NullIfEmpty(ComValue.Try(() => debugger.CurrentProcess?.Name));
        if (mode != dbgDebugMode.dbgBreakMode)
        {
            return new DebuggerState(DebuggerText.Mode(mode), BreakReason: null, Location: null, Breakpoint: null, process);
        }

        var reason = debugger.LastBreakReason;
        var current = ComValue.Try(() => Location(debugger.CurrentStackFrame));
        var location = ComValue.Try(() => SourceFrame(current, Stack(() => debugger.CurrentThread.StackFrames, MaxSourceFrameDepth))) ?? current;
        return new DebuggerState(
            DebuggerText.Break,
            DebuggerText.Reason(reason),
            location,
            reason == dbgEventReason.dbgEventReasonBreakpoint ? ComValue.Try(() => LastHit(debugger)) : null,
            process,
            StoppedIn: location == current ? null : current);
    }

    /// <summary>
    /// The current frame if it has source, otherwise the nearest frame on the stack that does, e.g. the caller
    /// of a CRT function stopped on a function breakpoint. Visual Studio already does this for exceptions.
    /// </summary>
    public static SourceLocation? SourceFrame(SourceLocation? current, IEnumerable<SourceLocation> stack) =>
        current?.File is not null ? current : stack.FirstOrDefault(frame => frame.File is not null) ?? current;

    /// <summary>The first <paramref name="maxFrames"/> frames of the current thread. Call only on the STA thread in break mode.</summary>
    public static CallStack CaptureCallStack(Debugger debugger, int maxFrames)
    {
        // Each StackFrames collection walks the whole stack on its first Count (0.7 s for 3800 frames), so only one is read.
        var frames = debugger.CurrentThread.StackFrames;
        return new CallStack(frames.Count, [.. Stack(() => frames, maxFrames)]);
    }

    // Reads the frames only when enumerated: Capture needs none when the current frame has source.
    private static IEnumerable<SourceLocation> Stack(Func<StackFrames> read, int maxFrames)
    {
        var frames = read();
        for (var i = 1; i <= Math.Min(frames.Count, maxFrames); i++)
        {
            if (Location(frames.Item(i)) is { } frame)
            {
                yield return frame;
            }
        }
    }

    private static SourceLocation? Location(StackFrame? stackFrame)
    {
        if (stackFrame is not StackFrame2 frame)
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
            ? new BreakpointHit(
                ComValue.NullIfEmpty(breakpoint.File),
                breakpoint.FileLine > 0 ? breakpoint.FileLine : null,
                ComValue.NullIfEmpty(ComValue.Try(() => breakpoint.FunctionName)),
                breakpoint.CurrentHits)
            : null;
}
