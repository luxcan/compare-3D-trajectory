namespace Compare3DTrajectory.Core.Loading;

/// <summary>A file, or one sheet of it, that gave no trajectory, and why.</summary>
public sealed record LoadFailure(string Name, string SourcePath, string Reason);
