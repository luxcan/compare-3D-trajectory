using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geo;

public class LocalFrameTests
{
    private static readonly GeodeticPosition Singapore = new(1.3521, 103.8198, 20);

    [Fact]
    public void ToLocal_PointStraightAboveOrigin_IsUp()
    {
        var frame = new LocalFrame(Wgs84.ToEcef(Singapore));

        Vec3 local = frame.ToLocal(Wgs84.ToEcef(Singapore with { Height = 120 }));

        local.X.Should().BeApproximately(0, 1e-6);
        local.Y.Should().BeApproximately(0, 1e-6);
        local.Z.Should().BeApproximately(100, 1e-6);
    }

    [Fact]
    public void ToLocal_PointSlightlyNorthAndEast_HasPositiveNorthAndEast()
    {
        var frame = new LocalFrame(Wgs84.ToEcef(Singapore));

        // About 111 m north and 111 m east at this latitude.
        Vec3 local = frame.ToLocal(Wgs84.ToEcef(Singapore with { LatitudeDegrees = 1.3531, LongitudeDegrees = 103.8208 }));

        local.X.Should().BeInRange(110, 112);
        local.Y.Should().BeInRange(110, 112);
        Math.Abs(local.Z).Should().BeLessThan(0.01);
    }

    [Fact]
    public void ToEcef_RoundTrip_RecoversPositionToMicrometres()
    {
        var frame = new LocalFrame(Wgs84.ToEcef(Singapore));
        Vec3 ecef = Wgs84.ToEcef(new GeodeticPosition(1.40, 103.90, 300));

        frame.ToEcef(frame.ToLocal(ecef)).DistanceTo(ecef).Should().BeLessThan(1e-6);
    }

    [Fact]
    public void Origin_IsTheGeodeticPositionOfTheOriginPoint()
    {
        var frame = new LocalFrame(Wgs84.ToEcef(Singapore));

        frame.Origin.LatitudeDegrees.Should().BeApproximately(Singapore.LatitudeDegrees, 1e-9);
        frame.Origin.LongitudeDegrees.Should().BeApproximately(Singapore.LongitudeDegrees, 1e-9);
    }
}
