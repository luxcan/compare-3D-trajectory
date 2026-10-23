namespace Compare3DTrajectory.Core.Geometry;

/// <summary>A point on a polyline and how far along the polyline it lies, in metres.</summary>
public readonly record struct PathSample(Vec3 Position, double ArcLength);
