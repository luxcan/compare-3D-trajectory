namespace Compare3DTrajectory.Core.Geometry;

/// <summary>
/// Finds the exact closest point on a polyline, fast enough to ask once for every point of a long
/// recording.
/// </summary>
/// <remarks>
/// <para>Segments are cut into pieces no longer than the path's average segment, and a k-d tree is
/// built over the pieces' midpoints. No piece is more than <c>h</c> from its midpoint, so a piece
/// that lies within <c>d</c> of the query has its midpoint within <c>d + h</c>.</para>
///
/// <para>A query takes the piece with the nearest midpoint, measures the exact distance <c>d</c> to
/// it, and then measures every piece whose midpoint is within <c>d + h</c>. Any closer piece must be
/// among those, so the answer is exact rather than the nearest vertex.</para>
/// </remarks>
public sealed class PolylineIndex
{
    /// <summary>Reused per thread: the index is shared by comparisons running in parallel.</summary>
    [ThreadStatic]
    private static List<int>? CandidateBuffer;

    private readonly Polyline _polyline;
    private readonly Piece[] _pieces;
    private readonly double _halfPieceLength;
    private readonly KdTree _tree;

    public PolylineIndex(Polyline polyline)
    {
        ArgumentNullException.ThrowIfNull(polyline);
        _polyline = polyline;

        double target = polyline.Length / polyline.SegmentCount;
        var pieces = new List<Piece>(polyline.SegmentCount);
        double longestPiece = 0;
        for (int segment = 0; segment < polyline.SegmentCount; segment++)
        {
            Vec3 start = polyline.Points[segment];
            Vec3 end = polyline.Points[segment + 1];
            double segmentLength = polyline.ArcLengthAt(segment + 1) - polyline.ArcLengthAt(segment);
            int cuts = target > 0 ? Math.Max(1, (int)Math.Ceiling(segmentLength / target)) : 1;
            for (int cut = 0; cut < cuts; cut++)
            {
                double from = (double)cut / cuts;
                double to = (double)(cut + 1) / cuts;
                pieces.Add(new Piece(
                    Vec3.Lerp(start, end, from),
                    Vec3.Lerp(start, end, to),
                    segment,
                    polyline.ArcLengthAt(segment) + (segmentLength * from)));
            }

            longestPiece = Math.Max(longestPiece, segmentLength / cuts);
        }

        _pieces = [.. pieces];
        _halfPieceLength = longestPiece / 2;
        _tree = new KdTree([.. _pieces.Select(piece => Vec3.Lerp(piece.Start, piece.End, 0.5))]);
    }

    public Polyline Polyline => _polyline;

    public NearestPoint Nearest(Vec3 query)
    {
        int seed = _tree.Nearest(query, out _);
        NearestPoint best = Measure(seed, query);

        List<int> candidates = CandidateBuffer ??= [];
        candidates.Clear();
        _tree.WithinRadius(query, best.Distance + _halfPieceLength, candidates);
        foreach (int candidate in candidates)
        {
            NearestPoint measured = Measure(candidate, query);
            if (measured.Distance < best.Distance)
            {
                best = measured;
            }
        }

        return best;
    }

    private NearestPoint Measure(int pieceIndex, Vec3 query)
    {
        Piece piece = _pieces[pieceIndex];
        Vec3 point = Polyline.ClosestPointOnSegment(query, piece.Start, piece.End, out double fraction);
        double arcLength = piece.StartArcLength + (piece.Start.DistanceTo(piece.End) * fraction);
        return new NearestPoint(point, query.DistanceTo(point), piece.SegmentIndex, arcLength);
    }

    private readonly record struct Piece(Vec3 Start, Vec3 End, int SegmentIndex, double StartArcLength);
}
