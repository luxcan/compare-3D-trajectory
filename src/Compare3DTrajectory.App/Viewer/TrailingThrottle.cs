namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// Lets a frequent event, such as a mouse move, through at most once per interval, and always lets
/// the last of a burst through, once the interval is up. Dropping it left the hover card describing a
/// point the pointer had already left.
/// </summary>
/// <param name="interval">The least time between two runs.</param>
/// <param name="clock">The time now.</param>
/// <param name="schedule">Runs an action once after a delay, on the thread that submits.</param>
public sealed class TrailingThrottle(TimeSpan interval, Func<DateTime> clock, Action<TimeSpan, Action> schedule)
{
    private DateTime _last = DateTime.MinValue;
    private Action? _waiting;
    private bool _scheduled;

    /// <summary>Runs the action now if the interval is up; otherwise keeps it, in place of any kept before, to run when it is.</summary>
    public void Submit(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        DateTime now = clock();
        TimeSpan since = now - _last;
        if (since >= interval && !_scheduled)
        {
            _last = now;
            action();
            return;
        }

        _waiting = action;
        if (!_scheduled)
        {
            _scheduled = true;
            schedule(interval - since, RunWaiting);
        }
    }

    /// <summary>Forgets anything still waiting, as when the pointer leaves the view.</summary>
    public void Cancel() => _waiting = null;

    private void RunWaiting()
    {
        _scheduled = false;
        Action? waiting = _waiting;
        _waiting = null;
        if (waiting is not null)
        {
            _last = clock();
            waiting();
        }
    }
}
