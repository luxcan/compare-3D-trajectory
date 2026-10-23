using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Synthetic;

/// <summary>Seeded jitter and rigid moves for synthetic paths, so every run of a test sees the same data.</summary>
internal static class Noise
{
    /// <summary>Adds normally distributed noise of the given standard deviation to every coordinate.</summary>
    public static List<Vec3> Jitter(IEnumerable<Vec3> points, double sigma, int seed)
    {
        var random = new Random(seed);
        return [.. points.Select(point => point + new Vec3(Gaussian(random), Gaussian(random), Gaussian(random)) * sigma)];
    }

    public static List<Vec3> Move(IEnumerable<Vec3> points, double headingDegrees, Vec3 offset)
    {
        var transform = new HeadingTransform(headingDegrees * Math.PI / 180, offset);
        return [.. points.Select(transform.Apply)];
    }

    public static List<Vec3> Mirror(IEnumerable<Vec3> points) =>
        [.. points.Select(point => point with { Y = -point.Y })];

    private static double Gaussian(Random random)
    {
        double u1 = 1 - random.NextDouble();
        double u2 = random.NextDouble();
        return Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
    }
}
