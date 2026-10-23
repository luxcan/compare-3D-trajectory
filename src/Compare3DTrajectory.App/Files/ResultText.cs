using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;

namespace Compare3DTrajectory.App.Files;

/// <summary>The sentences a file row shows about its comparison.</summary>
public static class ResultText
{
    /// <summary>Below this a fitted turn is noise, not a heading the run was really off by.</summary>
    private const double NegligibleTurnDegrees = 0.05;

    public static string Verdict(ComparisonResult result, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Issues.Count == 0
            ? $"✓ Follows the reference within {Measure.Metres(tolerance)}"
            : $"✗ {Measure.Plural(result.Issues.Count, "issue")} · worst {Measure.Metres(result.Summary.MaxDeviation)} off";
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
    /// it from the comparison.
    /// </summary>
    public static string Alignment(HeadingFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        if (!fit.IsFitted)
        {
            return "Compared where the file puts it (fitting is off)";
        }

        double turn = fit.Transform.HeadingDegrees;
        string turned = Math.Abs(turn) < NegligibleTurnDegrees
            ? "no turn"
            : $"turned {Measure.Degrees(Math.Abs(turn))} {(turn < 0 ? "clockwise" : "anticlockwise")}";
        return $"Fitted: moved {Measure.Metres(fit.CentroidShift.Length)}, {turned} · fit RMS {Measure.Metres(fit.TrimmedRms)}";
    }
}
