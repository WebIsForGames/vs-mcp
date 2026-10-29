using System.Text.RegularExpressions;
using EnvDTE;

namespace VsDebugMcp.VisualStudio;

/// <summary>Stable names for debugger enums in tool results.</summary>
public static partial class DebuggerText
{
    public const string Design = "design";
    public const string Run = "run";
    public const string Break = "break";
    public const string ExceptionThrown = "exceptionThrown";
    public const string ExceptionNotHandled = "exceptionNotHandled";

    private const int MaxExceptionMessageLines = 4;

    public static string Mode(dbgDebugMode mode) => mode switch
    {
        dbgDebugMode.dbgDesignMode => Design,
        dbgDebugMode.dbgRunMode => Run,
        dbgDebugMode.dbgBreakMode => Break,
        _ => mode.ToString(),
    };

    /// <summary>Maps e.g. dbgEventReasonExceptionThrown to "exceptionThrown".</summary>
    public static string Reason(dbgEventReason reason)
    {
        const string prefix = "dbgEventReason";
        var name = reason.ToString();
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
        {
            return name;
        }

        return char.ToLowerInvariant(name[prefix.Length]) + name[(prefix.Length + 1)..];
    }

    /// <summary>
    /// Picks the exception message from Debug pane text written since the program resumed: its last lines,
    /// without module load and exit notices. Visual Studio writes the message there as it breaks.
    /// </summary>
    public static string? ExceptionMessage(string? output)
    {
        var lines = (output ?? "").Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line) && !ModuleNotice().IsMatch(line) && !ExitNotice().IsMatch(line))
            .TakeLast(MaxExceptionMessageLines)
            .ToList();
        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    // "'app.exe' (Win32): Loaded 'C:\Windows\System32\ntdll.dll'." in any UI language.
    [GeneratedRegex(@"^'[^']+' \([^)]+\): ")]
    private static partial Regex ModuleNotice();

    // "The thread 42 has exited with code 0 (0x0)." and the same for the program, in any UI language.
    [GeneratedRegex(@"\(0x[0-9A-Fa-f]+\)\.$")]
    private static partial Regex ExitNotice();
}
