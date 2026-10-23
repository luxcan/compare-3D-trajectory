namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// How a run measured up overall.
/// </summary>
/// <param name="MaxDeviation">The farthest any run point is from the reference, in metres.</param>
/// <param name="MeanDeviation">The average distance of the run's points from the reference.</param>
/// <param name="RmsDeviation">The root-mean-square distance of the run's points from the reference.</param>
/// <param name="WithinTolerancePercent">The share of the run's points within tolerance of the reference.</param>
/// <param name="CoveragePercent">The share of the reference's length the run came within tolerance of.</param>
public sealed record ComparisonSummary(
    double MaxDeviation,
    double MeanDeviation,
    double RmsDeviation,
    double WithinTolerancePercent,
    double CoveragePercent);
