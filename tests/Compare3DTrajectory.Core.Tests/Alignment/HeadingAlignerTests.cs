using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests.Alignment;

public class HeadingAlignerTests
{
    private static readonly Route Route = Route.Standard();

    [Theory]
    [InlineData(35, 400, -250, 3)]
    [InlineData(-120, -1500, 800, -20)]
    [InlineData(178, 50, 50, 0)]
    public void Fit_RunMovedAndTurned_RecoversTheMoveAndTurn(double turn, double east, double north, double up)
    {
        List<Vec3> truth = Noise.Jitter(Route.Sample(1), sigma: 0.2, seed: 1);
        List<Vec3> moved = Noise.Move(truth, turn, new Vec3(east, north, up));
        PolylineIndex reference = new(new Polyline(Route.Sample(10)));

        HeadingFit fit = HeadingAligner.Fit(new Polyline(moved), reference);

        fit.Transform.HeadingDegrees.Should().BeApproximately(-turn, 0.1);
        MaxError(fit, moved, truth).Should().BeLessThan(0.15);
        fit.TrimmedRms.Should().BeLessThan(0.4);
    }

    [Fact]
    public void Fit_RunWithALargeDetour_StillRecoversTheMoveAndTurn()
    {
        // A 40 m bulge over 220 m of a 1.6 km route: about 14% of the run is far off the path.
        List<Vec3> truth = Route.SampleWith(1, (s, position, heading) => Route.Detour(s, position, heading, 450, 670, 40));
        List<Vec3> moved = Noise.Move(truth, 60, new Vec3(-300, 200, 5));
        PolylineIndex reference = new(new Polyline(Route.Sample(10)));

        HeadingFit fit = HeadingAligner.Fit(new Polyline(moved), reference);

        fit.Transform.HeadingDegrees.Should().BeApproximately(-60, 0.5);
        MaxError(fit, [.. moved.Take(400)], [.. truth.Take(400)]).Should().BeLessThan(0.5);
    }

    [Fact]
    public void Fit_MirroredRun_DoesNotFitTheReference()
    {
        // The same turns the other way round: no turn about the vertical makes them match, so the fit
        // must stay visibly poor rather than flip the path over.
        List<Vec3> mirrored = Noise.Mirror(Route.Sample(1));
        PolylineIndex reference = new(new Polyline(Route.Sample(10)));

        HeadingFit fit = HeadingAligner.Fit(new Polyline(mirrored), reference);

        fit.TrimmedRms.Should().BeGreaterThan(10);
    }

    [Fact]
    public void Fit_ReportsHowFarTheRunsCentreMoved()
    {
        List<Vec3> truth = Route.Sample(2);
        List<Vec3> moved = Noise.Move(truth, 0, new Vec3(30, 40, 0));
        PolylineIndex reference = new(new Polyline(Route.Sample(10)));

        HeadingFit fit = HeadingAligner.Fit(new Polyline(moved), reference);

        fit.CentroidShift.Length.Should().BeApproximately(50, 0.1);
        fit.IsFitted.Should().BeTrue();
    }

    private static double MaxError(HeadingFit fit, IReadOnlyList<Vec3> moved, IReadOnlyList<Vec3> truth) =>
        moved.Zip(truth, (point, original) => fit.Transform.Apply(point).DistanceTo(original)).Max();
}
