using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>A trajectory read from a CSV file or one worksheet, exactly as the file gave it.</summary>
/// <param name="Name">The file name, with the sheet name added when a workbook holds several.</param>
/// <param name="SourcePath">The full path of the file.</param>
/// <param name="Points">x, y, z in the file's own coordinates.</param>
/// <param name="LineNumbers">The line or row each point came from.</param>
/// <param name="SkippedRows">Rows that could not be read, with the reason for each.</param>
/// <param name="ColumnDescription">Which columns were read as x, y and z.</param>
/// <param name="Note">Anything else the reader had to decide.</param>
/// <param name="DetectedKind">Whether the points look geocentric or local.</param>
public sealed record LoadedTrajectory(
    string Name,
    string SourcePath,
    IReadOnlyList<Vec3> Points,
    IReadOnlyList<int> LineNumbers,
    IReadOnlyList<SkippedRow> SkippedRows,
    string ColumnDescription,
    string? Note,
    CoordinateKind DetectedKind);
