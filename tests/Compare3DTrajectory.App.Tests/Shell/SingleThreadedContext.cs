using System.Collections.Concurrent;

namespace Compare3DTrajectory.App.Tests.Shell;

/// <summary>
/// Runs a test the way the window runs: every continuation comes back to one thread, in the order
/// it was posted. A test can then post an action to happen while an operation is awaiting, as a
/// click or a keystroke would, and know exactly where it lands.
/// </summary>
internal sealed class SingleThreadedContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = [];

    public static void Run(Func<Task> body)
    {
        var context = new SingleThreadedContext();
        SynchronizationContext? previous = Current;
        SetSynchronizationContext(context);
        try
        {
            Task task = body();
            task.ContinueWith(_ => context._queue.CompleteAdding(), TaskScheduler.Default);
            foreach ((SendOrPostCallback callback, object? state) in context._queue.GetConsumingEnumerable())
            {
                callback(state);
            }

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>A continuation posted after the test has finished, such as a superseded comparison's, is dropped.</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        if (!_queue.IsAddingCompleted)
        {
            try
            {
                _queue.Add((d, state));
            }
            catch (InvalidOperationException)
            {
                // Completed between the check and the add.
            }
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();
}
