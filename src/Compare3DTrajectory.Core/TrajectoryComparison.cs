using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core;

/// <summary>
/// The entry point for another .NET application: compares a run's file against a reference file,
/// exactly as the window does, and reports pass or fail with every issue as an object.
/// </summary>
/// <remarks>
/// Everything the window sets up is done here too, so a caller never repeats it: reading and
/// screening both files, laying geocentric files out in one East-North-Up frame, and fitting the run
/// to the reference. To show someone the same comparison, start Compare3DTrajectory.exe with the
/// reference path first and the run path after it.
/// </remarks>
public static class TrajectoryComparison
{
    /// <summary>Compares the run against the reference.</summary>
    /// <exception cref="TrajectoryFileException">
    /// Either file gave no trajectory, or more than one. A file of numbers without a header naming x,
    /// y and z is refused with the same message the window shows, never returned to be ignored.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance is not a positive number of metres.</exception>
    public static ComparisonReport Compare(
        string referencePath,
        string runPath,
        ComparisonOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(referencePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(runPath);
        options ??= new ComparisonOptions();
        var settings = new ComparisonSettings(options.Tolerance);
        cancellationToken.ThrowIfCancellationRequested();

        LoadedTrajectory reference = Load(referencePath);
        LoadedTrajectory run = Load(runPath);

        // The reference goes through the frame first, so the East-North-Up origin is on it.
        var frame = new WorkingFrame();
        var preparedReference = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(reference.Points, reference.DetectedKind));
        var preparedRun = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(run.Points, run.DetectedKind));
        ComparisonResult result = new ComparisonEngine().Compare(preparedReference, preparedRun, settings, options.FitHeading, cancellationToken);

        return ComparisonReport.From(reference, run, settings.Tolerance, result);
    }

    /// <summary>The one trajectory a file holds.</summary>
    /// <exception cref="TrajectoryFileException">The file gave no trajectory, or more than one.</exception>
    public static LoadedTrajectory Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        FileLoadResult result = TrajectoryFileLoader.Load(path);

        if (result.Failures.FirstOrDefault(failure => failure.MissingHeader is not null) is { } refused)
        {
            throw new TrajectoryFileException(path, refused.Reason, refused.MissingHeader);
        }

        return result.Trajectories.Count switch
        {
            1 => result.Trajectories[0],
            0 => throw new TrajectoryFileException(
                path,
                string.Join(Environment.NewLine, result.Failures.Select(failure => $"{failure.Name}: {failure.Reason}"))),
            _ => throw new TrajectoryFileException(
                path,
                $"{Path.GetFileName(path)} holds {result.Trajectories.Count} trajectories "
                + $"(sheets {string.Join(", ", result.Trajectories.Select(SheetName))}); compare a file that holds one."),
        };
    }

    private static string SheetName(LoadedTrajectory trajectory)
    {
        int separator = trajectory.Name.IndexOf(" › ", StringComparison.Ordinal);
        return separator < 0 ? trajectory.Name : trajectory.Name[(separator + 3)..];
    }
}
