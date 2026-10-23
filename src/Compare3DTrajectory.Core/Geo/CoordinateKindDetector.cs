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

    /// <summary>
    /// Geocentric when most of the points lie in the shell. Deciding by the bulk means a few unset
    /// rows, such as a WorldLocation of 0, 0, 0 before an entity is placed, cannot turn a whole
    /// geocentric file local; the loader skips those rows instead.
    /// </summary>
    public static CoordinateKind Detect(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        int inShell = points.Count(IsInShell);
        return 2 * inShell > points.Count ? CoordinateKind.Geocentric : CoordinateKind.Local;
    }

    public static bool IsInShell(Vec3 point)
    {
        double radius = point.Length;
        return radius >= MinimumGeocentricRadius && radius <= MaximumGeocentricRadius;
    }
}
