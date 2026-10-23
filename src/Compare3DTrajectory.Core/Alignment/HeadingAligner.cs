using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Alignment;

/// <summary>
/// Lines a run up with the reference by a turn about the vertical and a move, so that what is left
/// over is the difference in shape.
/// </summary>
/// <remarks>
/// <para>Iterative closest point is accurate but only finds the fit nearest to where it starts, so it
/// is started from many places and the best result is kept. The starting guesses are: the run exactly
/// as recorded; pairing the two paths point for point by distance travelled, both forwards and
/// backwards; and laying the centres together at every 15° of heading. A run that covers only part of
/// the reference, and was moved as well, fits none of those, its centre not being the reference's; only
/// then are guesses tried that set its centre down at points spread along the reference, at every 15°
/// too. Each guess is refined a little on a coarse sample, and the best few fully on a finer one.</para>
///
/// <para>Each round pairs every run point with its nearest point on the reference and refits on the
/// pairs within a few times the median distance. A share fixed in advance would let a detour, an
/// overrun or a pre-roll longer than that share drag the whole fit sideways; the median adapts to
/// however much of the run really follows the reference, up to half of it.</para>
///
/// <para>Fits are scored on the closest half of the pairs, so a run that follows only part of the
/// reference, or strays for part of its length, still scores best where it really is. Fits that score
/// alike, such as a straight leg either way round, are decided by how little they move and turn the
/// run from where it was recorded.</para>
/// </remarks>
public static class HeadingAligner
{
    private const int ScreenSampleCount = 50;
    private const int CoarseSampleCount = 250;
    private const int FineSampleCount = 1000;
    private const int CoarseIterations = 8;
    private const int FineIterations = 60;
    private const int HeadingSteps = 24;
    private const int AnchorCount = 12;

    /// <summary>A round that turns and moves the run by less than this has settled: far below anything shown.</summary>
    private const double SettledRadians = 1e-8;

    private const double SettledMetres = 1e-5;

    /// <summary>
    /// A score, in square metres, that only a fit in the right place reaches: below it the guesses
    /// for a whole run have done, and those for part of one are not needed.
    /// </summary>
    private const double GoodScore = 1;

    /// <summary>How many of the guesses for part of a run go on from being scored as they stand to the coarse refinement.</summary>
    private const int ScreenedCount = 20;

    /// <summary>How many distinct fits go on to the fine refinement, besides any that tie with the best.</summary>
    private const int FinalistCount = 3;

    private const int MostFinalists = 4;

    /// <summary>Pairs within this many median distances are kept for the next round.</summary>
    private const double InlierMedians = 3;

    /// <summary>A floor for the inlier distance, so a noise-free run does not discard its own points.</summary>
    private const double SmallestInlierDistance = 0.05;

    private const double LeastKeptFraction = 0.3;

    /// <summary>The share of pairs <see cref="HeadingFit.TrimmedRms"/> is reported over.</summary>
    private const double ReportedFraction = 0.8;

    /// <summary>A path all within this many metres of its centre never moves, and has no shape to fit.</summary>
    private const double SmallestExtent = 1;

    /// <summary>Scores within this ratio of the best, and this many square metres, are a tie.</summary>
    private const double TieRatio = 1.05;

    private const double TieSlack = 0.01;

    /// <summary>
    /// Two coarse fits closer than this in heading and position are the same fit, reached from
    /// different guesses; refining both fully would only find it twice.
    /// </summary>
    private const double SameHeadingDegrees = 5;

    private const double SameOffsetMetres = 10;

