using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geo;

public class CoordinateKindDetectorTests
{
    [Fact]
    public void Detect_PointsOnTheEarthsSurface_IsGeocentric()
    {
        Vec3[] points =
        [
            Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 0)),
            Wgs84.ToEcef(new GeodeticPosition(1.36, 103.83, 11_000)),
        ];

        CoordinateKindDetector.Detect(points).Should().Be(CoordinateKind.Geocentric);
    }

    [Fact]
    public void Detect_SmallLocalCoordinates_IsLocal()
    {
        CoordinateKindDetector.Detect([new(0, 0, 0), new(120, 45, 3)]).Should().Be(CoordinateKind.Local);
    }

    /// <summary>F12: one uninitialised 0,0,0 row used to make a whole geocentric file local.</summary>
    [Fact]
    public void Detect_MostPointsOnTheSurfaceAndOneAtTheCentre_IsGeocentric()
    {
        Vec3[] points =
        [
            Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 0)),
            new Vec3(0, 0, 0),
            Wgs84.ToEcef(new GeodeticPosition(1.351, 103.82, 0)),
            Wgs84.ToEcef(new GeodeticPosition(1.352, 103.82, 0)),
        ];

        CoordinateKindDetector.Detect(points).Should().Be(CoordinateKind.Geocentric);
    }

    [Fact]
    public void Detect_HalfThePointsOffTheSurface_IsLocal()
    {
        Vec3[] points =
        [
            Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 0)),
            new Vec3(0, 0, 0),
        ];

        CoordinateKindDetector.Detect(points).Should().Be(CoordinateKind.Local);
    }
}
