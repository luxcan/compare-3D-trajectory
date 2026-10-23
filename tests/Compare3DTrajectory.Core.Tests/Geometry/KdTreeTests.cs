using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geometry;

public class KdTreeTests
{
    [Fact]
    public void Nearest_RandomPointsWithDuplicates_MatchesBruteForce()
    {
        var random = new Random(11);
        List<Vec3> points = [.. Enumerable.Range(0, 3000).Select(_ => RandomPoint(random, 1000))];
        points.AddRange(points.Take(200)); // duplicates must not upset the median split
        var tree = new KdTree(points);

        for (int i = 0; i < 1000; i++)
        {
            Vec3 query = RandomPoint(random, 1200);

            int found = tree.Nearest(query, out double distanceSquared);

            double expected = points.Min(point => point.DistanceSquaredTo(query));
            distanceSquared.Should().Be(expected);
            points[found].DistanceSquaredTo(query).Should().Be(expected);
        }
    }

    [Fact]
    public void WithinRadius_RandomPoints_FindsExactlyThePointsInside()
    {
        var random = new Random(13);
        List<Vec3> points = [.. Enumerable.Range(0, 2000).Select(_ => RandomPoint(random, 100))];
        var tree = new KdTree(points);
        var found = new List<int>();

        for (int i = 0; i < 200; i++)
        {
            Vec3 query = RandomPoint(random, 120);
            found.Clear();

            tree.WithinRadius(query, 15, found);

            int[] expected = [.. Enumerable.Range(0, points.Count).Where(index => points[index].DistanceTo(query) <= 15)];
            found.Should().BeEquivalentTo(expected);
        }
    }

    [Fact]
    public void Nearest_SinglePoint_ReturnsIt()
    {
        var tree = new KdTree([new Vec3(1, 2, 3)]);

        tree.Nearest(new Vec3(4, 6, 3), out double distanceSquared).Should().Be(0);
        distanceSquared.Should().Be(25);
    }

    private static Vec3 RandomPoint(Random random, double size) =>
        new((random.NextDouble() - 0.5) * size, (random.NextDouble() - 0.5) * size, (random.NextDouble() - 0.5) * size * 0.1);
}
