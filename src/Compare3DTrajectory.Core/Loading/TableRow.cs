namespace Compare3DTrajectory.Core.Loading;

/// <summary>One non-empty row of a file, with the line (CSV) or row (Excel) number it came from.</summary>
public sealed record TableRow(int LineNumber, IReadOnlyList<string> Cells);
