namespace Compare3DTrajectory.App.Services;

/// <summary>The file picker, behind an interface so the view model can be exercised without a desktop.</summary>
public interface IFileDialogService
{
    /// <summary>
    /// Prompts for any number of trajectory files, starting in <paramref name="initialFolder"/> when
    /// given. Returns an empty list if the user cancels.
    /// </summary>
    IReadOnlyList<string> PromptForTrajectoryFiles(string? initialFolder);
}
