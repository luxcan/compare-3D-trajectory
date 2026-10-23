using System.Windows;
using Compare3DTrajectory.App.Services;

namespace Compare3DTrajectory.App.Shell;

/// <summary>Asks through <see cref="AppDialog"/>, over the main window.</summary>
public sealed class DialogConfirmationService : IConfirmationService
{
    public bool Confirm(string title, string message, string confirm) =>
        AppDialog.Confirm(Application.Current?.MainWindow, title, message, confirm);

    public void Inform(string title, string message) =>
        AppDialog.Inform(Application.Current?.MainWindow, title, message);
}
