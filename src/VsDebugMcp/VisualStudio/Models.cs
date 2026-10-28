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
    int Line,
    string? Function,
    string? Condition,
    bool Enabled,
    int Hits,
    string BindState,
    int[]? BoundLines,
    bool? AlreadyExisted = null);

public sealed record SourceLocation(string? File, int? Line, string? Function, string? Module);

public sealed record BreakpointHit(string? File, int Line, int Hits);

/// <param name="Mode">"design" (not debugging), "run" or "break".</param>
/// <param name="BreakReason">Why the debugger stopped, e.g. "breakpoint", "exceptionThrown", "step"; break mode only.</param>
/// <param name="Location">Current stack frame; break mode only.</param>
/// <param name="Breakpoint">Breakpoint that caused the break, when <paramref name="BreakReason"/> is "breakpoint".</param>
public sealed record DebuggerState(
    string Mode,
    string? BreakReason,
    SourceLocation? Location,
    BreakpointHit? Breakpoint,
    string? Process,
    bool? TimedOut = null,
    string? Note = null);
