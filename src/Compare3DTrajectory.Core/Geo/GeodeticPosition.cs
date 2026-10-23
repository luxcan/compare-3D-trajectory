namespace Compare3DTrajectory.Core.Geo;

/// <summary>A WGS84 latitude and longitude in degrees, and height above the ellipsoid in metres.</summary>
public readonly record struct GeodeticPosition(double LatitudeDegrees, double LongitudeDegrees, double Height);
