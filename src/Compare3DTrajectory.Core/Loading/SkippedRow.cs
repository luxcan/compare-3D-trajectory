namespace Compare3DTrajectory.Core.Loading;

/// <summary>A row that could not be read as a point, and why.</summary>
public sealed record SkippedRow(int LineNumber, string Reason);
