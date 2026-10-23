using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geo;

public class WorkingFrameTests
{
    [Fact]
    public void ToWorking_GeocentricPoints_BecomeMetresFromTheFirstPoint()
    {
        var frame = new WorkingFrame();
        Vec3 first = Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10));
        Vec3 second = Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 60));

        Vec3[] working = frame.ToWorking([first, second], CoordinateKind.Geocentric);

        working[0].Length.Should().BeLessThan(1e-6);
        working[1].Z.Should().BeApproximately(50, 1e-6);
    }

    [Fact]
    public void ToWorking_SecondGeocentricFile_SharesTheFirstFilesOrigin()
    {
        var frame = new WorkingFrame();
        Vec3 origin = Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10));
        frame.ToWorking([origin], CoordinateKind.Geocentric);

        Vec3[] later = frame.ToWorking([Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 30))], CoordinateKind.Geocentric);

        later[0].Z.Should().BeApproximately(20, 1e-6);
    }

    [Fact]
    public void ToWorking_LocalPoints_AreUnchanged()
    {
        var frame = new WorkingFrame();

        Vec3[] working = frame.ToWorking([new Vec3(1, 2, 3)], CoordinateKind.Local);

        working.Should().Equal(new Vec3(1, 2, 3));
        frame.Enu.Should().BeNull();
    }

    [Fact]
    public void Reset_AfterGeocentricFile_ForgetsTheOrigin()
    {
        var frame = new WorkingFrame();
        frame.ToWorking([Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10))], CoordinateKind.Geocentric);

        frame.Reset();

        frame.Enu.Should().BeNull();
    }
}
