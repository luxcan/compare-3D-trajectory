using System.Windows;
using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// Code-behind is limited to drag-and-drop, the title bar and passing the view model's framing
/// requests to the 3D view, none of which has a clean binding equivalent. Everything else goes
/// through <see cref="MainViewModel"/>.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldViewModel)
        {
            oldViewModel.FocusRequested -= OnFocusRequested;
            oldViewModel.FitViewRequested -= OnFitViewRequested;
        }

        if (e.NewValue is MainViewModel newViewModel)
        {
            newViewModel.FocusRequested += OnFocusRequested;
            newViewModel.FitViewRequested += OnFitViewRequested;
        }
    }

    private void OnFocusRequested(object? sender, FocusRequestedEventArgs e) => Viewport.FocusOn(e.Min, e.Max);

    private void OnFitViewRequested(object? sender, EventArgs e) => Viewport.FitAll();

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = GetDroppedPaths(e).Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnFilesDropped(object sender, DragEventArgs e)
    {
        string[] paths = GetDroppedPaths(e);
        if (DataContext is not MainViewModel viewModel || paths.Length == 0)
        {
            return;
        }

        e.Handled = true;
        await viewModel.AddFilesAsync(paths);
    }

    private static string[] GetDroppedPaths(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths
            : [];
}
