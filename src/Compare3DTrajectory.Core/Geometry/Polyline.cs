namespace Compare3DTrajectory.Core.Geometry;

/// <summary>
/// An ordered path through two or more points, with the distance travelled to each one.
/// </summary>
public sealed class Polyline
{
    private readonly Vec3[] _points;
    private readonly double[] _arcLengths;

    public Polyline(IReadOnlyList<Vec3> points)
    {
        ArgumentNullException.ThrowIfNull(points);
        if (points.Count < 2)
        {
            throw new ArgumentException("A path needs at least two points.", nameof(points));
        }

        _points = [.. points];
        _arcLengths = new double[_points.Length];
        for (int i = 1; i < _points.Length; i++)
        {
            _arcLengths[i] = _arcLengths[i - 1] + _points[i - 1].DistanceTo(_points[i]);
        }
    }

    public IReadOnlyList<Vec3> Points => _points;

    public int Count => _points.Length;

    public int SegmentCount => _points.Length - 1;

    /// <summary>Total distance along the path, in metres.</summary>
    public double Length => _arcLengths[^1];

    /// <summary>Metres along the path from the first point to point <paramref name="index"/>.</summary>
    public double ArcLengthAt(int index) => _arcLengths[index];

    /// <summary>
    /// The segment that holds the point <paramref name="arcLength"/> metres along the path, clamped
    /// to the first and last segment.
    /// </summary>
    public int SegmentIndexAt(double arcLength)
    {
        int found = Array.BinarySearch(_arcLengths, arcLength);
        int index = found >= 0 ? found : ~found - 1;
        return Math.Clamp(index, 0, SegmentCount - 1);
    }

    public Vec3 PointAtArcLength(double arcLength)
    {
        double clamped = Math.Clamp(arcLength, 0, Length);
        int segment = SegmentIndexAt(clamped);
        double segmentLength = _arcLengths[segment + 1] - _arcLengths[segment];
        double fraction = segmentLength > 0 ? (clamped - _arcLengths[segment]) / segmentLength : 0;
        return Vec3.Lerp(_points[segment], _points[segment + 1], fraction);
    }

    /// <summary>
    /// <paramref name="count"/> points spread evenly by distance from the first point to the last,
    /// so densely and sparsely recorded paths can be compared like for like.
    /// </summary>
    public PathSample[] Resample(int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 2);

        var samples = new PathSample[count];
        double step = Length / (count - 1);
        int segment = 0;
        for (int i = 0; i < count; i++)
        {
            double arcLength = i == count - 1 ? Length : i * step;
            while (segment < SegmentCount - 1 && _arcLengths[segment + 1] < arcLength)
            {
                segment++;
            }

            double segmentLength = _arcLengths[segment + 1] - _arcLengths[segment];
            double fraction = segmentLength > 0 ? (arcLength - _arcLengths[segment]) / segmentLength : 0;
            samples[i] = new PathSample(
                Vec3.Lerp(_points[segment], _points[segment + 1], Math.Clamp(fraction, 0, 1)),
                arcLength);
        }

        return samples;
    }

    /// <summary>Points no more than <paramref name="spacing"/> metres apart along the whole path.</summary>
    public PathSample[] SampleEvery(double spacing)
    {
        if (!(spacing > 0) || !double.IsFinite(spacing))
        {
            throw new ArgumentOutOfRangeException(nameof(spacing), spacing, "Spacing must be a positive number of metres.");
        }

        int count = (int)Math.Min(int.MaxValue - 1, Math.Ceiling(Length / spacing)) + 1;
        return Resample(Math.Max(count, 2));
    }

    /// <summary>The closest point to <paramref name="point"/> on the segment from a to b.</summary>
    /// <param name="point">The point being measured from.</param>
    /// <param name="a">The start of the segment.</param>
    /// <param name="b">The end of the segment.</param>
    /// <param name="fraction">0 at a, 1 at b.</param>
    public static Vec3 ClosestPointOnSegment(Vec3 point, Vec3 a, Vec3 b, out double fraction)
    {
        Vec3 along = b - a;
        double lengthSquared = along.LengthSquared;
        fraction = lengthSquared > 0 ? Math.Clamp((point - a).Dot(along) / lengthSquared, 0, 1) : 0;
        return a + (along * fraction);
    }
}
