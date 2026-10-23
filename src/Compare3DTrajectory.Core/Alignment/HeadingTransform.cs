using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Alignment;

/// <summary>
/// A turn about the vertical (Z) axis through the frame's origin, followed by a move. There is no
/// tilt and no scaling, so a left turn can never be fitted onto a right turn.
/// </summary>
/// <param name="HeadingRadians">Anticlockwise seen from above (East towards North).</param>
/// <param name="Offset">The move applied after the turn, in metres.</param>
public readonly record struct HeadingTransform(double HeadingRadians, Vec3 Offset)
{
    public static HeadingTransform Identity => default;

    /// <summary>The turn in degrees, between -180 and 180, anticlockwise seen from above.</summary>
    public double HeadingDegrees
    {
        get
        {
            double degrees = HeadingRadians * 180 / Math.PI % 360;
            return degrees switch
            {
                > 180 => degrees - 360,
                <= -180 => degrees + 360,
                _ => degrees,
            };
        }
    }

    public Vec3 Apply(Vec3 point)
    {
        double cos = Math.Cos(HeadingRadians);
        double sin = Math.Sin(HeadingRadians);
        return new Vec3(
            (cos * point.X) - (sin * point.Y) + Offset.X,
            (sin * point.X) + (cos * point.Y) + Offset.Y,
            point.Z + Offset.Z);
    }

    /// <summary>
    /// The turn and move that best carry each <paramref name="from"/> point onto its partner in
    /// <paramref name="to"/>, in the least-squares sense.
    /// </summary>
    /// <remarks>
    /// With both sets centred on their centroids, the squared error is smallest at
    /// θ = atan2(Σ pₓq_y − p_yqₓ, Σ pₓqₓ + p_yq_y). The move then carries the turned centroid of
    /// <paramref name="from"/> onto the centroid of <paramref name="to"/>.
    /// </remarks>
    public static HeadingTransform FitPairs(IReadOnlyList<Vec3> from, IReadOnlyList<Vec3> to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);
        if (from.Count != to.Count || from.Count == 0)
        {
            throw new ArgumentException("Fitting needs the same, non-zero number of points on both sides.", nameof(to));
        }

        Vec3 fromCentre = Vec3.Centroid(from);
        Vec3 toCentre = Vec3.Centroid(to);
        double alongSum = 0;
        double acrossSum = 0;
        for (int i = 0; i < from.Count; i++)
        {
            Vec3 p = from[i] - fromCentre;
            Vec3 q = to[i] - toCentre;
            alongSum += (p.X * q.X) + (p.Y * q.Y);
            acrossSum += (p.X * q.Y) - (p.Y * q.X);
        }

        double heading = Math.Atan2(acrossSum, alongSum);
        Vec3 turnedCentre = new HeadingTransform(heading, Vec3.Zero).Apply(fromCentre);
        return new HeadingTransform(heading, toCentre - turnedCentre);
    }
}
