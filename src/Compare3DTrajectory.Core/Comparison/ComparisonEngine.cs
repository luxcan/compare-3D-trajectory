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
/// pair. Safe to call from several threads at once.
/// </remarks>
public sealed class ComparisonEngine
{
    private readonly ConcurrentDictionary<(Guid Reference, Guid Run), HeadingFit> _fits = new();

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
                (reference.Id, run.Id),
                _ => HeadingAligner.Fit(run.Polyline, reference.Index, cancellationToken))
            : HeadingFit.None;

        Vec3[] aligned = [.. run.Points.Select(fit.Transform.Apply)];
        return TrajectoryComparer.Compare(reference, aligned, fit, settings, cancellationToken);
    }

    /// <summary>Drops every fit involving a trajectory whose points have changed or gone.</summary>
    public void Forget(Guid trajectoryId)
    {
        foreach ((Guid Reference, Guid Run) key in _fits.Keys)
        {
            if (key.Reference == trajectoryId || key.Run == trajectoryId)
            {
                _fits.TryRemove(key, out _);
            }
        }
    }

    public void Clear() => _fits.Clear();
}