    public static HeadingFit Fit(
        Polyline run,
        PolylineIndex reference,
        HeadingTransform asRecorded = default,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(reference);

        if (Extent(run.Points) < SmallestExtent)
        {
            return HeadingFit.NotFitted("the run never moves, so there is no shape to fit") with { AsRecorded = asRecorded };
        }

        if (Extent(reference.Polyline.Points) < SmallestExtent)
        {
            return HeadingFit.NotFitted("the reference never moves, so there is no shape to fit to") with { AsRecorded = asRecorded };
        }

        Vec3[] coarse = Positions(run.Resample(CoarseSampleCount));
        Vec3[] fine = Positions(run.Resample(FineSampleCount));
        Vec3[] referenceCoarse = Positions(reference.Polyline.Resample(CoarseSampleCount));
        Vec3 centre = Vec3.Centroid(run.Points);
        double size = RadiusOfGyration(run.Points, centre);

        List<(HeadingTransform Transform, double Score)> coarseFits =
            [.. WholeRunGuesses(coarse, referenceCoarse, asRecorded).Select(guess => RefineCoarsely(guess, coarse, reference, cancellationToken))];

        if (coarseFits.Min(fit => fit.Score) > GoodScore)
        {
            Vec3[] screen = Positions(run.Resample(ScreenSampleCount));
            coarseFits.AddRange(PartRunGuesses(coarse, referenceCoarse)
                .Select(guess => (Guess: guess, Score: Score(Distances(guess, screen, reference))))
                .OrderBy(scored => scored.Score)
                .Take(ScreenedCount)
                .Select(scored => RefineCoarsely(scored.Guess, coarse, reference, cancellationToken)));
        }

        List<(HeadingTransform Transform, double Score)> fineFits =
        [
            .. Finalists(coarseFits)
                .Select(finalist => Refine(finalist, fine, reference, FineIterations, cancellationToken))
                .Select(fit => (fit, Score(Distances(fit, fine, reference)))),
        ];

        HeadingTransform best = LeastMoving(Ties(fineFits), asRecorded, centre, size);
        double trimmedRms = TrimmedRms(Distances(best, fine, reference), ReportedFraction);
        return new HeadingFit(best, trimmedRms, best.Apply(centre) - asRecorded.Apply(centre)) { AsRecorded = asRecorded };
    }

    private static IEnumerable<HeadingTransform> WholeRunGuesses(Vec3[] run, Vec3[] reference, HeadingTransform asRecorded)
    {
        yield return asRecorded;
        yield return HeadingTransform.FitPairs(run, reference);
        yield return HeadingTransform.FitPairs(run, [.. reference.Reverse()]);

        Vec3 runCentre = Vec3.Centroid(run);
        Vec3 referenceCentre = Vec3.Centroid(reference);
        foreach (double heading in Headings())
        {
            Vec3 turnedCentre = new HeadingTransform(heading, Vec3.Zero).Apply(runCentre);
            yield return new HeadingTransform(heading, referenceCentre - turnedCentre);
        }
    }

    /// <summary>The run's centre set down at points spread along the reference, at every heading.</summary>
    private static IEnumerable<HeadingTransform> PartRunGuesses(Vec3[] run, Vec3[] reference)
    {
        Vec3 runCentre = Vec3.Centroid(run);
        foreach (double heading in Headings())
        {
            Vec3 turnedCentre = new HeadingTransform(heading, Vec3.Zero).Apply(runCentre);
            for (int anchor = 0; anchor < AnchorCount; anchor++)
            {
                yield return new HeadingTransform(heading, reference[anchor * (reference.Length - 1) / (AnchorCount - 1)] - turnedCentre);
            }
        }
    }

    private static IEnumerable<double> Headings() => Enumerable.Range(0, HeadingSteps).Select(step => 2 * Math.PI * step / HeadingSteps);

    private static (HeadingTransform Transform, double Score) RefineCoarsely(
        HeadingTransform guess,
        Vec3[] samples,
        PolylineIndex reference,
        CancellationToken cancellationToken)
    {
        HeadingTransform fit = Refine(guess, samples, reference, CoarseIterations, cancellationToken);
        return (fit, Score(Distances(fit, samples, reference)));
    }

    /// <summary>The best distinct fits, and every other distinct fit that ties with the best.</summary>
    private static List<HeadingTransform> Finalists(List<(HeadingTransform Transform, double Score)> fits)
    {
        var chosen = new List<HeadingTransform>();
        double best = fits.Min(fit => fit.Score);
        foreach ((HeadingTransform transform, double score) in fits.OrderBy(fit => fit.Score))
        {
            bool tie = score <= (best * TieRatio) + TieSlack;
            if (chosen.Count >= MostFinalists || (chosen.Count >= FinalistCount && !tie))
            {
                break;
            }

            if (!chosen.Any(other => IsSameFit(other, transform)))
            {
                chosen.Add(transform);
            }
        }

        return chosen;
    }

    private static IEnumerable<HeadingTransform> Ties(List<(HeadingTransform Transform, double Score)> fits)
    {
        double best = fits.Min(fit => fit.Score);
        return fits.Where(fit => fit.Score <= (best * TieRatio) + TieSlack).Select(fit => fit.Transform);
    }

