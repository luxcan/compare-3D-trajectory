using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Geo;

/// <summary>
/// The one frame every loaded trajectory is compared in.
/// </summary>
/// <remarks>
/// <para>The East-North-Up origin is the first point near the Earth's surface of the first geocentric
/// trajectory, until it is anchored elsewhere or reset. A point nowhere near the surface, as in a
/// local file switched to geocentric, never becomes the origin: a frame there would turn and tilt
/// every file after it.</para>
///
/// <para>The window anchors the frame on the reference whenever the reference changes, so where a
/// run lands does not depend on which file happened to be opened first.</para>
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

        if (Enu is null && OriginOf(points) is { } origin)
        {
            Enu = new LocalFrame(origin);
        }

        // Points none of which is near the Earth have no sensible frame of their own either; one at
        // their first point keeps them in metres without making it everyone's.
        LocalFrame frame = Enu ?? new LocalFrame(points[0]);
        return [.. points.Select(frame.ToLocal)];
    }

    /// <summary>
    /// Moves the origin to the first point near the Earth's surface of <paramref name="points"/>, and
    /// says whether it moved; every trajectory must then be put into working metres again.
    /// </summary>
    public bool Anchor(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (OriginOf(points) is not { } origin || Enu?.OriginEcef == origin)
        {
            return false;
        }

        Enu = new LocalFrame(origin);
        return true;
    }

    public void Reset() => Enu = null;

    /// <summary>
    /// The points in a level frame of their own: East-North-Up at their own first point near the
    /// Earth's surface when geocentric, so Z is their own vertical wherever they are; unchanged when
    /// local. Fitting a run in its own level frame lets it be moved over the curved Earth without
    /// being tilted.
    /// </summary>
    public static Vec3[] ToLevel(IReadOnlyList<Vec3> points, CoordinateKind kind)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (kind == CoordinateKind.Local || points.Count == 0)
        {
            return [.. points];
        }

        var frame = new LocalFrame(OriginOf(points) ?? points[0]);
        return [.. points.Select(frame.ToLocal)];
    }

    private static Vec3? OriginOf(IReadOnlyList<Vec3> points)
    {
        foreach (Vec3 point in points)
        {
            if (CoordinateKindDetector.IsInShell(point))
            {
                return point;
            }
        }

        return null;
    }
}
