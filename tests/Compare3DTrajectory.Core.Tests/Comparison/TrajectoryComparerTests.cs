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

    /// <summary>
    /// F28: the reference was sampled evenly along its length, which steps over its corners, so a
    /// corner cut a little beyond tolerance was never seen.
    /// </summary>
    [Theory]
    [InlineData(100.0)]
    [InlineData(100.3)]
    [InlineData(101.7)]
    [InlineData(123.45)]
    public void Compare_CornerCutJustBeyondTolerance_IsMissedWhereverTheCornerFalls(double corner)
    {
        List<Vec3> model = [new(0, 0, 0), new(corner, 0, 0), new(corner, 200, 0)];
        List<Vec3> run = [.. Line(new Vec3(0, 0, 0), new Vec3(corner - 4.5, 0, 0), 0.5), .. Line(new Vec3(corner - 4.5, 0, 0), new Vec3(corner, 4.5, 0), 0.5), .. Line(new Vec3(corner, 4.5, 0), new Vec3(corner, 200, 0), 0.5)];

        ComparisonResult result = TrajectoryComparer.Compare(new PreparedTrajectory(Guid.NewGuid(), model), run, HeadingFit.None, new ComparisonSettings(3));

        TrajectoryIssue missed = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Missed).Subject;
        missed.WorstDeviation.Should().BeApproximately(3.18, 0.01);
    }

    /// <summary>F30: a miss of one sample just past a corner gave the next vertex, 100 m on, as both its ends.</summary>
    [Fact]
    public void Compare_OneSampleMissJustPastACorner_RangeTakesInTheCorner()
    {
        List<Vec3> model = [new(0, 0, 0), new(100, 0, 0), new(200.2, 0, 0), new(200.2, 100, 0), new(200.2, 200, 0)];
        List<Vec3> run = [.. Line(new Vec3(0, 0, 0), new Vec3(195.25, 0, 0), 0.5), .. Line(new Vec3(195.25, 0, 0), new Vec3(200.2, 4.95, 0), 0.5), .. Line(new Vec3(200.2, 4.95, 0), new Vec3(200.2, 200, 0), 0.5)];

        ComparisonResult result = TrajectoryComparer.Compare(new PreparedTrajectory(Guid.NewGuid(), model), run, HeadingFit.None, new ComparisonSettings(3));

        TrajectoryIssue missed = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Missed).Subject;
        missed.FirstIndex.Should().Be(2);
        missed.LastIndex.Should().BeInRange(2, 3);
        missed.Length.Should().BeGreaterThan(0);
    }

    /// <summary>F59: a one-row detour was listed as 0.00 m long.</summary>
    [Fact]
    public void Compare_OneRowSpike_HasTheLengthOfPathItSpans()
    {
        List<Vec3> run = Route.Sample(1);
        run[800] += new Vec3(0, 0, 50);

        ComparisonResult result = Compare(Route.Sample(10), run);

        TrajectoryIssue detour = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Detour).Subject;
        detour.FirstIndex.Should().Be(800);
        detour.Length.Should().BeApproximately(2, 0.2);
    }

    /// <summary>F81: a detour's length was measured along the run's own noise, a 60 m detour reading 40% longer.</summary>
    [Fact]
    public void Compare_DetourOfANoisyRun_IsMeasuredAlongTheReference()
    {
        List<Vec3> run = Noise.Jitter(
            Route.SampleWith(1, (s, position, heading) => Route.Detour(s, position, heading, 500, 560, 30)),
            sigma: 0.6,
            seed: 8);

        // Judged at 5 m, well clear of the noise, so the 30 m detour is the only issue.
        ComparisonResult result = TrajectoryComparer.Compare(
            new PreparedTrajectory(Guid.NewGuid(), Route.Sample(10)), run, HeadingFit.None, new ComparisonSettings(5));

        TrajectoryIssue detour = result.Issues.Should().ContainSingle(issue => issue.Kind == IssueKind.Detour).Subject;
        detour.Length.Should().BeInRange(45, 65);
    }

    private static IEnumerable<Vec3> Line(Vec3 from, Vec3 to, double spacing)
    {
        int steps = Math.Max(1, (int)Math.Ceiling(from.DistanceTo(to) / spacing));
        return Enumerable.Range(0, steps).Select(i => Vec3.Lerp(from, to, (double)i / steps));
    }

    private static ComparisonResult Compare(List<Vec3> reference, List<Vec3> run) =>
        TrajectoryComparer.Compare(new PreparedTrajectory(Guid.NewGuid(), reference), run, HeadingFit.None, TwoMetres);
}
