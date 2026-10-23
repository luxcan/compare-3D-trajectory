using System.Collections.Concurrent;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// Compares runs against a reference, remembering each fit so that changing the tolerance or the
/// colouring does not refit anything.
/// </summary>
/// <remarks>
/// A fit depends only on the two paths, never on the tolerance, so it is cached per (reference, run)
/// pair: per pair of prepared paths, not of ids. A file whose points change, as on a switch between
/// local and geocentric, is prepared afresh, so a fit for its old points that finishes late can never
/// be taken for its new ones. Safe to call from several threads at once.
/// </remarks>
public sealed class ComparisonEngine
{
    private readonly ConcurrentDictionary<(PreparedTrajectory Reference, PreparedTrajectory Run), HeadingFit> _fits = new();

    public int CachedFitCount => _fits.Count;

    public ComparisonResult Compare(
        PreparedTrajectory reference,
        PreparedTrajectory run,
        ComparisonSettings settings,
        bool fitHeading,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(settings);

        HeadingFit fit = fitHeading
            ? _fits.GetOrAdd(
                (reference, run),
                _ => HeadingAligner.Fit(run.Level, reference.Index, run.AsRecorded, cancellationToken))
            : HeadingFit.None;

        // A fitted run is carried from its own level frame onto the reference; an unfitted one is
        // compared exactly where its file puts it.
        IReadOnlyList<Vec3> aligned = fit.IsFitted ? [.. run.Level.Points.Select(fit.Transform.Apply)] : run.Points;
        return TrajectoryComparer.Compare(reference, aligned, fit, settings, cancellationToken);
    }

    /// <summary>Drops every fit involving a trajectory whose points have changed or gone.</summary>
    public void Forget(Guid trajectoryId)
    {
        foreach ((PreparedTrajectory Reference, PreparedTrajectory Run) key in _fits.Keys)
        {
            if (key.Reference.Id == trajectoryId || key.Run.Id == trajectoryId)
            {
                _fits.TryRemove(key, out _);
            }
        }
    }

    public void Clear() => _fits.Clear();
}
