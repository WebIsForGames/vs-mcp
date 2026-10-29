using EnvDTE;
using EnvDTE80;

namespace VsDebugMcp.VisualStudio;

/// <summary>
/// Follows one Visual Studio debugger beyond what polling shows. It counts transitions into run and break mode, so a
/// new stop that looks like the previous one (a __debugbreak in a loop) or a program that exits between two polls is
/// still noticed. It also reads the Debug output pane, the only place automation shows native exception messages.
/// Create it and call the output methods on the STA thread; Visual Studio raises the events on RPC threads.
/// </summary>
public sealed class DebuggerMonitor
{
    // GUID_OutWindowDebugPane: the pane name follows the UI language, the GUID does not.
    private static readonly Guid DebugPaneGuid = new("FC076020-078A-11D1-A7DF-00A0C9110051");

    private readonly DTE _dte;

    // The event source must stay referenced, or the subscription ends when it is collected.
    private readonly DebuggerEvents? _events;
    private int _runs;
    private int _breaks;
    private int _outputStart = 1;

    public DebuggerMonitor(DTE dte)
    {
        _dte = dte;
        _events = ComValue.Try(() =>
        {
            var events = dte.Events.DebuggerEvents;
            events.OnEnterRunMode += reason => Interlocked.Increment(ref _runs);
            events.OnEnterBreakMode += (dbgEventReason reason, ref dbgExecutionAction action) => Interlocked.Increment(ref _breaks);
            return events;
        });
    }

    /// <summary>Transitions into run and break mode seen since the monitor was created; zero without events.</summary>
    public (int Runs, int Breaks) Transitions => (Volatile.Read(ref _runs), Volatile.Read(ref _breaks));

    /// <summary>Remembers the end of the Debug pane, so <see cref="ReadOutput"/> returns only text written after it.</summary>
    public void MarkOutput() => _outputStart = ComValue.Try(() => DebugPane()?.TextDocument.EndPoint.AbsoluteCharOffset) ?? 1;

    /// <summary>Debug pane text written since <see cref="MarkOutput"/>, or all of it if the pane was cleared since then.</summary>
    public string? ReadOutput() => ComValue.Try(() =>
    {
        if (DebugPane()?.TextDocument is not { } document)
        {
            return null;
        }

        var start = document.StartPoint.CreateEditPoint();
        if (_outputStart <= document.EndPoint.AbsoluteCharOffset)
        {
            start.MoveToAbsoluteOffset(_outputStart);
        }

        return start.GetText(document.EndPoint);
    });

    private OutputWindowPane? DebugPane()
    {
        var panes = ((DTE2)_dte).ToolWindows.OutputWindow.OutputWindowPanes;
        for (var i = 1; i <= panes.Count; i++)
        {
            var pane = panes.Item(i);
            if (Guid.TryParse(pane.Guid, out var guid) && guid == DebugPaneGuid)
            {
                return pane;
            }
        }

        return null;
    }
}
