using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// One stretch where a run and the reference disagree.
/// </summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="FirstIndex">
/// The first point of the stretch. For <see cref="IssueKind.Missed"/> this is a point of the
/// reference; otherwise it is a point of the run.
/// </param>
/// <param name="LastIndex">The last point of the stretch, on the same path as <paramref name="FirstIndex"/>.</param>
/// <param name="Length">Metres along that path.</param>
/// <param name="WorstDeviation">The farthest the stretch gets from the other path, in metres.</param>
/// <param name="WorstPoint">Where that happens, in working metres.</param>
/// <param name="Path">The stretch itself, in working metres, for drawing it and framing the view on it.</param>
public sealed record TrajectoryIssue(
    IssueKind Kind,
    int FirstIndex,
    int LastIndex,
    double Length,
    double WorstDeviation,
    Vec3 WorstPoint,
    IReadOnlyList<Vec3> Path)
{
    public bool IsOnReference => Kind == IssueKind.Missed;
}
