using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Geo;

/// <summary>Tells geocentric files from local ones by how far their points are from the Earth's centre.</summary>
public static class CoordinateKindDetector
{
    /// <summary>
    /// The shell a geocentric point must lie in: the polar radius is 6,356.8 km and the equatorial
    /// 6,378.1 km. The margins leave room for any depth or altitude a simulation runs at, while a local
    /// frame would have to be thousands of kilometres across to land inside.
    /// </summary>
    public const double MinimumGeocentricRadius = 6_300_000;

    public const double MaximumGeocentricRadius = 6_450_000;

    public static CoordinateKind Detect(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            return CoordinateKind.Local;
        }

        foreach (Vec3 point in points)
        {
            double radius = point.Length;
            if (radius < MinimumGeocentricRadius || radius > MaximumGeocentricRadius)
            {
                return CoordinateKind.Local;
            }
        }

        return CoordinateKind.Geocentric;
    }
}
