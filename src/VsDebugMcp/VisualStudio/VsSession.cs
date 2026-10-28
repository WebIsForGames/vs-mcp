using EnvDTE;
using ModelContextProtocol;
using VsDebugMcp.Com;

namespace VsDebugMcp.VisualStudio;

public sealed record VsInstance(VsMoniker Moniker, DTE Dte);

/// <summary>
/// Finds Visual Studio instances, remembers the one the user selected and runs automation calls
/// against the target instance on the STA thread, turning COM failures into tool errors.
/// </summary>
public sealed class VsSession(StaComDispatcher sta)
{
    // Accessed only on the STA thread.
    private int? _selectedProcessId;

    public Task<List<InstanceInfo>> ListAsync() => sta.InvokeAsync(() => FindAll().Select(Describe).ToList());

    public Task<InstanceInfo> SelectAsync(int processId) => sta.InvokeAsync(() =>
    {
        var instance = FindAll().FirstOrDefault(i => i.Moniker.ProcessId == processId)
            ?? throw new McpException($"No Visual Studio 2022/2026 instance with pid {processId}. Call vs_list_instances.");
        _selectedProcessId = processId;
        return Describe(instance);
    });

    /// <summary>Resolves the target instance and runs <paramref name="action"/> against it in one STA call.</summary>
    public Task<T> RunAsync<T>(Func<VsInstance, T> action) => sta.InvokeAsync(() => Execute(Resolve(), action));

    /// <summary>Resolves the target instance once, for callers that poll it repeatedly.</summary>
    public Task<VsInstance> ResolveAsync() => sta.InvokeAsync(Resolve);

    public Task<T> InvokeAsync<T>(VsInstance instance, Func<VsInstance, T> action) =>
        sta.InvokeAsync(() => Execute(instance, action));

    private VsInstance Resolve()
    {
        var instances = FindAll();
        var target = InstanceSelector.Select(instances.Select(i => i.Moniker).ToList(), _selectedProcessId);
        return instances.First(i => i.Moniker == target);
    }

    private static T Execute<T>(VsInstance instance, Func<VsInstance, T> action)
    {
        try
        {
            return action(instance);
        }
        catch (Exception ex) when (ex is not McpException)
        {
            throw ComErrors.ToToolError(ex, instance.Moniker);
        }
    }

    private static List<VsInstance> FindAll() =>
        RunningObjectTable.GetObjects(VsMoniker.ParseSupported)
            .Where(entry => entry.Object is DTE)
            .Select(entry => new VsInstance(entry.Key, (DTE)entry.Object))
            .OrderBy(instance => instance.Moniker.ProcessId)
            .ToList();

    private InstanceInfo Describe(VsInstance instance)
    {
        var moniker = instance.Moniker;
        var selected = moniker.ProcessId == _selectedProcessId;
        try
        {
            var dte = instance.Dte;
            return new InstanceInfo(
                moniker.ProcessId,
                moniker.ProductYear!,
                ComValue.NullIfEmpty(dte.Solution.FullName),
                DebuggerText.Mode(dte.Debugger.CurrentMode),
                selected,
                Error: null);
        }
        catch (Exception ex)
        {
            return new InstanceInfo(
                moniker.ProcessId,
                moniker.ProductYear!,
                Solution: null,
                Mode: null,
                selected,
                ComErrors.ToToolError(ex, moniker).Message);
        }
    }
}
