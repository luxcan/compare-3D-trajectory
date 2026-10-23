using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// One run compared against the reference.
/// </summary>
/// <param name="Fit">How the run was lined up; <see cref="HeadingFit.None"/> when fitting is off.</param>
/// <param name="AlignedPoints">The run's points after the fit, in working metres, one per file point.</param>
/// <param name="Deviations">Each aligned point's distance from the reference path, in metres.</param>
/// <param name="Issues">Every stretch that is off the path or missed, in order along each path.</param>
/// <param name="Summary">The overall figures.</param>
public sealed record ComparisonResult(
    HeadingFit Fit,
    IReadOnlyList<Vec3> AlignedPoints,
    IReadOnlyList<double> Deviations,
    IReadOnlyList<TrajectoryIssue> Issues,
    ComparisonSummary Summary);
