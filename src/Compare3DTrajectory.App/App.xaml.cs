using System.Windows;
using System.Windows.Threading;
using Compare3DTrajectory.App.Services;
using Compare3DTrajectory.App.Settings;
using Compare3DTrajectory.App.Shell;
using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App;

/// <summary>
/// Composition root. The object graph is small enough that wiring it by hand stays clearer than a
/// container.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var viewModel = new MainViewModel(
            new FileDialogService(),
            new DialogConfirmationService(),
            JsonSettingsStore.ForCurrentUser(),
            PlotPalette.FromResources(Resources));

        MainWindow = new MainWindow { DataContext = viewModel };

        // Files given on the command line: dragging files onto the executable, or "Open with". Read
        // once the window is up, so the view can frame them; an event handler, so a failure reaches
        // the handler below instead of vanishing with an unobserved task.
        if (e.Args.Length > 0)
        {
            string[] files = e.Args;
            MainWindow.Loaded += async (_, _) => await viewModel.AddFilesAsync(files);
        }

        MainWindow.Show();

        // Once the window is up and idle, the published exe clears what older builds unpacked. Begun
        // with BeginInvoke, so a failure other than a folder in use reaches the handler below.
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(UnpackedLibraries.ClearOtherBuilds));
    }

    /// <summary>
    /// Last line of defence: report rather than vanish, so failures stay diagnosable. A failure
    /// before the window is up, during startup, leaves nothing to carry on with, so the app then
    /// closes instead of running on with no window.
    /// </summary>
    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string log = WriteCrashLog(e.Exception);

        // A Windows message box on purpose: whatever failed may have been the app's own drawing.
        MessageBox.Show(
            $"Something went wrong:{Environment.NewLine}{Environment.NewLine}{e.Exception.Message}{Environment.NewLine}{Environment.NewLine}Details: {log}",
            "Compare 3D Trajectory - Unexpected error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
        if (Current?.MainWindow is not { IsVisible: true })
        {
            Current?.Shutdown(1);
        }
    }

    /// <summary>
    /// The dialog shows only the message; the full stack goes to a known file. A failure to write the
    /// log must never replace the error the user is already being told about.
    /// </summary>
    private static string WriteCrashLog(Exception exception)
    {
        string path = Path.Combine(Path.GetTempPath(), "compare3dtrajectory-crash.txt");
        try
        {
            File.WriteAllText(path, exception.ToString());
            return path;
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
            return $"could not be written to {path} ({logFailure.Message})";
        }
    }
}
