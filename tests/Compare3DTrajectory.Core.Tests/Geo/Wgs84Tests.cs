using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geo;

public class Wgs84Tests
{
    [Fact]
    public void ToEcef_EquatorAtPrimeMeridian_IsOnTheSemiMajorAxis()
    {
        Vec3 ecef = Wgs84.ToEcef(new GeodeticPosition(0, 0, 0));

        ecef.X.Should().BeApproximately(6_378_137.0, 1e-6);
        ecef.Y.Should().BeApproximately(0, 1e-6);
        ecef.Z.Should().BeApproximately(0, 1e-6);
    }

    [Fact]
    public void ToEcef_NorthPole_IsOnTheSemiMinorAxis()
    {
        Vec3 ecef = Wgs84.ToEcef(new GeodeticPosition(90, 0, 0));

        ecef.Z.Should().BeApproximately(6_356_752.314245, 1e-5);
        new Vec3(ecef.X, ecef.Y, 0).Length.Should().BeLessThan(1e-6);
    }

    [Theory]
    [InlineData(1.3521, 103.8198, 50)]
    [InlineData(-33.8688, 151.2093, 12_000)]
    [InlineData(51.4700, -0.4543, -30)]
    [InlineData(89.9, 45, 100)]
    [InlineData(-89.95, -120, 2_000)]
    public void ToGeodetic_RoundTrip_RecoversPositionToBelowAMillimetre(double latitude, double longitude, double height)
    {
        var position = new GeodeticPosition(latitude, longitude, height);

        GeodeticPosition back = Wgs84.ToGeodetic(Wgs84.ToEcef(position));

        back.LatitudeDegrees.Should().BeApproximately(latitude, 1e-9);
        back.LongitudeDegrees.Should().BeApproximately(longitude, 1e-9);
        back.Height.Should().BeApproximately(height, 1e-3);
    }
}
