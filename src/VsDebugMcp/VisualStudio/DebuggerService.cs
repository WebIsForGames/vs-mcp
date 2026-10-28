using EnvDTE;
using EnvDTE80;
using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

/// <summary>Breakpoint and execution operations on the target Visual Studio instance.</summary>
public sealed class DebuggerService(VsSession session)
{
    public const int DefaultWaitSeconds = 60;
    public const int MaxWaitSeconds = 300;

    private const int MaxReportedBuildErrors = 10;
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan TransitionPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ContinueTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    public static TimeSpan ClampWaitTimeout(int seconds) => TimeSpan.FromSeconds(Math.Clamp(seconds, 1, MaxWaitSeconds));

    public Task<BreakpointInfo> SetBreakpointAsync(string file, int line, string? condition)
    {
        var path = SourcePath.Normalize(file);
        if (!File.Exists(path))
        {
            throw new McpException($"File not found: {path}");
        }

        if (line < 1)
        {
            throw new McpException($"Line must be 1 or greater, got {line}.");
        }

        return session.RunAsync(vs =>
        {
            var debugger = vs.Dte.Debugger;
            var debugging = debugger.CurrentMode != dbgDebugMode.dbgDesignMode;
            if (FindAt(debugger, path, line).FirstOrDefault() is { } existing)
            {
                return Describe(existing, debugging) with { AlreadyExisted = true };
            }

            var added = debugger.Breakpoints.Add(File: path, Line: line, Condition: condition ?? "");
            if (added.Count == 0)
            {
                throw new McpException($"Visual Studio did not create a breakpoint at {path}:{line}.");
            }

            return Describe(added.Item(1), debugging);
        });
    }

    public Task<List<BreakpointInfo>> ListBreakpointsAsync() => session.RunAsync(vs =>
    {
        var debugger = vs.Dte.Debugger;
        var debugging = debugger.CurrentMode != dbgDebugMode.dbgDesignMode;
        return All(debugger).Select(breakpoint => Describe(breakpoint, debugging)).ToList();
    });

    public Task<List<BreakpointInfo>> RemoveBreakpointAsync(string file, int line)
    {
        var path = SourcePath.Normalize(file);
        return session.RunAsync(vs =>
        {
            var debugger = vs.Dte.Debugger;
            var debugging = debugger.CurrentMode != dbgDebugMode.dbgDesignMode;
            var matches = FindAt(debugger, path, line).ToList();
            if (matches.Count == 0)
            {
                throw new McpException($"No breakpoint at {path}:{line}. Call vs_list_breakpoints to see existing ones.");
            }

            var removed = matches.Select(breakpoint => Describe(breakpoint, debugging)).ToList();
            matches.ForEach(breakpoint => breakpoint.Delete());
            return removed;
        });
    }

    public async Task<DebuggerState> StartAsync(bool build, CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        await session.InvokeAsync(instance, vs =>
        {
            var dte = vs.Dte;
            var mode = dte.Debugger.CurrentMode;
            if (mode != dbgDebugMode.dbgDesignMode)
            {
                throw new McpException(
                    $"Already debugging ({DebuggerText.Mode(mode)} mode). Use vs_continue or vs_wait_for_break, or vs_stop_debugging first.");
            }

            if (!dte.Solution.IsOpen)
            {
                throw new McpException("No solution is open in Visual Studio.");
            }

            var solutionBuild = dte.Solution.SolutionBuild;
            if (solutionBuild.StartupProjects is not object[] { Length: > 0 })
            {
                throw new McpException("The solution has no startup project. Set one in Visual Studio (Set as Startup Project).");
            }

            if (build)
            {
                solutionBuild.Build(WaitForBuildToFinish: true);
                if (solutionBuild.LastBuildInfo != 0)
                {
                    throw new McpException(
                        $"Build failed in {solutionBuild.LastBuildInfo} project(s); debugging was not started.{BuildErrors(dte)}");
                }
            }

            dte.Debugger.Go(WaitForBreakOrEnd: false);
            return true;
        });

        var state = await PollAsync(instance, StartTimeout, TransitionPollInterval, s => s.Mode != DebuggerText.Design, cancellationToken);
        return state.TimedOut == true
            ? state with { Note = $"The debugger did not start within {StartTimeout.TotalSeconds:0} s. Check Visual Studio for a dialog such as 'Unable to start program'." }
            : state;
    }

