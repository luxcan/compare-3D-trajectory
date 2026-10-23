namespace Compare3DTrajectory.Core.Loading;

/// <summary>The cells of a CSV file or one worksheet, as text, before anything is interpreted.</summary>
public sealed record RawTable(string Name, IReadOnlyList<TableRow> Rows);
