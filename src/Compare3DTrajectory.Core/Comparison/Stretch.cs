namespace Compare3DTrajectory.Core.Comparison;

/// <summary>A run of consecutive points beyond tolerance, by index.</summary>
internal readonly record struct Stretch(int First, int Last)
{
    /// <summary>How many points in a row must be back within tolerance, besides one tolerance of length, to end a stretch.</summary>
    private const int ReturnPoints = 3;

    /// <summary>
    /// The runs of points farther than <paramref name="tolerance"/> from the other path, each from its
    /// first point beyond tolerance to its last.
    /// </summary>
    /// <remarks>
    /// <para>A stretch starts at the first point beyond tolerance. It ends once a point comes back inside
    /// 80% of tolerance, or once the path has stayed within tolerance for a few points and one
    /// tolerance of length. Without the margin, a path running along the tolerance line with a little
    /// noise would split into dozens of one-point problems; without the second way out, a path that
    /// strayed briefly and then went on steadily just inside tolerance would never end its stretch.</para>
    ///
    /// <para>For the same reason, two stretches with less than one tolerance of good path between them
    /// are reported as one.</para>
    /// </remarks>
    public static List<Stretch> Find(IReadOnlyList<double> distances, Func<int, double> arcLengthAt, double tolerance)
    {
        double leaveBelow = tolerance * 0.8;
        var found = new List<Stretch>();
        int start = -1;
        int lastBeyond = -1;
        int backSince = -1;
        for (int i = 0; i < distances.Count; i++)
        {
            if (distances[i] > tolerance)
            {
                start = start < 0 ? i : start;
                lastBeyond = i;
                backSince = -1;
                continue;
            }

            if (start < 0)
            {
                continue;
            }

            backSince = backSince < 0 ? i : backSince;
            bool clearlyBack = distances[i] < leaveBelow;
            bool backForLong = i - backSince + 1 >= ReturnPoints && arcLengthAt(i) - arcLengthAt(lastBeyond) >= tolerance;
            if (clearlyBack || backForLong)
            {
                found.Add(new Stretch(start, lastBeyond));
                start = -1;
                backSince = -1;
            }
        }

        if (start >= 0)
        {
            found.Add(new Stretch(start, lastBeyond));
        }

        var merged = new List<Stretch>(found.Count);
        foreach (Stretch stretch in found)
        {
            if (merged.Count > 0 && arcLengthAt(stretch.First) - arcLengthAt(merged[^1].Last) < tolerance)
            {
                merged[^1] = merged[^1] with { Last = stretch.Last };
            }
            else
            {
                merged.Add(stretch);
            }
        }

        return merged;
    }
}