    /// <summary>
    /// Of fits that score alike, the one that moves and turns the run least from where it was
    /// recorded: a turn counts as far as it carries a point at the run's typical distance from its
    /// centre.
    /// </summary>
    private static HeadingTransform LeastMoving(IEnumerable<HeadingTransform> fits, HeadingTransform asRecorded, Vec3 centre, double size) =>
        fits.MinBy(fit =>
        {
            double turn = Math.Abs(TurnBetween(fit, asRecorded)) * Math.PI / 180;
            return fit.Apply(centre).DistanceTo(asRecorded.Apply(centre)) + (size * turn);
        });

    private static bool IsSameFit(HeadingTransform left, HeadingTransform right) =>
        Math.Abs(TurnBetween(left, right)) < SameHeadingDegrees && (left.Offset - right.Offset).Length < SameOffsetMetres;

    private static double TurnBetween(HeadingTransform left, HeadingTransform right) =>
        new HeadingTransform(left.HeadingRadians - right.HeadingRadians, Vec3.Zero).HeadingDegrees;

    /// <summary>Iterative closest point from <paramref name="start"/>, each round on the pairs within a few medians.</summary>
    private static HeadingTransform Refine(
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

            (Vec3[] from, Vec3[] to) = Inliers(current, samples, reference);
            HeadingTransform next = HeadingTransform.FitPairs(from, to);
            bool settled = Math.Abs(next.HeadingRadians - current.HeadingRadians) < SettledRadians
                && (next.Offset - current.Offset).Length < SettledMetres;
            current = next;
            if (settled)
            {
                break;
            }
        }

        return current;
    }

    /// <summary>
    /// Pairs each moved sample with its nearest reference point, and keeps the pairs within
    /// <see cref="InlierMedians"/> median distances, but never fewer than the closest
    /// <see cref="LeastKeptFraction"/> of them.
    /// </summary>
    private static (Vec3[] From, Vec3[] To) Inliers(HeadingTransform transform, Vec3[] samples, PolylineIndex reference)
    {
        var nearest = new NearestPoint[samples.Length];
        double[] distances = new double[samples.Length];
        int[] order = new int[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            nearest[i] = reference.Nearest(transform.Apply(samples[i]));
            distances[i] = nearest[i].Distance;
            order[i] = i;
        }

        Array.Sort(distances, order);
        double limit = Math.Max(InlierMedians * distances[distances.Length / 2], SmallestInlierDistance);
        int kept = Math.Max((int)Math.Ceiling(samples.Length * LeastKeptFraction), Array.FindLastIndex(distances, distance => distance <= limit) + 1);
        kept = Math.Clamp(kept, 1, samples.Length);

        var from = new Vec3[kept];
        var to = new Vec3[kept];
        for (int i = 0; i < kept; i++)
        {
            from[i] = samples[order[i]];
            to[i] = nearest[order[i]].Point;
        }

        return (from, to);
    }

    /// <summary>Each moved sample's distance from the reference, closest first.</summary>
    private static double[] Distances(HeadingTransform transform, Vec3[] samples, PolylineIndex reference)
    {
        double[] distances = [.. samples.Select(sample => reference.Nearest(transform.Apply(sample)).Distance)];
        Array.Sort(distances);
        return distances;
    }

    /// <summary>The mean square distance of the closest half, which a stretch of the run off the reference cannot spoil.</summary>
    private static double Score(double[] sortedDistances)
    {
        int half = Math.Max(1, sortedDistances.Length / 2);
        double sum = 0;
        for (int i = 0; i < half; i++)
        {
            sum += sortedDistances[i] * sortedDistances[i];
        }

        return sum / half;
    }

    private static double TrimmedRms(double[] sortedDistances, double fraction)
    {
        int kept = Math.Max(1, (int)Math.Ceiling(sortedDistances.Length * fraction));
        double sumOfSquares = 0;
        for (int i = 0; i < kept; i++)
        {
            sumOfSquares += sortedDistances[i] * sortedDistances[i];
        }

        return Math.Sqrt(sumOfSquares / kept);
    }

    private static double Extent(IReadOnlyList<Vec3> points)
    {
        Vec3 centre = Vec3.Centroid(points);
        return points.Max(point => point.DistanceTo(centre));
    }

    private static double RadiusOfGyration(IReadOnlyList<Vec3> points, Vec3 centre) =>
        Math.Sqrt(points.Average(point => point.DistanceSquaredTo(centre)));

    private static Vec3[] Positions(PathSample[] samples) => [.. samples.Select(sample => sample.Position)];
}
