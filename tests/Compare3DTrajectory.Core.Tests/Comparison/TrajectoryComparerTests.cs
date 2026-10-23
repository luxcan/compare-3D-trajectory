using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests.Comparison;

public class TrajectoryComparerTests
{
    private static readonly Route Route = Route.Standard();
    private static readonly ComparisonSettings TwoMetres = new(2);

    [Fact]
    public void Compare_SameShapeRecordedMoreDenselyWithNoise_FindsNoIssues()
    {
        List<Vec3> run = Noise.Jitter(Route.Sample(1), sigma: 0.2, seed: 3);

        ComparisonResult result = Compare(Route.Sample(10), run);

        result.Issues.Should().BeEmpty();
        result.Summary.WithinTolerancePercent.Should().Be(100);
        result.Summary.CoveragePercent.Should().Be(100);
        result.Summary.MaxDeviation.Should().BeLessThan(1.2);
        result.Deviations.Should().HaveCount(run.Count);
    }

    [Fact]
    public void Compare_PlantedDetour_FindsOneDetourOverThePlantedPointsAndTheStretchItMissed()
    {
        // 1 m spacing, so the point index is the distance along. The bulge peaks at 25 m.
        List<Vec3> run = Route.SampleWith(1, (s, position, heading) => Route.Detour(s, position, heading, 500, 600, 25));

        ComparisonResult result = Compare(Route.Sample(10), run);

        TrajectoryIssue detour = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Detour).Subject;
        detour.FirstIndex.Should().BeInRange(500, 506);
        detour.LastIndex.Should().BeInRange(594, 600);
        detour.WorstDeviation.Should().BeApproximately(25, 1);
        detour.Path.Should().HaveCount(detour.LastIndex - detour.FirstIndex + 1);

        TrajectoryIssue missed = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Missed).Subject;
        missed.Length.Should().BeInRange(40, 100);
        Route.At(550).Position.DistanceTo(missed.WorstPoint).Should().BeLessThan(10);
        result.Issues.Should().HaveCount(2);
    }

    [Fact]
    public void Compare_CornerCutWithAStraightLine_FindsTheCutAndTheMissedCorner()
    {
        // The 135° turn runs from 1232.7 m to 1374.1 m along the route; the run goes straight across it.
        (Vec3 cutFrom, _) = Route.At(1220);
        (Vec3 cutTo, _) = Route.At(1385);
        List<Vec3> run = Route.SampleWith(1, (s, position, _) =>
            s is > 1220 and < 1385 ? Vec3.Lerp(cutFrom, cutTo, (s - 1220) / 165) : position);

        ComparisonResult result = Compare(Route.Sample(10), run);

        result.Issues.Select(issue => issue.Kind).Should().BeEquivalentTo([IssueKind.Detour, IssueKind.Missed]);
        TrajectoryIssue missed = result.Issues.Single(issue => issue.Kind == IssueKind.Missed);
        Route.At(1303).Position.DistanceTo(missed.WorstPoint).Should().BeLessThan(15);
        missed.FirstIndex.Should().BeLessThan(missed.LastIndex);
    }

    [Fact]
    public void Compare_RunStartsFarAwayAndConverges_IsOffPathAtTheStart()
    {
        List<Vec3> run = Route.SampleWith(1, (s, position, heading) =>
            s < 80 ? position + new Vec3(0, 40 * (1 - (s / 80)), 0) : position);

        ComparisonResult result = Compare(Route.Sample(10), run);

        TrajectoryIssue offPath = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.OffPath).Subject;
        offPath.FirstIndex.Should().Be(0);
    }

    [Fact]
    public void Compare_RunStopsHalfWay_ReportsTheRestOfTheReferenceAsMissed()
    {
        List<Vec3> run = [.. Route.Sample(1).Take(800)];

        ComparisonResult result = Compare(Route.Sample(10), run);

        TrajectoryIssue missed = result.Issues.Should().ContainSingle().Subject;
        missed.Kind.Should().Be(IssueKind.Missed);
        missed.Length.Should().BeApproximately(Route.Length - 799 - 2, 3);
        result.Summary.CoveragePercent.Should().BeInRange(48, 53);
    }

    [Fact]
    public void Compare_ReportsTheFitItWasGiven()
    {
        var fit = new HeadingFit(new HeadingTransform(0.1, Vec3.Zero), 0.3, Vec3.Zero);
        List<Vec3> points = Route.Sample(5);

        ComparisonResult result = TrajectoryComparer.Compare(new PreparedTrajectory(Guid.NewGuid(), points), points, fit, TwoMetres);

        result.Fit.Should().BeSameAs(fit);
        result.AlignedPoints.Should().BeSameAs(points);
    }

    private static ComparisonResult Compare(List<Vec3> reference, List<Vec3> run) =>
        TrajectoryComparer.Compare(new PreparedTrajectory(Guid.NewGuid(), reference), run, HeadingFit.None, TwoMetres);
}
