using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.App.Issues;
using Compare3DTrajectory.App.Services;
using Compare3DTrajectory.App.Settings;
using Compare3DTrajectory.App.Viewer;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// The whole window: the files, which one is the reference, the comparison settings, the issues
/// found and what the 3D view draws.
/// </summary>
/// <remarks>
/// Comparing runs on the thread pool and is restarted, never queued, whenever something it depends
/// on changes: the reference, the tolerance, fitting, or the set of files. Showing, hiding,
/// selecting and recolouring only redraw.
/// </remarks>
public sealed partial class MainViewModel : ObservableObject
{
    public const string EmptyStatus = "Add CSV or Excel files of x, y, z: drop them anywhere on this window, or use Add files.";

    /// <summary>The smallest box an issue is framed in, so a one-point issue does not fill the screen.</summary>
    private const double MinimumFocusSize = 20;

    private readonly IFileDialogService _fileDialog;
    private readonly IConfirmationService _confirmation;
    private readonly ISettingsStore _settingsStore;
    private readonly PlotPalette _palette;
    private readonly ComparisonEngine _engine = new();
    private readonly WorkingFrame _frame = new();

    private CancellationTokenSource? _comparison;
    private int _nextColour;
    private string? _lastFolder;
    private bool _rebuildingIssues;

    public MainViewModel(
        IFileDialogService fileDialog,
        IConfirmationService confirmation,
        ISettingsStore settingsStore,
        PlotPalette palette)
    {
        _fileDialog = fileDialog;
        _confirmation = confirmation;
        _settingsStore = settingsStore;
        _palette = palette;

        AppSettings settings = settingsStore.Load();
        Tolerance = settings.Tolerance;
        _toleranceText = settings.Tolerance.ToString(CultureInfo.CurrentCulture);
        _fitHeading = settings.FitHeading;
        _selectedColourMode = ColourModeOption.For(settings.ColourMode);
        _lastFolder = settings.LastFolder;
        _statusText = EmptyStatus;
    }

    /// <summary>Asks the view to frame one issue.</summary>
    public event EventHandler<FocusRequestedEventArgs>? FocusRequested;

    /// <summary>Asks the view to frame everything drawn.</summary>
    public event EventHandler? FitViewRequested;

    public ObservableCollection<TrajectoryItemViewModel> Trajectories { get; } = [];

    public ObservableCollection<LoadFailureViewModel> Failures { get; } = [];

    public ObservableCollection<IssueRowViewModel> Issues { get; } = [];

    public IReadOnlyList<ColourModeOption> ColourModes { get; } = ColourModeOption.All;

    /// <summary>The tolerance in force: the last valid value typed.</summary>
    public double Tolerance { get; private set; }

    public string LegendWithinText => $"within {Measure.Metres(Tolerance)}";

    public string LegendNearText => $"{Measure.Metres(Tolerance)} to {Measure.Metres(2 * Tolerance)}";

    public string LegendBeyondText => $"beyond {Measure.Metres(2 * Tolerance)}";

    public string GridText => Scene.GridSpacing > 0 ? $"grid squares {Measure.Metres(Scene.GridSpacing)}" : string.Empty;

    public bool IsDeviationColouring => SelectedColourMode.Mode == ColourMode.Deviation;

    /// <summary>The comparison most recently started, so a caller can wait for the results.</summary>
    public Task CurrentComparison { get; private set; } = Task.CompletedTask;

    [ObservableProperty]
    private string _toleranceText;

    [ObservableProperty]
    private string? _toleranceError;

