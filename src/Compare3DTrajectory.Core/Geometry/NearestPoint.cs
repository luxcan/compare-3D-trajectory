namespace Compare3DTrajectory.Core.Geometry;

/// <summary>
/// The closest point on a polyline to some query point.
/// </summary>
/// <param name="Point">The closest point itself, which may lie between two vertices.</param>
/// <param name="Distance">Metres from the query to <paramref name="Point"/>.</param>
/// <param name="SegmentIndex">The segment it lies on: segment i runs from vertex i to vertex i + 1.</param>
/// <param name="ArcLength">Metres along the polyline from its first vertex.</param>
public readonly record struct NearestPoint(Vec3 Point, double Distance, int SegmentIndex, double ArcLength);
