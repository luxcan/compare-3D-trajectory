using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// The points read from one table, and everything the user should know about how they were read.
/// </summary>
/// <param name="Points">The x, y, z of every readable row, in file order.</param>
/// <param name="LineNumbers">The line or row number each point came from, so a point can be found in the file.</param>
/// <param name="SkippedRows">Rows that were not points, or not plausible positions, with the reason for each.</param>
/// <param name="ColumnDescription">Which columns were taken as x, y and z.</param>
/// <param name="Note">Anything else the reader had to decide, such as ignoring a title above the header.</param>
/// <param name="FailureReason">Why the table gave no usable points; null when it did.</param>
public sealed record TableParseResult(
    IReadOnlyList<Vec3> Points,
    IReadOnlyList<int> LineNumbers,
    IReadOnlyList<SkippedRow> SkippedRows,
    string ColumnDescription,
    string? Note,
    string? FailureReason)
{
    /// <summary>Set when the table holds numbers but no header naming x, y and z, so it was refused.</summary>
    public MissingHeader? MissingHeader { get; init; }

    /// <summary>Whether the points look geocentric or local, judged by most of them.</summary>
    public CoordinateKind DetectedKind { get; init; }
}
