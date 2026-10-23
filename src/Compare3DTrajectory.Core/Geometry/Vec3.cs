namespace Compare3DTrajectory.Core.Geometry;

/// <summary>
/// A position or offset in metres.
/// </summary>
/// <remarks>
/// Double precision on purpose: a geocentric position is millions of metres from the Earth's centre,
/// and a float there resolves only to about half a metre.
/// </remarks>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static Vec3 Zero => default;

    public double LengthSquared => (X * X) + (Y * Y) + (Z * Z);

    public double Length => Math.Sqrt(LengthSquared);

    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);

    public static Vec3 operator +(Vec3 left, Vec3 right) => new(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

    public static Vec3 operator -(Vec3 left, Vec3 right) => new(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

    public static Vec3 operator *(Vec3 vector, double scale) => new(vector.X * scale, vector.Y * scale, vector.Z * scale);

    public static Vec3 operator *(double scale, Vec3 vector) => vector * scale;

    public static Vec3 operator /(Vec3 vector, double divisor) => new(vector.X / divisor, vector.Y / divisor, vector.Z / divisor);

    public double Dot(Vec3 other) => (X * other.X) + (Y * other.Y) + (Z * other.Z);

    public double DistanceTo(Vec3 other) => (this - other).Length;

    public double DistanceSquaredTo(Vec3 other) => (this - other).LengthSquared;

    /// <summary>The coordinate on one axis: 0 is X, 1 is Y, 2 is Z.</summary>
    public double this[int axis] => axis switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(axis), axis, "Axis must be 0, 1 or 2."),
    };

    public static Vec3 Lerp(Vec3 from, Vec3 to, double fraction) => from + ((to - from) * fraction);

    public static Vec3 Min(Vec3 left, Vec3 right) =>
        new(Math.Min(left.X, right.X), Math.Min(left.Y, right.Y), Math.Min(left.Z, right.Z));

    public static Vec3 Max(Vec3 left, Vec3 right) =>
        new(Math.Max(left.X, right.X), Math.Max(left.Y, right.Y), Math.Max(left.Z, right.Z));

    public static Vec3 Centroid(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count == 0)
        {
            throw new ArgumentException("A centroid needs at least one point.", nameof(points));
        }

        Vec3 sum = Zero;
        foreach (Vec3 point in points)
        {
            sum += point;
        }

        return sum / points.Count;
    }
}
