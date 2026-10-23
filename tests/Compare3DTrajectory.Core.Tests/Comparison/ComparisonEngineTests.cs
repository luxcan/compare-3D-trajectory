using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests.Comparison;

public class ComparisonEngineTests
{
    private static readonly Route Route = Route.Standard();

    [Fact]
    public void Compare_MovedRunWithFitting_FindsNoIssues()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var run = new PreparedTrajectory(Guid.NewGuid(), Noise.Move(Route.Sample(1), 35, new Vec3(400, -250, 3)));

        ComparisonResult result = engine.Compare(reference, run, new ComparisonSettings(1), fitHeading: true);

        result.Issues.Should().BeEmpty();
        result.Fit.Transform.HeadingDegrees.Should().BeApproximately(-35, 0.1);
    }

    [Fact]
    public void Compare_MovedRunWithoutFitting_IsOffPathEverywhere()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var run = new PreparedTrajectory(Guid.NewGuid(), Noise.Move(Route.Sample(1), 35, new Vec3(400, -250, 3)));

        ComparisonResult result = engine.Compare(reference, run, new ComparisonSettings(1), fitHeading: false);

        result.Fit.IsFitted.Should().BeFalse();
        result.Summary.WithinTolerancePercent.Should().BeLessThan(5);
        engine.CachedFitCount.Should().Be(0);
    }

    [Fact]
    public void Compare_SecondCallWithANewTolerance_ReusesTheFit()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var run = new PreparedTrajectory(Guid.NewGuid(), Noise.Move(Route.Sample(1), 10, new Vec3(5, 5, 0)));

        ComparisonResult first = engine.Compare(reference, run, new ComparisonSettings(1), fitHeading: true);
        ComparisonResult second = engine.Compare(reference, run, new ComparisonSettings(5), fitHeading: true);

        second.Fit.Should().BeSameAs(first.Fit);
        engine.CachedFitCount.Should().Be(1);
    }

    /// <summary>
    /// F22: fits were cached by the trajectories' ids alone, so after a file's points changed, as on a
    /// switch between local and geocentric, a fit for the old points could be used on the new ones.
    /// </summary>
    [Fact]
    public void Compare_SameIdWithNewPoints_FitsTheNewPoints()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        Guid id = Guid.NewGuid();
        var before = new PreparedTrajectory(id, Noise.Move(Route.Sample(1), 0, new Vec3(100, 0, 0)));
        var after = new PreparedTrajectory(id, Noise.Move(Route.Sample(1), 0, new Vec3(-300, 0, 0)));
        engine.Compare(reference, before, new ComparisonSettings(1), fitHeading: true);

        ComparisonResult result = engine.Compare(reference, after, new ComparisonSettings(1), fitHeading: true);

        result.Issues.Should().BeEmpty();
        result.Fit.CentroidShift.X.Should().BeApproximately(300, 0.5);
    }

    /// <summary>F26: a parked entity 6.5 km away was fitted onto the path and read as 100% on it.</summary>
    [Fact]
    public void Compare_RunThatNeverMoves_IsComparedWhereItIs()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var parked = new PreparedTrajectory(Guid.NewGuid(), [.. Enumerable.Repeat(new Vec3(5000, 5000, 10), 50)]);

        ComparisonResult result = engine.Compare(reference, parked, new ComparisonSettings(2), fitHeading: true);

        result.Fit.IsFitted.Should().BeFalse();
        result.Summary.WithinTolerancePercent.Should().Be(0);
        result.Issues.Should().Contain(issue => issue.Kind == IssueKind.OffPath);
    }

    [Fact]
    public void Forget_Trajectory_DropsEveryFitItIsPartOf()
    {
        var engine = new ComparisonEngine();
        var a = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var b = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(7));
        var c = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(5));
        engine.Compare(a, b, new ComparisonSettings(1), fitHeading: true);
        engine.Compare(b, c, new ComparisonSettings(1), fitHeading: true);
        engine.Compare(a, c, new ComparisonSettings(1), fitHeading: true);

        engine.Forget(b.Id);

        engine.CachedFitCount.Should().Be(1);
    }

    [Fact]
    public void Compare_CancelledToken_Throws()
    {
        var engine = new ComparisonEngine();
        var reference = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10));
        var run = new PreparedTrajectory(Guid.NewGuid(), Route.Sample(1));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Action compare = () => engine.Compare(reference, run, new ComparisonSettings(1), fitHeading: true, cancellation.Token);

        compare.Should().Throw<OperationCanceledException>();
        engine.CachedFitCount.Should().Be(0);
    }
}
