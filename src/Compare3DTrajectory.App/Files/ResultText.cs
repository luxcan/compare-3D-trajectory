using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Files;

/// <summary>The sentences a file row shows about its comparison.</summary>
public static class ResultText
{
    /// <summary>Below this a fitted turn is noise, not a heading the run was really off by.</summary>
    private const double NegligibleTurnDegrees = 0.05;

    /// <summary>Below this a move up or down, or across, is too small to be worth naming apart.</summary>
    private const double NegligibleMoveMetres = 0.5;

    /// <summary>
    /// Pass or fail. The worst counts the missed stretches as well as the run's own points, so a run
    /// that stopped half way does not read as barely off.
    /// </summary>
    public static string Verdict(ComparisonResult result, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Issues.Count == 0)
        {
            return $"✓ Follows the reference within {Measure.Metres(tolerance)}";
        }

        double worst = Math.Max(result.Summary.MaxDeviation, result.Issues.Max(issue => issue.WorstDeviation));
        return $"✗ {Measure.Plural(result.Issues.Count, "issue")} · worst {Measure.Metres(worst)} off";
    }

    public static string Figures(ComparisonSummary summary)
    {
        ArgumentNullException.ThrowIfNull(summary);
        return $"{Measure.Percent(summary.WithinTolerancePercent)} of points on the path · "
            + $"{Measure.Percent(summary.CoveragePercent)} of the reference covered · "
            + $"RMS {Measure.Metres(summary.RmsDeviation)}";
    }

    /// <summary>
    /// What the fit did to the run, so a large real offset is still seen even though the fit removed
    /// it from the comparison. A move straight up or down, as from a different height datum or height
    /// in feet, is named as such rather than read as a move sideways.
    /// </summary>
    public static string Alignment(HeadingFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        if (!fit.IsFitted)
        {
            return fit.NotFittedReason is { } reason
                ? $"Compared where the file puts it: {reason}"
                : "Compared where the file puts it (fitting is off)";
        }

        double turn = fit.TurnDegrees;
        string turned = Math.Abs(turn) < NegligibleTurnDegrees
            ? "no turn"
            : $"turned {Measure.Degrees(Math.Abs(turn))} {(turn < 0 ? "clockwise" : "anticlockwise")}";
        return $"Fitted: {Moved(fit.CentroidShift)}, {turned} · fit RMS {Measure.Metres(fit.TrimmedRms)}";
    }

    private static string Moved(Vec3 shift)
    {
        double across = Math.Sqrt((shift.X * shift.X) + (shift.Y * shift.Y));
        double vertical = Math.Abs(shift.Z);
        string upOrDown = shift.Z > 0 ? "up" : "down";
        return vertical < NegligibleMoveMetres ? $"moved {Measure.Metres(across)}"
            : across < NegligibleMoveMetres ? $"moved {Measure.Metres(vertical)} {upOrDown}"
            : $"moved {Measure.Metres(across)} across and {Measure.Metres(vertical)} {upOrDown}";
    }
}
