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

    /// <summary>
    /// F17: a local file switched to geocentric set the origin to a point near the Earth's centre, and
    /// every geocentric file after it was then turned and tilted.
    /// </summary>
    [Fact]
    public void ToWorking_GeocentricPointsNowhereNearTheEarth_DoNotSetTheOrigin()
    {
        var frame = new WorkingFrame();
        frame.ToWorking([new Vec3(0, 0, 0), new Vec3(10, 0, 0)], CoordinateKind.Geocentric);

        frame.Enu.Should().BeNull();
        Vec3 real = Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10));
        frame.ToWorking([real], CoordinateKind.Geocentric)[0].Length.Should().BeLessThan(1e-6);
    }

    [Fact]
    public void Anchor_OnAnotherFile_MovesTheOriginToItsFirstPoint()
    {
        var frame = new WorkingFrame();
        frame.ToWorking([Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10))], CoordinateKind.Geocentric);
        Vec3 elsewhere = Wgs84.ToEcef(new GeodeticPosition(1.9, 104.1, 30));

        frame.Anchor([elsewhere]).Should().BeTrue();

        frame.ToWorking([elsewhere], CoordinateKind.Geocentric)[0].Length.Should().BeLessThan(1e-6);
        frame.Anchor([elsewhere]).Should().BeFalse("the origin is already there");
    }

    /// <summary>F61: a run's own level frame keeps its own vertical as up, wherever on the Earth it is.</summary>
    [Fact]
    public void ToLevel_GeocentricPoints_AreMetresFromTheirOwnFirstPointWithTheirOwnUp()
    {
        Vec3 first = Wgs84.ToEcef(new GeodeticPosition(1.9, 104.1, 10));
        Vec3 above = Wgs84.ToEcef(new GeodeticPosition(1.9, 104.1, 60));

        Vec3[] level = WorkingFrame.ToLevel([first, above], CoordinateKind.Geocentric);

        level[0].Length.Should().BeLessThan(1e-6);
        level[1].Z.Should().BeApproximately(50, 1e-6);
        WorkingFrame.ToLevel([new Vec3(1, 2, 3)], CoordinateKind.Local).Should().Equal(new Vec3(1, 2, 3));
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
