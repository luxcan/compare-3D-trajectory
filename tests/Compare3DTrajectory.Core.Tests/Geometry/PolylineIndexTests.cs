using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geometry;

public class PolylineIndexTests
{
    [Fact]
    public void Nearest_RandomWalkAndQueries_MatchesBruteForceExactly()
    {
        var random = new Random(7);
        var points = new List<Vec3> { Vec3.Zero };
        for (int i = 0; i < 600; i++)
        {
            // Mostly short steps with the odd long one, so pieces of very different sizes are tested.
            double step = random.NextDouble() < 0.05 ? 200 : 5;
            points.Add(points[^1] + new Vec3(random.NextDouble() - 0.5, random.NextDouble() - 0.5, (random.NextDouble() - 0.5) * 0.2) * step);
        }

        var polyline = new Polyline(points);
        var index = new PolylineIndex(polyline);

        for (int i = 0; i < 2000; i++)
        {
            var query = new Vec3((random.NextDouble() - 0.5) * 800, (random.NextDouble() - 0.5) * 800, (random.NextDouble() - 0.5) * 100);

            NearestPoint found = index.Nearest(query);

            found.Distance.Should().BeApproximately(BruteForceDistance(polyline, query), 1e-9);
            found.Point.DistanceTo(query).Should().BeApproximately(found.Distance, 1e-9);
        }
    }

    [Fact]
    public void Nearest_PointBesideSecondSegment_ReportsSegmentAndDistanceAlong()
    {
        var index = new PolylineIndex(new Polyline([new(0, 0, 0), new(10, 0, 0), new(10, 10, 0)]));

        NearestPoint found = index.Nearest(new Vec3(13, 4, 0));

        found.Point.Should().Be(new Vec3(10, 4, 0));
        found.Distance.Should().BeApproximately(3, 1e-12);
        found.SegmentIndex.Should().Be(1);
        found.ArcLength.Should().BeApproximately(14, 1e-12);
    }

    [Fact]
    public void Nearest_AllPointsIdentical_ReturnsThatPoint()
    {
        var index = new PolylineIndex(new Polyline([new(2, 2, 2), new(2, 2, 2), new(2, 2, 2)]));

        NearestPoint found = index.Nearest(new Vec3(2, 2, 7));

        found.Distance.Should().Be(5);
    }

    private static double BruteForceDistance(Polyline polyline, Vec3 query)
    {
        double best = double.PositiveInfinity;
        for (int i = 0; i < polyline.SegmentCount; i++)
        {
            Vec3 closest = Polyline.ClosestPointOnSegment(query, polyline.Points[i], polyline.Points[i + 1], out _);
            best = Math.Min(best, closest.DistanceTo(query));
        }

        return best;
    }
}
