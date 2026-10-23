using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Alignment;

public class HeadingTransformTests
{
    [Fact]
    public void Apply_QuarterTurn_TurnsEastIntoNorthAndKeepsHeight()
    {
        var transform = new HeadingTransform(Math.PI / 2, new Vec3(10, 0, 1));

        Vec3 moved = transform.Apply(new Vec3(1, 0, 5));

        moved.X.Should().BeApproximately(10, 1e-12);
        moved.Y.Should().BeApproximately(1, 1e-12);
        moved.Z.Should().Be(6);
    }

    [Theory]
    [InlineData(190, -170)]
    [InlineData(-190, 170)]
    [InlineData(540, 180)]
    [InlineData(-45, -45)]
    public void HeadingDegrees_OutOfRange_IsWrappedIntoPlusMinus180(double degrees, double expected)
    {
        new HeadingTransform(degrees * Math.PI / 180, Vec3.Zero).HeadingDegrees.Should().BeApproximately(expected, 1e-9);
    }

    [Fact]
    public void FitPairs_PointsMovedByAKnownTransform_RecoversItExactly()
    {
        Vec3[] from = [new(0, 0, 0), new(10, 0, 1), new(10, 5, 2), new(-3, 8, 0)];
        var known = new HeadingTransform(0.7, new Vec3(100, -50, 4));
        Vec3[] to = [.. from.Select(known.Apply)];

        HeadingTransform fitted = HeadingTransform.FitPairs(from, to);

        fitted.HeadingRadians.Should().BeApproximately(0.7, 1e-12);
        (fitted.Offset - known.Offset).Length.Should().BeLessThan(1e-9);
    }
}
