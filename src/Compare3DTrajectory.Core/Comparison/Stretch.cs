namespace Compare3DTrajectory.Core.Comparison;

/// <summary>A run of consecutive points beyond tolerance, by index.</summary>
internal readonly record struct Stretch(int First, int Last)
{
    /// <summary>
    /// The runs of points farther than <paramref name="tolerance"/> from the other path.
    /// </summary>
    /// <remarks>
    /// <para>A stretch starts at the first point beyond tolerance, but only ends once a point comes
    /// back inside 80% of it. Without that margin, a path running along the tolerance line with a
    /// little noise would split into dozens of one-point problems.</para>
    ///
    /// <para>For the same reason, two stretches with less than one tolerance of good path between them
    /// are reported as one.</para>
    /// </remarks>
    public static List<Stretch> Find(IReadOnlyList<double> distances, Func<int, double> arcLengthAt, double tolerance)
    {
        double leaveBelow = tolerance * 0.8;
        var found = new List<Stretch>();
        int start = -1;
        for (int i = 0; i < distances.Count; i++)
        {
            if (start < 0)
            {
                if (distances[i] > tolerance)
                {
                    start = i;
                }
            }
            else if (distances[i] < leaveBelow)
            {
                found.Add(new Stretch(start, i - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            found.Add(new Stretch(start, distances.Count - 1));
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
