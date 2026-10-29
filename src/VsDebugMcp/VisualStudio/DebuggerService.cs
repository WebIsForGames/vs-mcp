using EnvDTE;
using EnvDTE80;
using ModelContextProtocol;

namespace VsDebugMcp.VisualStudio;

/// <summary>Breakpoint and execution operations on the target Visual Studio instance.</summary>
public sealed class DebuggerService(VsSession session)
{
    public const int DefaultWaitSeconds = 60;
    public const int MaxWaitSeconds = 300;
    public const int DefaultStackFrames = 20;
    public const int MaxStackFrames = 100;

    private const int MaxReportedBuildErrors = 10;
    private static readonly TimeSpan WaitPollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan TransitionPollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ContinueTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(15);

    // Visual Studio writes the exception message to the Debug pane up to about 0.4 s after it breaks.
    private static readonly TimeSpan ExceptionMessageTimeout = TimeSpan.FromSeconds(1);

    public static TimeSpan ClampWaitTimeout(int seconds) => TimeSpan.FromSeconds(Math.Clamp(seconds, 1, MaxWaitSeconds));

    public static int ClampStackFrames(int frames) => Math.Clamp(frames, 1, MaxStackFrames);

    public Task<BreakpointInfo> SetBreakpointAsync(string? file, int? line, string? function, string? condition)
    {
        var target = BreakpointTarget.Create(file, line, function);
        if (target.File is not null && !File.Exists(target.File))
        {
            throw new McpException($"File not found: {target.File}");
        }

        return session.RunAsync(vs =>
        {
            var debugger = vs.Dte.Debugger;
            var debugging = debugger.CurrentMode != dbgDebugMode.dbgDesignMode;
            if (Find(debugger, target).FirstOrDefault() is { } existing)
            {
                return Describe(existing, debugging) with { AlreadyExisted = true };
            }

            var added = target.Function is not null
                ? debugger.Breakpoints.Add(Function: target.Function, Condition: condition ?? "")
                : debugger.Breakpoints.Add(File: target.File, Line: target.Line.GetValueOrDefault(), Condition: condition ?? "");
            if (added.Count == 0)
            {
                throw new McpException($"Visual Studio did not create a breakpoint at {target}.");
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

    public Task<List<BreakpointInfo>> RemoveBreakpointAsync(string? file, int? line, string? function)
    {
        var target = BreakpointTarget.Create(file, line, function);
        return session.RunAsync(vs =>
        {
            var debugger = vs.Dte.Debugger;
            var debugging = debugger.CurrentMode != dbgDebugMode.dbgDesignMode;
            var matches = Find(debugger, target).ToList();
            if (matches.Count == 0)
            {
                throw new McpException($"No breakpoint at {target}. Call vs_list_breakpoints to see existing ones.");
            }

            var removed = matches.Select(breakpoint => Describe(breakpoint, debugging)).ToList();
            matches.ForEach(breakpoint => breakpoint.Delete());
            return removed;
        });
    }

    public async Task<DebuggerState> StartAsync(bool build, CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        var runsBefore = await session.InvokeAsync(instance, vs =>
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

            var runs = vs.Monitor.Transitions.Runs;
            vs.Monitor.MarkOutput();
            dte.Debugger.Go(WaitForBreakOrEnd: false);
            return runs;
        });

        // A program that exits before the first poll shows up only as a run transition.
        var state = await PollAsync(
            instance,
            StartTimeout,
            TransitionPollInterval,
            (s, transitions) => s.Mode != DebuggerText.Design || transitions.Runs != runsBefore,
            cancellationToken);
        return state switch
        {
            { TimedOut: true } => state with { Note = $"The debugger did not start within {StartTimeout.TotalSeconds:0} s. Check Visual Studio for a dialog such as 'Unable to start program'." },
            { Mode: DebuggerText.Design } => state with { Note = "The program started and exited without stopping." },
            _ => state,
        };
    }

    public async Task<DebuggerState> ContinueAsync(CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        var (before, transitionsBefore) = await session.InvokeAsync(instance, vs =>
        {
            var debugger = vs.Dte.Debugger;
            RequireBreakMode(debugger, "vs_continue");
            var transitions = vs.Monitor.Transitions;
            var state = DebuggerSnapshot.Capture(debugger);
            vs.Monitor.MarkOutput();
            debugger.Go(WaitForBreakOrEnd: false);
            return (state, transitions);
        });

        // Wait until Visual Studio leaves the break it was in, so a following vs_wait_for_break
        // reports the next stop rather than this one. A quick re-hit shows up as a changed state or,
        // when it looks the same (a __debugbreak in a loop), as a new run and break transition.
        var after = await PollAsync(
            instance,
            ContinueTimeout,
            TransitionPollInterval,
            (s, transitions) => s != before || (transitions.Runs > transitionsBefore.Runs && transitions.Breaks > transitionsBefore.Breaks),
            cancellationToken);
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
        var state = await PollAsync(instance, timeout, WaitPollInterval, (s, _) => s.Mode != DebuggerText.Run, cancellationToken);
        return state.Mode switch
        {
            DebuggerText.Design => state with { Note = "Not debugging: the program exited or debugging was never started." },
            DebuggerText.Run => state with { Note = $"Still running after {timeout.TotalSeconds:0} s. Call vs_wait_for_break again, or vs_stop_debugging." },
            _ => state,
        };
    }

    public async Task<DebuggerState> GetStateAsync(CancellationToken cancellationToken)
    {
        var instance = await session.ResolveAsync();
        var state = await session.InvokeAsync(instance, vs => DebuggerSnapshot.Capture(vs.Dte.Debugger));
        return await WithExceptionMessageAsync(instance, state, cancellationToken);
    }

    public Task<CallStack> GetCallStackAsync(int maxFrames) => session.RunAsync(vs =>
    {
        var debugger = vs.Dte.Debugger;
        RequireBreakMode(debugger, "vs_get_call_stack");
        return DebuggerSnapshot.CaptureCallStack(debugger, ClampStackFrames(maxFrames));
    });

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

        var after = await PollAsync(instance, StopTimeout, TransitionPollInterval, (s, _) => s.Mode == DebuggerText.Design, cancellationToken);
        return after.TimedOut == true
            ? after with { Note = $"The debugger did not stop within {StopTimeout.TotalSeconds:0} s." }
            : after;
    }

    private async Task<DebuggerState> PollAsync(
        VsInstance instance,
        TimeSpan timeout,
        TimeSpan interval,
        Func<DebuggerState, (int Runs, int Breaks), bool> isDone,
        CancellationToken cancellationToken)
    {
        // Transitions are read before the state, so one already counted is always reflected in that state.
        var (polled, timedOut) = await PollUntilAsync(
            instance,
            vs => (Transitions: vs.Monitor.Transitions, State: DebuggerSnapshot.Capture(vs.Dte.Debugger)),
            p => isDone(p.State, p.Transitions),
            timeout,
            interval,
            cancellationToken);
        return timedOut
            ? polled.State with { TimedOut = true }
            : await WithExceptionMessageAsync(instance, polled.State, cancellationToken);
    }

    private async Task<(T Value, bool TimedOut)> PollUntilAsync<T>(
        VsInstance instance,
        Func<VsInstance, T> read,
        Func<T, bool> isDone,
        TimeSpan timeout,
        TimeSpan interval,
        CancellationToken cancellationToken)
    {
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        while (true)
        {
            var value = await session.InvokeAsync(instance, read);
            if (isDone(value))
            {
                return (value, false);
            }

            if (elapsed.Elapsed >= timeout)
            {
                return (value, true);
            }

            await Task.Delay(interval, cancellationToken);
        }
    }

    private async Task<DebuggerState> WithExceptionMessageAsync(VsInstance instance, DebuggerState state, CancellationToken cancellationToken)
    {
        if (state.BreakReason is not (DebuggerText.ExceptionThrown or DebuggerText.ExceptionNotHandled))
        {
            return state;
        }

        var (message, _) = await PollUntilAsync(
            instance,
            vs => DebuggerText.ExceptionMessage(vs.Monitor.ReadOutput()),
            text => text is not null,
            ExceptionMessageTimeout,
            TransitionPollInterval,
            cancellationToken);
        return state with { ExceptionMessage = message };
    }

    private static void RequireBreakMode(Debugger debugger, string tool)
    {
        var mode = debugger.CurrentMode;
        if (mode != dbgDebugMode.dbgBreakMode)
        {
            throw new McpException(
                $"{tool} works only in break mode, but the debugger is in {DebuggerText.Mode(mode)} mode." +
                (mode == dbgDebugMode.dbgDesignMode ? " Use vs_start_debugging." : " Use vs_wait_for_break."));
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

    private static IEnumerable<Breakpoint> Find(Debugger debugger, BreakpointTarget target) => All(debugger).Where(target.Matches);

    private static BreakpointInfo Describe(Breakpoint breakpoint, bool debugging)
    {
        // Bound children exist only while debugging; their hit counts are the ones that change.
        var bound = ComValue.Try(() => Items(breakpoint.Children).Select(child => (child.FileLine, child.CurrentHits)).ToList()) ?? [];
        // Function breakpoints and their bound children in code without source have line 0.
        int[] boundLines = [.. bound.Select(child => child.FileLine).Where(line => line > 0).Distinct().Order()];
        var bindState = !debugging ? "notDebugging" : bound.Count > 0 ? "bound" : "unbound";
        return new BreakpointInfo(
            ComValue.NullIfEmpty(breakpoint.File),
            breakpoint.FileLine > 0 ? breakpoint.FileLine : null,
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
