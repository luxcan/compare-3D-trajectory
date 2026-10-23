using Compare3DTrajectory.Core.Loading;
using Microsoft.Win32;

namespace Compare3DTrajectory.App.Services;

/// <inheritdoc />
public sealed class FileDialogService : IFileDialogService
{
    public IReadOnlyList<string> PromptForTrajectoryFiles(string? initialFolder)
    {
        string patterns = string.Join(";", TrajectoryFileLoader.SupportedExtensions.Select(extension => "*" + extension));
        var dialog = new OpenFileDialog
        {
            Title = "Add trajectory files",
            Filter = $"Trajectories ({patterns})|{patterns}|All files (*.*)|*.*",
            Multiselect = true,
            CheckFileExists = true,
            InitialDirectory = initialFolder ?? string.Empty,
        };

        return dialog.ShowDialog() == true ? dialog.FileNames : [];
    }
}
