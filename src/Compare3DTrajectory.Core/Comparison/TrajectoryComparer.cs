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
/// <para>Reference to run: the reference is sampled evenly along its length, and at every one of its
/// own points, and each sample is measured against the run. Stretches beyond tolerance are parts of
/// the reference the run missed. Even sampling makes the coverage figure a share of length rather than
/// of however the file happened to be recorded; the reference's own points make sure a corner is
/// measured at the corner, which even samples would step over.</para>
///
/// <para>Every issue's length is measured along the reference, which is the path the user knows, and
/// which the noise of a run's recording cannot lengthen.</para>
/// </remarks>
public static class TrajectoryComparer
{
    /// <summary>The most samples taken along a reference, so a very long one at a very tight tolerance stays quick.</summary>
    private const int MaximumReferenceSamples = 200_000;

    private const int CancellationCheckInterval = 512;

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
        double[] alongReference = new double[alignedRun.Count];
        for (int i = 0; i < deviations.Length; i++)
        {
            if (i % CancellationCheckInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            NearestPoint nearest = reference.Index.Nearest(alignedRun[i]);
            deviations[i] = nearest.Distance;
            alongReference[i] = nearest.ArcLength;
        }

        var issues = new List<TrajectoryIssue>();
        foreach (Stretch stretch in Stretch.Find(deviations, run.ArcLengthAt, tolerance))
        {
            bool touchesEnd = stretch.First == 0 || stretch.Last == deviations.Length - 1;

            // From where the run left the path to where it came back, both read off the reference.
            double from = alongReference[Math.Max(0, stretch.First - 1)];
            double to = alongReference[Math.Min(deviations.Length - 1, stretch.Last + 1)];
            issues.Add(Describe(
                touchesEnd ? IssueKind.OffPath : IssueKind.Detour,
                stretch,
                index => alignedRun[index],
                index => deviations[index],
                Math.Abs(to - from),
                stretch.First,
                stretch.Last));
        }

        double spacing = Math.Max(tolerance / 2, reference.Polyline.Length / MaximumReferenceSamples);
        PathSample[] samples = CoverageSamples(reference.Polyline, spacing);
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
            // Half-way to the neighbouring samples at each end, so a miss of one sample is as long as
            // the stretch of reference that sample stands for.
            double from = stretch.First > 0 ? (samples[stretch.First - 1].ArcLength + samples[stretch.First].ArcLength) / 2 : 0;
            double to = stretch.Last < samples.Length - 1 ? (samples[stretch.Last].ArcLength + samples[stretch.Last + 1].ArcLength) / 2 : reference.Polyline.Length;
            issues.Add(Describe(
                IssueKind.Missed,
                stretch,
                index => samples[index].Position,
                index => coverage[index],
                to - from,
                ReferencePointAtOrBefore(reference.Polyline, samples[stretch.First].ArcLength),
                ReferencePointAtOrAfter(reference.Polyline, samples[stretch.Last].ArcLength)));
        }

        return new ComparisonResult(fit, alignedRun, deviations, issues, Summarise(deviations, coverage, tolerance));
    }

    /// <summary>Evenly along the reference, and at each of its own points, in order along it.</summary>
    private static PathSample[] CoverageSamples(Polyline reference, double spacing)
    {
        PathSample[] even = reference.SampleEvery(spacing);
        var samples = new List<PathSample>(even.Length + reference.Count);
        int vertex = 0;
        foreach (PathSample sample in even)
        {
            for (; vertex < reference.Count && reference.ArcLengthAt(vertex) <= sample.ArcLength; vertex++)
            {
                if (reference.ArcLengthAt(vertex) < sample.ArcLength)
                {
                    samples.Add(new PathSample(reference.Points[vertex], reference.ArcLengthAt(vertex)));
                }
            }

            samples.Add(sample);
        }

        return [.. samples];
    }

    private static TrajectoryIssue Describe(
        IssueKind kind,
        Stretch stretch,
        Func<int, Vec3> positionAt,
        Func<int, double> deviationAt,
        double length,
        int firstIndex,
        int lastIndex)
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

        return new TrajectoryIssue(kind, firstIndex, lastIndex, length, deviationAt(worst), positionAt(worst), path);
    }

    /// <summary>
    /// The reference points that bracket a missed stretch: the one at or before its start and the one
    /// at or after its end, so the point range always covers the missed part, even a miss of one
    /// sample just past a corner.
    /// </summary>
    private static int ReferencePointAtOrBefore(Polyline reference, double arcLength) => reference.SegmentIndexAt(arcLength);

    private static int ReferencePointAtOrAfter(Polyline reference, double arcLength)
    {
        int segment = reference.SegmentIndexAt(arcLength);
        return arcLength > reference.ArcLengthAt(segment) ? segment + 1 : segment;
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
