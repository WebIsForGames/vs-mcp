using System.Collections.Concurrent;

namespace VsDebugMcp.Com;

/// <summary>
/// Runs all COM automation calls on one dedicated STA thread with an OLE message filter,
/// so calls rejected by a busy Visual Studio are retried instead of failing.
/// </summary>
public sealed class StaComDispatcher : IDisposable
{
    private readonly BlockingCollection<Action> _queue = new();
    private readonly Thread _thread;

    public StaComDispatcher()
    {
        _thread = new Thread(Run) { IsBackground = true, Name = "COM STA" };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();
    }

    public Task<T> InvokeAsync<T>(Func<T> func)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        _queue.Add(() =>
        {
            try
            {
                completion.SetResult(func());
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
        });
        return completion.Task;
    }

    public void Dispose()
    {
        _queue.CompleteAdding();
        _thread.Join(TimeSpan.FromSeconds(5));
    }

    private void Run()
    {
        MessageFilter.Register();
        try
        {
            foreach (var action in _queue.GetConsumingEnumerable())
            {
                action();
            }
        }
        finally
        {
            MessageFilter.Revoke();
        }
    }
}
