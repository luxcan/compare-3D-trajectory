using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Alignment;

/// <summary>
/// Lines a run up with the reference by a turn about the vertical and a move, so that what is left
/// over is the difference in shape.
/// </summary>
/// <remarks>
/// <para>Iterative closest point is accurate but only finds the fit nearest to where it starts, so it
/// is started from many places and the best result is kept. The starting guesses are: pairing the two
/// paths point for point by distance travelled, both forwards and backwards; and laying the centres
/// together at every 15° of heading.</para>
///
/// <para>Each guess is refined a little on a coarse sample, and the three best are refined fully on a
/// finer one. Each round pairs every run point with its nearest point on the reference, discards the
/// worst-matched 20% and refits. Discarding them is what stops a detour, or a part of the reference the
/// run never visited, from dragging the whole fit sideways.</para>
/// </remarks>
public static class HeadingAligner
{
    private const int CoarseSampleCount = 250;
    private const int FineSampleCount = 1000;
    private const int CoarseIterations = 8;
    private const int FineIterations = 60;
    private const int FinalistCount = 3;
    private const double KeptFraction = 0.8;
    private const int HeadingSteps = 24;

    public static HeadingFit Fit(Polyline run, PolylineIndex reference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(reference);

        Vec3[] coarse = Positions(run.Resample(CoarseSampleCount));
        Vec3[] fine = Positions(run.Resample(FineSampleCount));
        Vec3[] referenceCoarse = Positions(reference.Polyline.Resample(CoarseSampleCount));

        List<Refined> finalists = StartingGuesses(coarse, referenceCoarse)
            .Select(guess => Refine(guess, coarse, reference, CoarseIterations, cancellationToken))
            .OrderBy(refined => refined.Rms)
            .Take(FinalistCount)
            .ToList();

        Refined best = finalists
            .Select(finalist => Refine(finalist.Transform, fine, reference, FineIterations, cancellationToken))
            .MinBy(refined => refined.Rms);

        Vec3 centre = Vec3.Centroid(run.Points);
        return new HeadingFit(best.Transform, best.Rms, best.Transform.Apply(centre) - centre);
    }

    private static IEnumerable<HeadingTransform> StartingGuesses(Vec3[] run, Vec3[] reference)
    {
        yield return HeadingTransform.FitPairs(run, reference);
        yield return HeadingTransform.FitPairs(run, [.. reference.Reverse()]);

        Vec3 runCentre = Vec3.Centroid(run);
        Vec3 referenceCentre = Vec3.Centroid(reference);
        for (int step = 0; step < HeadingSteps; step++)
        {
            double heading = 2 * Math.PI * step / HeadingSteps;
            Vec3 turnedCentre = new HeadingTransform(heading, Vec3.Zero).Apply(runCentre);
            yield return new HeadingTransform(heading, referenceCentre - turnedCentre);
        }
    }

    /// <summary>Trimmed iterative closest point from <paramref name="start"/>.</summary>
    private static Refined Refine(
        HeadingTransform start,
        Vec3[] samples,
        PolylineIndex reference,
        int iterations,
        CancellationToken cancellationToken)
    {
        HeadingTransform current = start;
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Match match = MatchBest(current, samples, reference);
            HeadingTransform next = HeadingTransform.FitPairs(match.From, match.To);
            bool settled = Math.Abs(next.HeadingRadians - current.HeadingRadians) < 1e-10
                && (next.Offset - current.Offset).Length < 1e-7;
            current = next;
            if (settled)
            {
                break;
            }
        }

        return new Refined(current, MatchBest(current, samples, reference).Rms);
    }

    /// <summary>Pairs each moved sample with its nearest reference point and keeps the best 80%.</summary>
    private static Match MatchBest(HeadingTransform transform, Vec3[] samples, PolylineIndex reference)
    {
        var nearest = new NearestPoint[samples.Length];
        int[] order = new int[samples.Length];
        double[] distances = new double[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            nearest[i] = reference.Nearest(transform.Apply(samples[i]));
            distances[i] = nearest[i].Distance;
            order[i] = i;
        }

        Array.Sort(distances, order);
        int kept = Math.Max(1, (int)Math.Ceiling(samples.Length * KeptFraction));
        var from = new Vec3[kept];
        var to = new Vec3[kept];
        double sumOfSquares = 0;
        for (int i = 0; i < kept; i++)
        {
            from[i] = samples[order[i]];
            to[i] = nearest[order[i]].Point;
            sumOfSquares += distances[i] * distances[i];
        }

        return new Match(from, to, Math.Sqrt(sumOfSquares / kept));
    }

    private static Vec3[] Positions(PathSample[] samples) => [.. samples.Select(sample => sample.Position)];

    private readonly record struct Refined(HeadingTransform Transform, double Rms);

    private sealed record Match(Vec3[] From, Vec3[] To, double Rms);
}
