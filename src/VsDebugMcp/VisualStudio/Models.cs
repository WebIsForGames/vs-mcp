namespace VsDebugMcp.VisualStudio;

public sealed record InstanceInfo(
    int ProcessId,
    string Version,
    string? Solution,
    string? Mode,
    bool Selected,
    string? Error);

/// <param name="BindState">
/// "notDebugging", "bound" (code was found for the line) or "unbound" (module not loaded yet, no symbols, or no code at the line).
/// </param>
/// <param name="BoundLines">Lines the debugger actually bound to; may differ from <paramref name="Line"/>.</param>
public sealed record BreakpointInfo(
    string? File,
    int? Line,
    string? Function,
    string? Condition,
    bool Enabled,
    int Hits,
    string BindState,
    int[]? BoundLines,
    bool? AlreadyExisted = null);

public sealed record SourceLocation(string? File, int? Line, string? Function, string? Module);

public sealed record BreakpointHit(string? File, int? Line, string? Function, int Hits);

/// <param name="Mode">"design" (not debugging), "run" or "break".</param>
/// <param name="BreakReason">Why the debugger stopped, e.g. "breakpoint", "exceptionThrown", "step"; break mode only.</param>
/// <param name="Location">
/// Where execution stopped in source code: the current stack frame, or its nearest caller with source when the
/// current frame has none; break mode only.
/// </param>
/// <param name="Breakpoint">Breakpoint that caused the break, when <paramref name="BreakReason"/> is "breakpoint".</param>
/// <param name="StoppedIn">The frame execution stopped in when it has no source, e.g. a CRT function.</param>
/// <param name="ExceptionMessage">
/// What Visual Studio wrote to the Debug output pane for an exception stop, e.g. "Exception thrown: read access
/// violation."; worded in the Visual Studio UI language.
/// </param>
public sealed record DebuggerState(
    string Mode,
    string? BreakReason,
    SourceLocation? Location,
    BreakpointHit? Breakpoint,
    string? Process,
    SourceLocation? StoppedIn = null,
    string? ExceptionMessage = null,
    bool? TimedOut = null,
    string? Note = null);
