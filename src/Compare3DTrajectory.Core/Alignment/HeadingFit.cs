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

    /// <summary>Why fitting was on but the run could not be fitted; null otherwise.</summary>
    public string? NotFittedReason { get; init; }

    /// <summary>
    /// Where <see cref="Transform"/> would put the run had it not been moved or turned at all, so the
    /// turn reported is the run's own and not the small difference between two frames' north.
    /// </summary>
    public HeadingTransform AsRecorded { get; init; }

    /// <summary>How far the fit turned the run, in degrees between -180 and 180, anticlockwise seen from above.</summary>
    public double TurnDegrees => new HeadingTransform(Transform.HeadingRadians - AsRecorded.HeadingRadians, Vec3.Zero).HeadingDegrees;

    /// <summary>A run that fitting was asked for but could not be given, compared where the file put it.</summary>
    public static HeadingFit NotFitted(string reason) => None with { NotFittedReason = reason };
}
