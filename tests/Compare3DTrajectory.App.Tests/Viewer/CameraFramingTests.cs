using System.Numerics;
using System.Windows.Media.Media3D;
using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Tests.Viewer;

public class CameraFramingTests
{
    /// <summary>The view's own camera: 45° vertical field of view, looking north and down at 37°.</summary>
    private const double FieldOfView = 45;

    private static readonly Vector3D StartingLook = new(0, 600, -450);

    private static readonly Vector3D ModelUp = new(0, 0, 1);

    /// <summary>
    /// F42: after the top face of the view cube, the camera looks straight down; framing with Z as
    /// up then gave a view matrix of NaN, and the view went blank for good.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(1)]
    public void Frame_LookingStraightDownOrUp_KeepsAnUpThatIsNotAlongTheLook(double sign)
    {
        Vector3[] points = [new(-100, -50, 0), new(100, 50, 20)];

        CameraPlacement placement = CameraFraming.FitAll(points, new Vector3D(0, 0, sign * 500), new Vector3D(0, 1, 0), FieldOfView, 1.6);

        Vector3D across = Vector3D.CrossProduct(placement.LookDirection, placement.UpDirection);
        across.Length.Should().BeGreaterThan(1e-3 * placement.LookDirection.Length * placement.UpDirection.Length);
        double.IsFinite(placement.Position.X + placement.Position.Y + placement.Position.Z).Should().BeTrue();
    }

    [Fact]
    public void Frame_LookingAtAnAngle_KeepsZUp()
    {
        CameraPlacement placement = CameraFraming.FitAll([new(0, 0, 0), new(100, 0, 0)], StartingLook, ModelUp, FieldOfView, 1.6);

        placement.UpDirection.Should().Be(ModelUp);
    }

    /// <summary>
    /// F43: a straight 10 km leg, framed in the default window from the starting angle, had its
    /// southern end and the start marker below the bottom edge.
    /// </summary>
    [Theory]
    [InlineData(895, 460)]
    [InlineData(440, 460)]
    [InlineData(1800, 400)]
    public void Frame_StraightLegFromTheStartingAngle_KeepsEveryPointOnScreen(double width, double height)
    {
        Vector3[] leg = [.. Enumerable.Range(0, 101).Select(i => new Vector3(0, -5000 + (100 * i), 0))];

        CameraPlacement placement = CameraFraming.FitAll(leg, StartingLook, ModelUp, FieldOfView, width / height);

        foreach (Vector3 point in leg)
        {
            (double x, double y) = Project(placement, point, width / height);
            x.Should().BeInRange(-1, 1);
            y.Should().BeInRange(-1, 1);
        }
    }

    [Fact]
    public void Frame_Box_FillsMostOfTheView()
    {
        Vector3[] corners = [new(-10, -10, -10), new(10, 10, 10)];

        CameraPlacement placement = CameraFraming.Focus(corners[0], corners[1], StartingLook, ModelUp, FieldOfView, 1.6);

        double widest = corners.SelectMany(corner => Box(corner)).Select(point => Project(placement, point, 1.6))
            .Max(projected => Math.Max(Math.Abs(projected.X), Math.Abs(projected.Y)));
        widest.Should().BeInRange(0.7, 1);
    }

    private static IEnumerable<Vector3> Box(Vector3 corner) =>
        from x in new[] { -corner.X, corner.X }
        from y in new[] { -corner.Y, corner.Y }
        from z in new[] { -corner.Z, corner.Z }
        select new Vector3(x, y, z);

    /// <summary>Where a point lands on screen, from -1 to 1 across and up, for a perspective camera.</summary>
    private static (double X, double Y) Project(CameraPlacement placement, Vector3 point, double widthOverHeight)
    {
        Vector3D forward = placement.LookDirection;
        forward.Normalize();
        Vector3D right = Vector3D.CrossProduct(forward, placement.UpDirection);
        right.Normalize();
        Vector3D up = Vector3D.CrossProduct(right, forward);
        Vector3D offset = new Point3D(point.X, point.Y, point.Z) - placement.Position;
        double depth = Vector3D.DotProduct(offset, forward);
        double tanVertical = Math.Tan(FieldOfView * Math.PI / 360);
        return (Vector3D.DotProduct(offset, right) / (depth * tanVertical * widthOverHeight),
                Vector3D.DotProduct(offset, up) / (depth * tanVertical));
    }
}