    [ObservableProperty]
    private bool _fitHeading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDeviationColouring))]
    private ColourModeOption _selectedColourMode;

    [ObservableProperty]
    private TrajectoryItemViewModel? _reference;

    [ObservableProperty]
    private TrajectoryItemViewModel? _selectedTrajectory;

    [ObservableProperty]
    private IssueRowViewModel? _selectedIssue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GridText))]
    private SceneModel _scene = SceneModel.Empty;

    /// <summary>What the empty issues table says, which depends on why it is empty.</summary>
    [ObservableProperty]
    private string _noIssuesText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusText;

    [ObservableProperty]
    private bool _hasTrajectories;

    /// <summary>
    /// Reads the files (and the supported files directly inside any folders) on the thread pool and
    /// adds every trajectory they hold. A file already in the list is not added twice. Files of
    /// numbers without a header naming x, y and z are refused out loud, in one dialog for them all.
    /// </summary>
    public async Task AddFilesAsync(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        List<string> files = ExpandFolders(paths);
        if (files.Count == 0)
        {
            return;
        }

        bool wasEmpty = Trajectories.Count == 0;
        IsBusy = true;
        StatusText = $"Reading {Measure.Plural(files.Count, "file")}…";
        FileLoadResult[] results;
        try
        {
            results = await Task.Run(() => files.AsParallel().AsOrdered().Select(TrajectoryFileLoader.Load).ToArray());
        }
        finally
        {
            IsBusy = false;
        }

        int duplicates = 0;
        foreach (FileLoadResult result in results)
        {
            foreach (LoadFailure failure in result.Failures)
            {
                Failures.Add(new LoadFailureViewModel(failure));
            }

            foreach (LoadedTrajectory loaded in result.Trajectories)
            {
                if (Trajectories.Any(item => item.SourcePath == loaded.SourcePath && item.Name == loaded.Name))
                {
                    duplicates++;
                    continue;
                }

                AddTrajectory(loaded);
            }
        }

        _lastFolder = Path.GetDirectoryName(files[^1]);
        SaveSettings();

        if (Reference is null && Trajectories.Count > 0)
        {
            Reference = Trajectories[0];
        }
        else
        {
            StartComparison();
        }

        List<LoadFailure> refused = [.. results.SelectMany(result => result.Failures).Where(failure => failure.MissingHeader is not null)];
        if (refused.Count > 0)
        {
            _confirmation.Inform(RefusalText.Title, RefusalText.Describe(refused));
        }

        await CurrentComparison;
        if (duplicates > 0)
        {
            StatusText += $" · {Measure.Plural(duplicates, "file")} already loaded";
        }

        if (wasEmpty && HasTrajectories)
        {
            FitViewRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    [RelayCommand]
    private async Task AddFiles()
    {
        IReadOnlyList<string> paths = _fileDialog.PromptForTrajectoryFiles(_lastFolder);
        if (paths.Count > 0)
        {
            await AddFilesAsync(paths);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        if (Trajectories.Count == 0 && Failures.Count == 0)
        {
            return;
        }

        if (Trajectories.Count > 0 && !_confirmation.Confirm(
                "Remove all files?",
                $"All {Measure.Plural(Trajectories.Count, "trajectory")} leave the comparison. The files themselves are not touched.",
                "Remove all"))
        {
            return;
        }

        _comparison?.Cancel();
        foreach (TrajectoryItemViewModel item in Trajectories)
        {
            item.PropertyChanged -= OnTrajectoryPropertyChanged;
        }

        Trajectories.Clear();
        Failures.Clear();
        _engine.Clear();
        _frame.Reset();
        HasTrajectories = false;
        Reference = null;
    }

    [RelayCommand]
    private void RemoveTrajectory(TrajectoryItemViewModel item)
    {
        item.PropertyChanged -= OnTrajectoryPropertyChanged;
        _engine.Forget(item.Id);
        Trajectories.Remove(item);
        HasTrajectories = Trajectories.Count > 0;
        if (!HasTrajectories)
        {
            _frame.Reset();
        }

        if (item == Reference)
        {
            Reference = Trajectories.FirstOrDefault();
        }
        else
        {
            RebuildIssuesAndScene();
            UpdateStatus();
        }
    }

    /// <summary>Reads the file's x, y, z the other way: geocentric becomes local, local becomes geocentric.</summary>
    [RelayCommand]
    private void ToggleCoordinateKind(TrajectoryItemViewModel item)
    {
        item.Kind = item.Kind == CoordinateKind.Geocentric ? CoordinateKind.Local : CoordinateKind.Geocentric;
        _engine.Forget(item.Id);
        item.Prepared = new PreparedTrajectory(item.Id, _frame.ToWorking(item.Loaded.Points, item.Kind));
        StartComparison();
    }

    [RelayCommand]
    private void DismissFailure(LoadFailureViewModel failure) => Failures.Remove(failure);

    [RelayCommand]
    private void FitView() => FitViewRequested?.Invoke(this, EventArgs.Empty);

    partial void OnToleranceTextChanged(string value)
    {
        if (!Measure.TryParseNumber(value, out double tolerance) || !(tolerance > 0) || !double.IsFinite(tolerance))
        {
            ToleranceError = "Enter a distance in metres greater than 0.";
            return;
        }

        ToleranceError = null;
        if (tolerance != Tolerance)
        {
            Tolerance = tolerance;
            OnPropertyChanged(nameof(LegendWithinText));
            OnPropertyChanged(nameof(LegendNearText));
            OnPropertyChanged(nameof(LegendBeyondText));
            SaveSettings();
            StartComparison();
        }
    }

    partial void OnFitHeadingChanged(bool value)
    {
        SaveSettings();
        StartComparison();
    }

    partial void OnSelectedColourModeChanged(ColourModeOption value)
    {
        SaveSettings();
        RebuildScene();
    }

    partial void OnReferenceChanged(TrajectoryItemViewModel? oldValue, TrajectoryItemViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.IsReference = false;
        }

        if (newValue is not null)
        {
            newValue.IsReference = true;
        }

        StartComparison();
    }

    partial void OnSelectedTrajectoryChanged(TrajectoryItemViewModel? value) => RebuildScene();

    partial void OnSelectedIssueChanged(IssueRowViewModel? value)
    {
        if (_rebuildingIssues)
        {
            return;
        }

        RebuildScene();
        if (value is not null)
        {
            Focus(value.Issue);
        }
    }

    private void AddTrajectory(LoadedTrajectory loaded)
    {
        var item = new TrajectoryItemViewModel(loaded, _palette.FileColour(_nextColour++));
        item.Prepared = new PreparedTrajectory(item.Id, _frame.ToWorking(loaded.Points, item.Kind));
        item.PropertyChanged += OnTrajectoryPropertyChanged;
        Trajectories.Add(item);
        HasTrajectories = true;
    }

    private void OnTrajectoryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not TrajectoryItemViewModel item)
        {
            return;
        }

        switch (e.PropertyName)
        {
            case nameof(TrajectoryItemViewModel.IsVisible):
                RebuildIssuesAndScene();
                UpdateStatus();
                break;
            case nameof(TrajectoryItemViewModel.IsReference) when item.IsReference && item != Reference:
                Reference = item;
                break;
        }
    }

    private void StartComparison() => CurrentComparison = CompareAsync();

    /// <summary>
    /// Compares every run against the reference, cancelling any comparison still running.
    /// </summary>
    /// <remarks>
    /// This is the top of a fire-and-forget operation, so any failure other than cancellation is
    /// caught here and shown, in the status bar and on the rows; left in the task it would be lost.
    /// </remarks>
    private async Task CompareAsync()
    {
        _comparison?.Cancel();
        TrajectoryItemViewModel? reference = Reference;
        if (reference?.Prepared is null)
        {
            foreach (TrajectoryItemViewModel item in Trajectories)
            {
                item.ShowNotCompared("Choose a reference to compare against");
            }

            RebuildIssuesAndScene();
            UpdateStatus();
            return;
        }

        using var cancellation = new CancellationTokenSource();
        _comparison = cancellation;
        CancellationToken token = cancellation.Token;

        PreparedTrajectory referencePath = reference.Prepared;
        reference.ShowAsReference();
        List<TrajectoryItemViewModel> runs = [.. Trajectories.Where(item => item != reference && item.Prepared is not null)];
        foreach (TrajectoryItemViewModel run in runs.Where(run => run.Result is null))
        {
            run.ShowNotCompared("Comparing…");
        }

        var settings = new ComparisonSettings(Tolerance);
        bool fitHeading = FitHeading;
        IsBusy = true;
        StatusText = $"Comparing {Measure.Plural(runs.Count, "run")} with {reference.Name}…";
        try
        {
            (ComparisonResult? Result, string? Failure)[] outcomes = await Task.WhenAll(runs.Select(run =>
            {
                PreparedTrajectory runPath = run.Prepared!;
                return Task.Run(() => CompareOne(referencePath, runPath, settings, fitHeading, token), token);
            }));
            token.ThrowIfCancellationRequested();

            for (int i = 0; i < runs.Count; i++)
            {
                if (outcomes[i].Result is { } result)
                {
                    runs[i].ShowResult(result, settings.Tolerance);
                }
                else
                {
                    runs[i].ShowFailure(outcomes[i].Failure!);
                }
            }

            RebuildIssuesAndScene();
            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
            // A newer comparison replaced this one and will show its own results.
        }
        catch (Exception exception)
        {
            foreach (TrajectoryItemViewModel run in runs)
            {
                run.ShowNotCompared("Comparison failed");
            }

            StatusText = $"Comparison failed: {exception.Message}";
        }
        finally
        {
            if (_comparison == cancellation)
            {
                _comparison = null;
                IsBusy = false;
            }
        }
    }

    /// <summary>
    /// One run against the reference. A run that cannot be compared is reported on its own row, so
    /// it never takes the other runs, or the 3D view, down with it.
    /// </summary>
    private (ComparisonResult? Result, string? Failure) CompareOne(
        PreparedTrajectory reference,
        PreparedTrajectory run,
        ComparisonSettings settings,
        bool fitHeading,
        CancellationToken token)
    {
        try
        {
            return (_engine.Compare(reference, run, settings, fitHeading, token), null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (null, exception.Message);
        }
    }

    private void RebuildIssuesAndScene()
    {
        _rebuildingIssues = true;
        try
        {
            SelectedIssue = null;
            Issues.Clear();
            if (Reference is { } reference)
            {
                int number = 1;
                foreach (TrajectoryItemViewModel run in Trajectories.Where(item => item != reference && item.IsVisible))
                {
                    foreach (TrajectoryIssue issue in run.Result?.Issues ?? [])
                    {
                        Issues.Add(new IssueRowViewModel(number++, run, reference, issue));
                    }
                }
            }
        }
        finally
        {
            _rebuildingIssues = false;
        }

        bool anyCompared = Trajectories.Any(item => item != Reference && item.IsVisible && item.Result is not null);
        NoIssuesText = Issues.Count > 0 ? string.Empty
            : anyCompared ? $"No issues: every run shown follows the reference within {Measure.Metres(Tolerance)}."
            : HasTrajectories ? "Add or show another file to compare it with the reference."
            : string.Empty;

        RebuildScene();
    }

    private void RebuildScene()
    {
        List<SceneTrajectory> trajectories =
        [
            .. Trajectories
                .Where(item => item.IsVisible && item.DrawnPoints.Count > 1)
                .Select(item => new SceneTrajectory(
                    item.DrawnPoints,
                    item == Reference ? null : item.Result?.Deviations,
                    item.Colour,
                    item == Reference,
                    item == SelectedTrajectory,
                    item.DescribePoint)),
        ];
        List<SceneIssue> issues = [.. Issues.Select(row => new SceneIssue(row.Number, row.Issue, row == SelectedIssue))];

        // Redrawing follows a click or a toggle, far from any handler that could show a failure, so
        // one is shown here rather than left to escape as a crash.
        try
        {
            Scene = SceneBuilder.Build(trajectories, issues, SelectedColourMode.Mode, Tolerance, _palette);
        }
        catch (Exception exception)
        {
            Scene = SceneModel.Empty;
            StatusText = $"The 3D view could not be drawn: {exception.Message}";
        }
    }

    private void Focus(TrajectoryIssue issue)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (Vec3 point in issue.Path.Append(issue.WorstPoint))
        {
            Vector3 position = Scene.ToRender(point);
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        Vector3 centre = (min + max) / 2;
        Vector3 half = Vector3.Max((max - min) / 2, new Vector3((float)(MinimumFocusSize / 2)));
        FocusRequested?.Invoke(this, new FocusRequestedEventArgs(centre - half, centre + half));
    }

    private void UpdateStatus()
    {
        if (!HasTrajectories)
        {
            StatusText = EmptyStatus;
            return;
        }

        if (Reference is null)
        {
            StatusText = "Choose a reference with the round button on its row.";
            return;
        }

        int runs = Trajectories.Count(item => item != Reference && item.Result is not null);
        StatusText = $"Reference {Reference.Name} · {Measure.Plural(runs, "run")} compared · "
            + $"{Measure.Plural(Issues.Count, "issue")} · tolerance {Measure.Metres(Tolerance)}";

        List<TrajectoryItemViewModel> failed = [.. Trajectories.Where(item => item.ComparisonFailure is not null)];
        if (failed.Count > 0)
        {
            string which = failed.Count == 1 ? failed[0].Name : Measure.Plural(failed.Count, "run");
            StatusText += $" · {which} could not be compared";
        }
    }

    private void SaveSettings()
    {
        string? problem = _settingsStore.Save(new AppSettings(Tolerance, FitHeading, SelectedColourMode.Mode, _lastFolder));
        if (problem is not null)
        {
            StatusText = $"Settings were not saved: {problem}";
        }
    }

    /// <summary>Files as given, plus the supported files directly inside any folders, in name order.</summary>
    private static List<string> ExpandFolders(IEnumerable<string> paths)
    {
        var files = new List<string>();
        foreach (string path in paths)
        {
            if (Directory.Exists(path))
            {
                files.AddRange(Directory.EnumerateFiles(path)
                    .Where(TrajectoryFileLoader.IsSupported)
                    .Order(StringComparer.OrdinalIgnoreCase));
            }
            else
            {
                files.Add(path);
            }
        }

        return files;
    }
}
