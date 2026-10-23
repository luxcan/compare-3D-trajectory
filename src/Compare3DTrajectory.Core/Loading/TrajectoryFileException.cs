namespace Compare3DTrajectory.Core.Loading;

/// <summary>A file that gave no trajectory to compare, or more than one; the message says why.</summary>
public sealed class TrajectoryFileException : Exception
{
    public TrajectoryFileException()
    {
        SourcePath = string.Empty;
    }

    public TrajectoryFileException(string message)
        : base(message)
    {
        SourcePath = string.Empty;
    }

    public TrajectoryFileException(string message, Exception innerException)
        : base(message, innerException)
    {
        SourcePath = string.Empty;
    }

    public TrajectoryFileException(string sourcePath, string message, MissingHeader? missingHeader = null)
        : base(message)
    {
        SourcePath = sourcePath;
        MissingHeader = missingHeader;
    }

    /// <summary>The file, as it was given.</summary>
    public string SourcePath { get; }

    /// <summary>Set when the file holds numbers but no header naming x, y and z.</summary>
    public MissingHeader? MissingHeader { get; }
}
