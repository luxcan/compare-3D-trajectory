using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Tests.Viewer;

public class TrailingThrottleTests
{
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(40);

    private readonly List<(TimeSpan Delay, Action Run)> _scheduled = [];

    private DateTime _now = new(2026, 10, 23, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Submit_FirstMove_RunsAtOnce()
    {
        var shown = new List<int>();

        Throttle().Submit(() => shown.Add(1));

        shown.Should().Equal(1);
    }

    /// <summary>
    /// F44: the moves within the interval were dropped, the last one too, so the card went on
    /// describing a point the pointer had already left.
    /// </summary>
    [Fact]
    public void Submit_MovesThatStopWithinTheInterval_StillRunTheLastOneOnceTheIntervalIsUp()
    {
        var shown = new List<int>();
        TrailingThrottle throttle = Throttle();

        throttle.Submit(() => shown.Add(1));
        _now += TimeSpan.FromMilliseconds(10);
        throttle.Submit(() => shown.Add(2));
        _now += TimeSpan.FromMilliseconds(10);
        throttle.Submit(() => shown.Add(3));
        RunScheduled();

        shown.Should().Equal(1, 3);
    }

    [Fact]
    public void Cancel_WhileAMoveWaits_DropsIt()
    {
        var shown = new List<int>();
        TrailingThrottle throttle = Throttle();
        throttle.Submit(() => shown.Add(1));
        _now += TimeSpan.FromMilliseconds(10);
        throttle.Submit(() => shown.Add(2));

        throttle.Cancel();
        RunScheduled();

        shown.Should().Equal(1);
    }

    private TrailingThrottle Throttle() => new(Interval, () => _now, (delay, run) => _scheduled.Add((delay, run)));

    private void RunScheduled()
    {
        foreach ((TimeSpan delay, Action run) in _scheduled.ToList())
        {
            _now += delay;
            run();
        }
    }
}
