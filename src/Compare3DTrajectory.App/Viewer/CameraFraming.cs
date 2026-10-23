using System.Numerics;
using System.Windows.Media.Media3D;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>Where the camera goes to frame what is drawn, looking the way it already looks.</summary>
/// <remarks>
/// The distance is worked out from every point against both fields of view, rather than from a
/// bounding sphere. Seen at an angle, a long flat route's near end lies lower on screen than its far
/// end, and a sphere either left too much room or, brought closer by a guessed factor, cut the near
/// end off.
/// </remarks>
public static class CameraFraming
{
    /// <summary>The room left round what is framed, as a share of the view.</summary>
    private const double Margin = 1.08;

    /// <summary>No point is framed nearer than this, in metres, in front of the camera.</summary>
    private const double NearestDepth = 1;

    /// <summary>A single point, or a path that never moves, is framed from this far.</summary>
    private const double ShortestDistance = 5;

    private static readonly Vector3D ModelUp = new(0, 0, 1);

    /// <summary>Within a degree of straight down or up, Z cannot serve as the up direction.</summary>
    private static readonly double NearlyVertical = Math.Cos(Math.PI / 180);

    /// <summary>Frames every point drawn.</summary>
    public static CameraPlacement FitAll(IReadOnlyList<Vector3> points, Vector3D look, Vector3D up, double verticalFieldOfViewDegrees, double widthOverHeight) =>
        Frame(points, look, up, verticalFieldOfViewDegrees, widthOverHeight);

    /// <summary>Frames one box, by its eight corners.</summary>
    public static CameraPlacement Focus(Vector3 min, Vector3 max, Vector3D look, Vector3D up, double verticalFieldOfViewDegrees, double widthOverHeight)
    {
        Vector3[] corners =
        [
            .. from x in new[] { min.X, max.X }
               from y in new[] { min.Y, max.Y }
               from z in new[] { min.Z, max.Z }
               select new Vector3(x, y, z),
        ];
        return Frame(corners, look, up, verticalFieldOfViewDegrees, widthOverHeight);
    }

    private static CameraPlacement Frame(IReadOnlyList<Vector3> points, Vector3D look, Vector3D up, double verticalFieldOfViewDegrees, double widthOverHeight)
    {
        ArgumentNullException.ThrowIfNull(points);
        Vector3D forward = look;
        forward.Normalize();
        Vector3D screenUp = UpFor(forward, up);
        Vector3D right = Vector3D.CrossProduct(forward, screenUp);
        right.Normalize();
        Vector3D upward = Vector3D.CrossProduct(right, forward);

        Point3D centre = Centre(points);
        double tanVertical = Math.Tan(verticalFieldOfViewDegrees * Math.PI / 360) / Margin;
        double tanHorizontal = tanVertical * widthOverHeight;
        double distance = ShortestDistance;
        foreach (Vector3 point in points)
        {
            Vector3D offset = new Point3D(point.X, point.Y, point.Z) - centre;
            double across = Math.Abs(Vector3D.DotProduct(offset, right)) / tanHorizontal;
            double above = Math.Abs(Vector3D.DotProduct(offset, upward)) / tanVertical;
            distance = Math.Max(distance, Math.Max(NearestDepth, Math.Max(across, above)) - Vector3D.DotProduct(offset, forward));
        }

        return new CameraPlacement(centre - (forward * distance), forward * distance, screenUp);
    }

    /// <summary>
    /// Z, unless the camera looks straight down or up, as after the view cube's top or bottom face:
    /// a view matrix built from an up along the look is NaN, and the view goes blank for good. Then
    /// the camera's own up is kept, made square to the look.
    /// </summary>
    private static Vector3D UpFor(Vector3D forward, Vector3D current)
    {
        if (Math.Abs(Vector3D.DotProduct(forward, ModelUp)) < NearlyVertical)
        {
            return ModelUp;
        }

        Vector3D up = current - (forward * Vector3D.DotProduct(current, forward));
        if (up.Length < 1e-9)
        {
            up = new Vector3D(0, 1, 0) - (forward * forward.Y);
        }

        up.Normalize();
        return up;
    }

    private static Point3D Centre(IReadOnlyList<Vector3> points)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (Vector3 point in points)
        {
            min = Vector3.Min(min, point);
            max = Vector3.Max(max, point);
        }

        Vector3 centre = (min + max) / 2;
        return new Point3D(centre.X, centre.Y, centre.Z);
    }
}

/// <param name="Position">Where the camera stands.</param>
/// <param name="LookDirection">From the camera to the centre of what it frames, as HelixToolkit takes it.</param>
/// <param name="UpDirection">Which way is up on screen; never along the look direction.</param>
public readonly record struct CameraPlacement(Point3D Position, Vector3D LookDirection, Vector3D UpDirection);
