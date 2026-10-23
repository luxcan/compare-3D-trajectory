using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Geometry;

public class PolylineTests
{
    [Fact]
    public void Constructor_FewerThanTwoPoints_Throws()
    {
        Action build = () => _ = new Polyline([new Vec3(1, 2, 3)]);

        build.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Length_ThreeSegments_SumsSegmentLengths()
    {
        var polyline = new Polyline([new(0, 0, 0), new(3, 4, 0), new(3, 4, 12), new(3, 4, 12)]);

        polyline.Length.Should().Be(17);
        polyline.ArcLengthAt(2).Should().Be(17);
    }

    [Fact]
    public void Resample_UnevenSpacing_GivesEvenSpacingAndExactEnds()
    {
        var polyline = new Polyline([new(0, 0, 0), new(1, 0, 0), new(10, 0, 0)]);

        PathSample[] samples = polyline.Resample(11);

        samples.Select(sample => sample.Position.X).Should().Equal(0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        samples[^1].ArcLength.Should().Be(10);
    }

    [Fact]
    public void SampleEvery_SpacingDoesNotDivideLength_KeepsSamplesNoFurtherApartThanAsked()
    {
        var polyline = new Polyline([new(0, 0, 0), new(10, 0, 0)]);

        PathSample[] samples = polyline.SampleEvery(3);

        samples.Should().HaveCount(5);
        samples.Zip(samples.Skip(1), (a, b) => b.ArcLength - a.ArcLength).Should().OnlyContain(gap => gap <= 3);
    }

    [Theory]
    [InlineData(-5, 1, 0, 0)]
    [InlineData(5, 3, 5, 0.5)]
    [InlineData(15, -2, 10, 1)]
    public void ClosestPointOnSegment_QueryBeforeAlongOrAfter_ClampsToSegment(double x, double y, double expectedX, double expectedFraction)
    {
        Vec3 closest = Polyline.ClosestPointOnSegment(new Vec3(x, y, 0), new Vec3(0, 0, 0), new Vec3(10, 0, 0), out double fraction);

        closest.Should().Be(new Vec3(expectedX, 0, 0));
        fraction.Should().Be(expectedFraction);
    }

    [Fact]
    public void ClosestPointOnSegment_ZeroLengthSegment_ReturnsItsPoint()
    {
        Vec3 closest = Polyline.ClosestPointOnSegment(new Vec3(5, 5, 5), new Vec3(1, 1, 1), new Vec3(1, 1, 1), out double fraction);

        closest.Should().Be(new Vec3(1, 1, 1));
        fraction.Should().Be(0);
    }

    [Fact]
    public void SegmentIndexAt_BeyondEitherEnd_ClampsToFirstAndLastSegment()
    {
        var polyline = new Polyline([new(0, 0, 0), new(1, 0, 0), new(2, 0, 0)]);

        polyline.SegmentIndexAt(-1).Should().Be(0);
        polyline.SegmentIndexAt(1.5).Should().Be(1);
        polyline.SegmentIndexAt(99).Should().Be(1);
    }
}
