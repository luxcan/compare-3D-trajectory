using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core;

/// <summary>A run compared against a reference: whether it passed, and where it did not.</summary>
/// <param name="Reference">The reference as read, with any rows skipped and why.</param>
/// <param name="Run">The run as read, with any rows skipped and why.</param>
/// <param name="Tolerance">The tolerance it was judged by, in metres.</param>
/// <param name="Issues">Every stretch off the path or missed, with the lines it covers in its own file.</param>
/// <param name="Result">The comparison itself: each point's deviation, the fit, the figures.</param>
public sealed record ComparisonReport(
    LoadedTrajectory Reference,
    LoadedTrajectory Run,
    double Tolerance,
    IReadOnlyList<ReportedIssue> Issues,
    ComparisonResult Result)
{
    /// <summary>True when the run follows the reference within tolerance all the way: no issues at all.</summary>
    public bool Passed => Issues.Count == 0;

    public ComparisonSummary Summary => Result.Summary;

    /// <summary>How the run was moved and turned onto the reference; not fitted when fitting was off.</summary>
    public HeadingFit Fit => Result.Fit;

    internal static ComparisonReport From(LoadedTrajectory reference, LoadedTrajectory run, double tolerance, ComparisonResult result)
    {
        ReportedIssue[] issues =
        [
            .. result.Issues.Select(issue =>
            {
                LoadedTrajectory owner = issue.IsOnReference ? reference : run;
                return new ReportedIssue(
                    issue.Kind,
                    owner.SourcePath,
                    owner.LineNumbers[issue.FirstIndex],
                    owner.LineNumbers[issue.LastIndex],
                    issue.Length,
                    issue.WorstDeviation,
                    issue);
            }),
        ];
        return new ComparisonReport(reference, run, tolerance, issues, result);
    }
}

/// <summary>One stretch where the run and the reference disagree, placed by line in its own file.</summary>
/// <param name="Kind">What went wrong.</param>
/// <param name="SourcePath">The file the stretch lies in: the reference's for a miss, the run's otherwise.</param>
/// <param name="FirstLine">The stretch's first line (or worksheet row) in that file.</param>
/// <param name="LastLine">Its last line (or row).</param>
/// <param name="Length">Metres along that path.</param>
/// <param name="WorstDeviation">The farthest the stretch gets from the other path, in metres.</param>
/// <param name="Issue">The issue itself, with its points in the comparison's working metres.</param>
public sealed record ReportedIssue(
    IssueKind Kind,
    string SourcePath,
    int FirstLine,
    int LastLine,
    double Length,
    double WorstDeviation,
    TrajectoryIssue Issue);
