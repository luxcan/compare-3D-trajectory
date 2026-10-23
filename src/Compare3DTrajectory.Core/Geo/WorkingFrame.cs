using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Geo;

/// <summary>
/// The one frame every loaded trajectory is compared in.
/// </summary>
/// <remarks>
/// The East-North-Up origin is the first point of the first geocentric trajectory, and it is kept
/// until <see cref="Reset"/>. Moving it when that file is removed would shift every other file and
/// invalidate every fit, for no gain: any nearby origin is equally good.
/// </remarks>
public sealed class WorkingFrame
{
    public LocalFrame? Enu { get; private set; }

    /// <summary>The points in working metres: East-North-Up when geocentric, unchanged when local.</summary>
    public Vec3[] ToWorking(IReadOnlyList<Vec3> points, CoordinateKind kind)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (kind == CoordinateKind.Local || points.Count == 0)
        {
            return [.. points];
        }

        LocalFrame frame = Enu ??= new LocalFrame(points[0]);
        return [.. points.Select(frame.ToLocal)];
    }

    public void Reset() => Enu = null;
}
