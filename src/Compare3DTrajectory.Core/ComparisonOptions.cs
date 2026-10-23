namespace Compare3DTrajectory.Core;

/// <summary>How <see cref="TrajectoryComparison.Compare"/> judges a run; the window's own defaults unless set.</summary>
public sealed record ComparisonOptions
{
    /// <summary>How far, in metres, a point may be from the other path and still be on it.</summary>
    public double Tolerance { get; init; } = 2;

    /// <summary>
    /// Whether the run is first moved and turned about the vertical to fit the reference best, so
    /// only differences of shape are reported. Off compares positions exactly as recorded.
    /// </summary>
    public bool FitHeading { get; init; } = true;
}