    public async Task<DebuggerState> ContinueAsync(CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        var before = await session.InvokeAsync(instance, vs =>
        {
            var debugger = vs.Dte.Debugger;
            var mode = debugger.CurrentMode;
            if (mode != dbgDebugMode.dbgBreakMode)
            {
                throw new McpException(
                    $"vs_continue works only in break mode, but the debugger is in {DebuggerText.Mode(mode)} mode." +
                    (mode == dbgDebugMode.dbgDesignMode ? " Use vs_start_debugging." : " Use vs_wait_for_break."));
            }

            var state = DebuggerSnapshot.Capture(debugger);
            debugger.Go(WaitForBreakOrEnd: false);
            return state;
        });

        // Wait until Visual Studio leaves the break it was in, so a following vs_wait_for_break
        // reports the next stop rather than this one. A quick re-hit shows up as a changed state.
        var after = await PollAsync(instance, ContinueTimeout, TransitionPollInterval, s => s != before, cancellationToken);
        return after switch
        {
            { TimedOut: true } => after with { Note = $"Visual Studio was still in the same break after {ContinueTimeout.TotalSeconds:0} s; check it for a dialog." },
            { Mode: DebuggerText.Design } => after with { Note = "The debugging session ended (the program exited)." },
            _ => after,
        };
    }

    public async Task<DebuggerState> WaitForBreakAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var timeout = ClampWaitTimeout(timeoutSeconds);
        var instance = await session.ResolveAsync();
        var state = await PollAsync(instance, timeout, WaitPollInterval, s => s.Mode != DebuggerText.Run, cancellationToken);
        return state.Mode switch
        {
            DebuggerText.Design => state with { Note = "Not debugging: the program exited or debugging was never started." },
            DebuggerText.Run => state with { Note = $"Still running after {timeout.TotalSeconds:0} s. Call vs_wait_for_break again, or vs_stop_debugging." },
            _ => state,
        };
    }

    public Task<DebuggerState> GetStateAsync() => session.RunAsync(vs => DebuggerSnapshot.Capture(vs.Dte.Debugger));

    public async Task<DebuggerState> StopAsync(CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        var before = await session.InvokeAsync(instance, vs =>
        {
            var debugger = vs.Dte.Debugger;
            var state = DebuggerSnapshot.Capture(debugger);
            if (state.Mode != DebuggerText.Design)
            {
                debugger.Stop(WaitForDesignMode: false);
            }

            return state;
        });

        if (before.Mode == DebuggerText.Design)
        {
            return before with { Note = "Not debugging; nothing to stop." };
        }

        var after = await PollAsync(instance, StopTimeout, TransitionPollInterval, s => s.Mode == DebuggerText.Design, cancellationToken);
        return after.TimedOut == true
            ? after with { Note = $"The debugger did not stop within {StopTimeout.TotalSeconds:0} s." }
            : after;
    }

    private async Task<DebuggerState> PollAsync(
        VsInstance instance,
        TimeSpan timeout,
        TimeSpan interval,
        Func<DebuggerState, bool> isDone,
        CancellationToken cancellationToken)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            var state = await session.InvokeAsync(instance, vs => DebuggerSnapshot.Capture(vs.Dte.Debugger));
            if (isDone(state))
            {
                return state;
            }

            if (elapsed.Elapsed >= timeout)
            {
                return state with { TimedOut = true };
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private static IEnumerable<Breakpoint> Items(Breakpoints breakpoints)
    {
        for (var i = 1; i <= breakpoints.Count; i++)
        {
            yield return breakpoints.Item(i);
        }
    }

    private static IEnumerable<Breakpoint> All(Debugger debugger) => Items(debugger.Breakpoints);

    private static IEnumerable<Breakpoint> FindAt(Debugger debugger, string path, int line) =>
        All(debugger).Where(breakpoint => breakpoint.FileLine == line && SourcePath.Same(breakpoint.File, path));

    private static BreakpointInfo Describe(Breakpoint breakpoint, bool debugging)
    {
        // Bound children exist only while debugging; their hit counts are the ones that change.
        var bound = ComValue.Try(() => Items(breakpoint.Children).Select(child => (child.FileLine, child.CurrentHits)).ToList()) ?? [];
        int[] boundLines = [.. bound.Select(child => child.FileLine).Distinct().Order()];
        var bindState = !debugging ? "notDebugging" : bound.Count > 0 ? "bound" : "unbound";
        return new BreakpointInfo(
            ComValue.NullIfEmpty(breakpoint.File),
            breakpoint.FileLine,
            ComValue.NullIfEmpty(ComValue.Try(() => breakpoint.FunctionName)),
            ComValue.NullIfEmpty(breakpoint.Condition),
            breakpoint.Enabled,
            bound.Count > 0 ? bound.Sum(child => child.CurrentHits) : breakpoint.CurrentHits,
            bindState,
            boundLines.Length > 0 ? boundLines : null);
    }

    private static string BuildErrors(DTE dte)
    {
        var errors = ComValue.Try(() =>
        {
            var items = ((DTE2)dte).ToolWindows.ErrorList.ErrorItems;
            var lines = new List<string>();
            for (var i = 1; i <= items.Count && lines.Count < MaxReportedBuildErrors; i++)
            {
                var item = items.Item(i);
                if (item.ErrorLevel == vsBuildErrorLevel.vsBuildErrorLevelHigh)
                {
                    lines.Add($"{item.FileName}({item.Line}): {item.Description}");
                }
            }

            return lines;
        });

        return errors is { Count: > 0 } ? "\nErrors:\n" + string.Join("\n", errors) : " See the Error List in Visual Studio.";
    }
}
