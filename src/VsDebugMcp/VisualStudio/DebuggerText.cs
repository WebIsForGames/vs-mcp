using EnvDTE;

namespace VsDebugMcp.VisualStudio;

/// <summary>Stable names for debugger enums in tool results.</summary>
public static class DebuggerText
{
    public const string Design = "design";
    public const string Run = "run";
    public const string Break = "break";

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
}
