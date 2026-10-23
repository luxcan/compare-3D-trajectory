using System.Globalization;
using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.Core.Comparison;

namespace Compare3DTrajectory.App.Issues;

/// <summary>One row of the issues table: where a run and the reference disagree.</summary>
public sealed class IssueRowViewModel
{
    public IssueRowViewModel(int number, TrajectoryItemViewModel run, TrajectoryItemViewModel reference, TrajectoryIssue issue)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(issue);
        Number = number;
        Run = run;
        Issue = issue;

        // Off the path from the first point to the last is the plainest sign of a file read the wrong
        // way, or a run in the wrong place, so it is said as such rather than as a bad start.
        bool allTheWay = issue.FirstIndex == 0 && issue.LastIndex == run.Loaded.Points.Count - 1;
        KindText = issue.Kind switch
        {
            IssueKind.Detour => "Detour",
            IssueKind.OffPath when allTheWay => "Off path all the way",
            IssueKind.OffPath => issue.FirstIndex == 0 ? "Off path at start" : "Off path at end",
            _ => "Missed",
        };

        // Line numbers rather than point indices: they are what finds the place in the file.
        TrajectoryItemViewModel owner = issue.IsOnReference ? reference : run;
        int firstLine = owner.Loaded.LineNumbers[issue.FirstIndex];
        int lastLine = owner.Loaded.LineNumbers[issue.LastIndex];
        string where = issue.IsOnReference ? "reference" : "run";
        WhereText = firstLine == lastLine
            ? string.Create(CultureInfo.CurrentCulture, $"{where} line {firstLine}")
            : string.Create(CultureInfo.CurrentCulture, $"{where} lines {firstLine}–{lastLine}");
    }

    public int Number { get; }

    public TrajectoryItemViewModel Run { get; }

    public TrajectoryIssue Issue { get; }

    public string FileName => Run.Name;

    public string KindText { get; }

    public string WhereText { get; }

    public string LengthText => Measure.Metres(Issue.Length);

    public string WorstText => Measure.Metres(Issue.WorstDeviation);
}
