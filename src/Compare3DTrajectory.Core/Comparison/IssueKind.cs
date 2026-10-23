namespace Compare3DTrajectory.Core.Comparison;

/// <summary>The ways a run can differ from the reference.</summary>
public enum IssueKind
{
    /// <summary>The run left the reference path and came back to it.</summary>
    Detour,

    /// <summary>The run started or finished away from the reference path.</summary>
    OffPath,

    /// <summary>A part of the reference path the run never came near.</summary>
    Missed,
}
