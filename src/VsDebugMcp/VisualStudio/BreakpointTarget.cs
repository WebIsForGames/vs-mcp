using EnvDTE;
using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

/// <summary>Where a breakpoint is: a source line, or a function when <see cref="Function"/> is set.</summary>
public sealed record BreakpointTarget(string? File, int? Line, string? Function)
{
    public static BreakpointTarget Create(string? file, int? line, string? function)
    {
        if (!string.IsNullOrWhiteSpace(function))
        {
            return file is null && line is null
                ? new BreakpointTarget(null, null, function.Trim())
                : throw new McpException("Pass either function, or file and line, not both.");
        }

        if (file is null || line is null)
        {
            throw new McpException("Pass file and line for a line breakpoint, or function for a function breakpoint.");
        }

        if (line < 1)
        {
            throw new McpException($"Line must be 1 or greater, got {line}.");
        }

        return new BreakpointTarget(SourcePath.Normalize(file), line, null);
    }

    public bool Matches(Breakpoint breakpoint) =>
        Function is not null
            ? breakpoint.LocationType == dbgBreakpointLocationType.dbgBreakpointLocationTypeFunction
                && string.Equals(breakpoint.FunctionName, Function, StringComparison.Ordinal)
            : breakpoint.FileLine == Line && SourcePath.Same(breakpoint.File, File);

    public override string ToString() => Function is not null ? $"function {Function}" : $"{File}:{Line}";
}
