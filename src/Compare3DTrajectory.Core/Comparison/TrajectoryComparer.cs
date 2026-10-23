using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// Measures an already-aligned run against the reference, in both directions.
/// </summary>
/// <remarks>
/// <para>Run to reference: how far each run point is from the reference path. Stretches beyond
/// tolerance are detours, or off-path when they include the run's first or last point.</para>
///
/// <para>Reference to run: the reference is sampled evenly along its length and each sample is measured
/// against the run. Stretches beyond tolerance are parts of the reference the run missed. Even
/// sampling makes the coverage figure a share of length rather than of however the file happened to
/// be recorded.</para>
/// </remarks>
public static class TrajectoryComparer
{
    /// <summary>The most samples taken along a reference, so a very long one at a very tight tolerance stays quick.</summary>
    private const int MaximumReferenceSamples = 200_000;

    private const int CancellationCheckInterval = 4096;

    public static ComparisonResult Compare(
        PreparedTrajectory reference,
        IReadOnlyList<Vec3> alignedRun,
        HeadingFit fit,
        ComparisonSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(alignedRun);
        ArgumentNullException.ThrowIfNull(fit);
        ArgumentNullException.ThrowIfNull(settings);

        double tolerance = settings.Tolerance;
        var run = new Polyline(alignedRun);

        double[] deviations = new double[alignedRun.Count];
        for (int i = 0; i < deviations.Length; i++)
        {
            if (i % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            deviations[i] = reference.Index.Nearest(alignedRun[i]).Distance;
        }

        var issues = new List<TrajectoryIssue>();
        foreach (Stretch stretch in Stretch.Find(deviations, run.ArcLengthAt, tolerance))
        {
            bool touchesEnd = stretch.First == 0 || stretch.Last == deviations.Length - 1;
            issues.Add(Describe(
                touchesEnd ? IssueKind.OffPath : IssueKind.Detour,
                stretch,
                index => alignedRun[index],
                index => deviations[index],
                run.ArcLengthAt,
                index => index));
        }

        double spacing = Math.Max(tolerance / 2, reference.Polyline.Length / MaximumReferenceSamples);
        PathSample[] samples = reference.Polyline.SampleEvery(spacing);
        var runIndex = new PolylineIndex(run);
        double[] coverage = new double[samples.Length];
        for (int i = 0; i < samples.Length; i++)
        {
            if (i % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            coverage[i] = runIndex.Nearest(samples[i].Position).Distance;
        }

        foreach (Stretch stretch in Stretch.Find(coverage, index => samples[index].ArcLength, tolerance))
        {
            issues.Add(Describe(
                IssueKind.Missed,
                stretch,
                index => samples[index].Position,
                index => coverage[index],
                index => samples[index].ArcLength,
                index => ReferenceVertexNear(reference.Polyline, samples[index].ArcLength, index == stretch.Last)));
        }

        return new ComparisonResult(fit, alignedRun, deviations, issues, Summarise(deviations, coverage, tolerance));
    }

    private static TrajectoryIssue Describe(
        IssueKind kind,
        Stretch stretch,
        Func<int, Vec3> positionAt,
        Func<int, double> deviationAt,
        Func<int, double> arcLengthAt,
        Func<int, int> pointIndexAt)
    {
        int worst = stretch.First;
        var path = new List<Vec3>(stretch.Last - stretch.First + 1);
        for (int i = stretch.First; i <= stretch.Last; i++)
        {
            path.Add(positionAt(i));
            if (deviationAt(i) > deviationAt(worst))
            {
                worst = i;
            }
        }

        return new TrajectoryIssue(
            kind,
            pointIndexAt(stretch.First),
            pointIndexAt(stretch.Last),
            arcLengthAt(stretch.Last) - arcLengthAt(stretch.First),
            deviationAt(worst),
            positionAt(worst),
            path);
    }

    /// <summary>
    /// The reference point at or before a distance along it, or at or after it for the end of a
    /// stretch, so the reported point range always covers the missed part.
    /// </summary>
    private static int ReferenceVertexNear(Polyline reference, double arcLength, bool isEnd)
    {
        int segment = reference.SegmentIndexAt(arcLength);
        return isEnd && arcLength > reference.ArcLengthAt(segment) ? segment + 1 : segment;
    }

    private static ComparisonSummary Summarise(double[] deviations, double[] coverage, double tolerance)
    {
        double sum = 0;
        double sumOfSquares = 0;
        double max = 0;
        int within = 0;
        foreach (double deviation in deviations)
        {
            sum += deviation;
            sumOfSquares += deviation * deviation;
            max = Math.Max(max, deviation);
            if (deviation <= tolerance)
            {
                within++;
            }
        }

        int covered = coverage.Count(distance => distance <= tolerance);
        return new ComparisonSummary(
            max,
            sum / deviations.Length,
            Math.Sqrt(sumOfSquares / deviations.Length),
            100.0 * within / deviations.Length,
            100.0 * covered / coverage.Length);
    }
}
