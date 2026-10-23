using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Alignment;

/// <summary>
/// How a run was lined up with the reference, and how well it fitted.
/// </summary>
/// <param name="Transform">Carries the run onto the reference.</param>
/// <param name="TrimmedRms">Root-mean-square distance of the best-fitting 80% of the run, in metres.</param>
/// <param name="CentroidShift">How far the run's centre moved, which is the intuitive "it was this far off".</param>
public sealed record HeadingFit(HeadingTransform Transform, double TrimmedRms, Vec3 CentroidShift)
{
    /// <summary>No fitting: the run is compared where the file put it.</summary>
    public static HeadingFit None { get; } = new(HeadingTransform.Identity, double.NaN, Vec3.Zero);

    public bool IsFitted => !double.IsNaN(TrimmedRms);
}
