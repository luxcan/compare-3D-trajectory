using Compare3DTrajectory.App.Services;

namespace Compare3DTrajectory.App.Tests.Shell;

internal sealed class StubFileDialog(params string[] paths) : IFileDialogService
{
    public string? LastInitialFolder { get; private set; }

    public IReadOnlyList<string> PromptForTrajectoryFiles(string? initialFolder)
    {
        LastInitialFolder = initialFolder;
        return paths;
    }
}
