using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.App.Settings;
using Compare3DTrajectory.App.Shell;
using Compare3DTrajectory.App.Viewer;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;
using Compare3DTrajectory.Core.Tests.Loading;

namespace Compare3DTrajectory.App.Tests.Shell;

/// <summary>
/// The window's behaviour end to end on the committed samples: real loading, real comparison, stub
/// dialogs. The samples are a model and four runs with known faults (see Core.Tests SampleScenarios).
/// </summary>
public sealed class MainViewModelTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Compare3DTrajectory.AppTests", Guid.NewGuid().ToString("N"));

    public MainViewModelTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task AddFilesAsync_SamplesFolder_ComparesEveryRunWithTheFirstFile()
    {
        var confirmation = new StubConfirmation(answer: true);
        MainViewModel viewModel = Create(confirmation: confirmation);

        await viewModel.AddFilesAsync([RepositoryFiles.Samples]);

        confirmation.Told.Should().BeEmpty();
        viewModel.Failures.Should().BeEmpty();

        viewModel.Trajectories.Select(item => item.Name).Should().Equal(
            "model.csv", "run-cut-corner.tsv", "run-detour.csv", "run-good.csv", "run-moved-and-turned.xlsx");
        viewModel.Reference!.Name.Should().Be("model.csv");
        viewModel.Issues.Select(row => (row.FileName, row.KindText)).Should().Equal(
            ("run-cut-corner.tsv", "Detour"),
            ("run-cut-corner.tsv", "Missed"),
            ("run-detour.csv", "Detour"),
            ("run-detour.csv", "Missed"));
        Item(viewModel, "run-good.csv").VerdictText.Should().Be("✓ Follows the reference within 2.00 m");
        Item(viewModel, "run-moved-and-turned.xlsx").AlignmentText.Should().Contain("turned 35.0° clockwise");
        viewModel.Scene.IsEmpty.Should().BeFalse();
        viewModel.IsBusy.Should().BeFalse();
    }

    /// <summary>
    /// Another application shows its comparison by starting the window with the reference path
    /// first, so the first path given is the reference, whatever the names sort to.
    /// </summary>
    [Fact]
    public async Task AddFilesAsync_PathsGivenAsByAnotherApplication_TakesTheFirstAsTheReference()
    {
        MainViewModel viewModel = Create();

        await viewModel.AddFilesAsync([Path.Combine(RepositoryFiles.Samples, "run-good.csv"), Path.Combine(RepositoryFiles.Samples, "model.csv")]);

        viewModel.Reference!.Name.Should().Be("run-good.csv");
        Item(viewModel, "model.csv").VerdictText.Should().Be("✓ Follows the reference within 2.00 m");
    }

    [Fact]
    public async Task ToleranceText_RaisedPastTheDetour_ClearsItsIssuesWithoutRefitting()
    {
        MainViewModel viewModel = await LoadedSamples();
        HeadingFit fitBefore = Item(viewModel, "run-detour.csv").Result!.Fit;

        viewModel.ToleranceText = "30";
        await viewModel.CurrentComparison;

        Item(viewModel, "run-detour.csv").Result!.Issues.Should().BeEmpty();
        Item(viewModel, "run-detour.csv").Result!.Fit.Should().BeSameAs(fitBefore);
        Item(viewModel, "run-cut-corner.tsv").HasIssues.Should().BeTrue();
        viewModel.LegendWithinText.Should().Be("within 30.0 m");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task ToleranceText_NotAPositiveNumber_ShowsAnErrorAndKeepsTheTolerance(string text)
    {
        MainViewModel viewModel = await LoadedSamples();

        viewModel.ToleranceText = text;

        viewModel.ToleranceError.Should().NotBeNull();
        viewModel.Tolerance.Should().Be(2);
    }

    [Fact]
    public async Task FitHeading_TurnedOff_ComparesTheMovedRunWhereItIs()
    {
        MainViewModel viewModel = await LoadedSamples();

        viewModel.FitHeading = false;
        await viewModel.CurrentComparison;

        TrajectoryItemViewModel moved = Item(viewModel, "run-moved-and-turned.xlsx");
        moved.HasIssues.Should().BeTrue();
        moved.AlignmentText.Should().Be("Compared where the file puts it (fitting is off)");
    }

    [Fact]
    public async Task Reference_ChangedByItsRadioButton_ComparesTheOldReferenceAsARun()
    {
        MainViewModel viewModel = await LoadedSamples();
        TrajectoryItemViewModel good = Item(viewModel, "run-good.csv");

        good.IsReference = true;
        await viewModel.CurrentComparison;

        viewModel.Reference.Should().BeSameAs(good);
        Item(viewModel, "model.csv").IsReference.Should().BeFalse();
        Item(viewModel, "model.csv").Result.Should().NotBeNull();
        good.VerdictText.Should().Be("Reference");
        viewModel.Issues.Should().OnlyContain(row => row.FileName != "run-good.csv");
    }

    /// <summary>F54: one run that could not be compared used to fail every run and leave the view empty.</summary>
    [Fact]
    public async Task CompareAsync_OneRunFails_TheOthersAreStillComparedAndDrawn()
    {
        MainViewModel viewModel = await LoadedSamples();
        TrajectoryItemViewModel broken = Item(viewModel, "run-detour.csv");
        broken.Prepared = new PreparedTrajectory(broken.Id, [new Vec3(0, 0, 0), new Vec3(double.NaN, 0, 0), new Vec3(1, 1, 1)]);

        viewModel.ToleranceText = "3";
        await viewModel.CurrentComparison;

        broken.VerdictText.Should().StartWith("Comparison failed: ");
        Item(viewModel, "run-good.csv").VerdictText.Should().Be("✓ Follows the reference within 3.00 m");
        viewModel.Issues.Should().Contain(row => row.FileName == "run-cut-corner.tsv");
        viewModel.Scene.IsEmpty.Should().BeFalse();
        viewModel.StatusText.Should().StartWith("Reference model.csv · 3 runs compared").And.EndWith(" · run-detour.csv could not be compared");
    }

    [Fact]
    public async Task IsVisible_RunHidden_DropsItsIssuesAndItsLine()
    {
        MainViewModel viewModel = await LoadedSamples();
        int linesBefore = viewModel.Scene.Lines.Count(line => line.Describe is not null);

        Item(viewModel, "run-detour.csv").IsVisible = false;

        viewModel.Issues.Should().OnlyContain(row => row.FileName != "run-detour.csv");
        viewModel.Scene.Lines.Count(line => line.Describe is not null).Should().Be(linesBefore - 1);
    }

    [Fact]
    public async Task SelectedIssue_Chosen_AsksTheViewToFrameIt()
    {
        MainViewModel viewModel = await LoadedSamples();
        FocusRequestedEventArgs? request = null;
        viewModel.FocusRequested += (_, e) => request = e;

        viewModel.SelectedIssue = viewModel.Issues[2];

        request.Should().NotBeNull();
        (request!.Max - request.Min).Length().Should().BeGreaterThan(20);
        viewModel.Scene.Lines.Should().Contain(line => line.Thickness == SceneBuilder.HighlightThickness);
    }

    [Fact]
    public async Task AddFilesAsync_UnreadableFile_ListsTheFailureWithItsReason()
    {
        var confirmation = new StubConfirmation(answer: true);
        MainViewModel viewModel = Create(confirmation: confirmation);
        string bad = Path.Combine(_folder, "notes.csv");
        await File.WriteAllTextAsync(bad, "just,two\nwords,here\n");

        await viewModel.AddFilesAsync([bad]);

        viewModel.Trajectories.Should().BeEmpty();
        LoadFailureViewModel failure = viewModel.Failures.Should().ContainSingle().Subject;
        failure.Name.Should().Be("notes.csv");
        failure.Reason.Should().StartWith("Could not find three numeric columns");
        confirmation.Told.Should().BeEmpty("only files of numbers without a header are refused out loud");
    }

    [Fact]
    public async Task AddFilesAsync_FilesWithoutAHeader_AreListedInOneDialogAndKeptAsFailures()
    {
        var confirmation = new StubConfirmation(answer: true);
        MainViewModel viewModel = Create(confirmation: confirmation);
        string headerless = Path.Combine(_folder, "headerless.csv");
        string enu = Path.Combine(_folder, "east-north-up.csv");
        await File.WriteAllTextAsync(headerless, "1,2,3\n4,5,6\n");
        await File.WriteAllTextAsync(enu, "Time,East,North,Up\n0,1,2,3\n1,4,5,6\n");

        await viewModel.AddFilesAsync([headerless, enu, Path.Combine(RepositoryFiles.Samples, "model.csv")]);

        viewModel.Trajectories.Select(item => item.Name).Should().Equal("model.csv");
        viewModel.Failures.Select(failure => failure.Name).Should().Equal("headerless.csv", "east-north-up.csv");
        (string title, string message) = confirmation.Told.Should().ContainSingle().Subject;
        title.Should().Be("Please add in the header for x, y, z");
        message.Should().Be(string.Join(
            Environment.NewLine,
            "2 files were not loaded:",
            string.Empty,
            "headerless.csv",
            "Columns found: none; line 1 already holds numbers (1, 2, 3)",
            string.Empty,
            "east-north-up.csv",
            "Columns found: 'Time', 'East', 'North', 'Up'",
            "No column is named x, y or z.",
            string.Empty,
            "Example first line: x,y,z"));
    }

    [Fact]
    public async Task AddFilesAsync_SameFileTwice_AddsItOnce()
    {
        MainViewModel viewModel = await LoadedSamples();

        await viewModel.AddFilesAsync([Path.Combine(RepositoryFiles.Samples, "model.csv")]);

        viewModel.Trajectories.Should().HaveCount(5);
        viewModel.StatusText.Should().EndWith("1 file already loaded");
    }

    [Fact]
    public async Task RemoveTrajectory_TheReference_MakesTheNextFileTheReference()
    {
        MainViewModel viewModel = await LoadedSamples();

        viewModel.RemoveTrajectoryCommand.Execute(viewModel.Reference);
        await viewModel.CurrentComparison;

        viewModel.Reference!.Name.Should().Be("run-cut-corner.tsv");
        viewModel.Trajectories.Should().HaveCount(4);
    }

    [Fact]
    public async Task Clear_Declined_KeepsEverything()
    {
        var confirmation = new StubConfirmation(answer: false);
        MainViewModel viewModel = await LoadedSamples(confirmation);

        viewModel.ClearCommand.Execute(null);

        confirmation.Asked.Should().Be(1);
        viewModel.Trajectories.Should().HaveCount(5);
    }

    [Fact]
    public async Task Clear_Confirmed_EmptiesTheWindow()
    {
        MainViewModel viewModel = await LoadedSamples(new StubConfirmation(answer: true));

        viewModel.ClearCommand.Execute(null);
        await viewModel.CurrentComparison;

        viewModel.Trajectories.Should().BeEmpty();
        viewModel.Issues.Should().BeEmpty();
        viewModel.Reference.Should().BeNull();
        viewModel.Scene.Should().BeSameAs(SceneModel.Empty);
        viewModel.StatusText.Should().Be(MainViewModel.EmptyStatus);
    }

    [Fact]
    public async Task ToggleCoordinateKind_GeocentricFileReadAsLocal_NoLongerMatches()
    {
        MainViewModel viewModel = await LoadedSamples();
        TrajectoryItemViewModel good = Item(viewModel, "run-good.csv");

        viewModel.ToggleCoordinateKindCommand.Execute(good);
        await viewModel.CurrentComparison;

        good.Kind.Should().Be(Core.Geo.CoordinateKind.Local);
        good.HasIssues.Should().BeTrue();
        good.DetailText.Should().Contain("local x, y, z");
    }

    [Fact]
    public async Task Settings_ChangedInTheWindow_AreSaved()
    {
        var store = new MemorySettingsStore(AppSettings.Default);
        MainViewModel viewModel = Create(store: store);
        await viewModel.AddFilesAsync([RepositoryFiles.Samples]);

        viewModel.ToleranceText = "5";
        viewModel.FitHeading = false;
        viewModel.SelectedColourMode = ColourModeOption.For(ColourMode.File);
        await viewModel.CurrentComparison;

        store.Saved.Should().Be(new AppSettings(5, false, ColourMode.File, RepositoryFiles.Samples));
    }

    [Fact]
    public async Task AddFilesCommand_UsesTheLastFolderForTheDialog()
    {
        var dialog = new StubFileDialog(Path.Combine(RepositoryFiles.Samples, "model.csv"));
        MainViewModel viewModel = Create(dialog, store: new MemorySettingsStore(AppSettings.Default with { LastFolder = @"C:\Somewhere" }));

        await viewModel.AddFilesCommand.ExecuteAsync(null);

        dialog.LastInitialFolder.Should().Be(@"C:\Somewhere");
        viewModel.Trajectories.Should().ContainSingle();
    }

    /// <summary>F32: while a new reference was being compared against, a redraw paired the runs' old issues with it.</summary>
    [Fact]
    public async Task Reference_ChangedAndARunHiddenBeforeTheComparisonEnds_NeverPairsOldIssuesWithIt()
    {
        MainViewModel viewModel = await LoadedSamples();
        string shortPath = Path.Combine(_folder, "short.csv");
        await File.WriteAllLinesAsync(shortPath, File.ReadLines(Path.Combine(RepositoryFiles.Samples, "model.csv")).Take(4));
        await viewModel.AddFilesAsync([shortPath]);

        Item(viewModel, "short.csv").IsReference = true;
        Item(viewModel, "run-detour.csv").IsVisible = false;

        viewModel.Issues.Should().BeEmpty("no run has been compared with short.csv yet");
        await viewModel.CurrentComparison;
        viewModel.Issues.Should().NotBeEmpty();
    }

    /// <summary>F33: one file whose reading threw lost every file in the drop.</summary>
    [Fact]
    public async Task AddFilesAsync_OneFileThrowsWhileBeingRead_TheOthersStillLoad()
    {
        string boom = Path.Combine(_folder, "boom.csv");
        await File.WriteAllTextAsync(boom, "x,y,z\n");
        MainViewModel viewModel = Create(load: path => path == boom ? throw new InvalidOperationException("The reader failed.") : TrajectoryFileLoader.Load(path));

        await viewModel.AddFilesAsync([boom, Path.Combine(RepositoryFiles.Samples, "model.csv"), Path.Combine(RepositoryFiles.Samples, "run-good.csv")]);

        viewModel.Trajectories.Should().HaveCount(2);
        LoadFailureViewModel failure = viewModel.Failures.Should().ContainSingle().Subject;
        failure.Name.Should().Be("boom.csv");
        failure.Reason.Should().Be("The reader failed.");
        viewModel.StatusText.Should().StartWith("Reference model.csv");
        viewModel.IsBusy.Should().BeFalse();
    }

    /// <summary>F34: a hand-edited or corrupted settings file with ColourMode 7 stopped the app starting.</summary>
    [Fact]
    public void Constructor_SettingsWithAnUndefinedColourMode_StartsWithTheDefaultColouring()
    {
        MainViewModel viewModel = Create(store: new MemorySettingsStore(AppSettings.Default with { ColourMode = (ColourMode)7 }));

        viewModel.SelectedColourMode.Mode.Should().Be(ColourMode.Deviation);
    }

    /// <summary>F36: "Settings were not saved" was replaced at once by the comparison's own status.</summary>
    [Fact]
    public async Task SettingsThatCannotBeSaved_StaySaidInTheStatusBar()
    {
        const string Problem = "Settings were not saved: The file is in use by another process.";
        MainViewModel viewModel = Create(store: new MemorySettingsStore(AppSettings.Default, "The file is in use by another process."));
        await viewModel.AddFilesAsync([RepositoryFiles.Samples]);
        viewModel.StatusText.Should().EndWith(Problem);

        viewModel.ToleranceText = "3";
        await viewModel.CurrentComparison;
        viewModel.StatusText.Should().EndWith(Problem);

        viewModel.FitHeading = false;
        await viewModel.CurrentComparison;
        viewModel.StatusText.Should().EndWith(Problem);
    }

    /// <summary>
    /// F37: a comparison replaced while a drop waited for it ended the wait early, so the view was
    /// framed on an empty scene and the "already loaded" note was overwritten.
    /// </summary>
    [Fact]
    public void AddFilesAsync_ComparisonReplacedWhileItWaits_FramesTheViewAndKeepsTheNote() => SingleThreadedContext.Run(async () =>
    {
        MainViewModel viewModel = Create();
        bool replaced = false;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.StatusText) && !replaced && viewModel.StatusText.StartsWith("Comparing", StringComparison.Ordinal))
            {
                replaced = true;
                SynchronizationContext.Current!.Post(_ => viewModel.ToleranceText = "3", null);
            }
        };
        SceneModel? framed = null;
        viewModel.FitViewRequested += (_, _) => framed = viewModel.Scene;

        await viewModel.AddFilesAsync([RepositoryFiles.Samples, Path.Combine(RepositoryFiles.Samples, "model.csv")]);

        replaced.Should().BeTrue();
        framed.Should().NotBeNull();
        framed!.IsEmpty.Should().BeFalse();
        viewModel.StatusText.Should().Contain("tolerance 3.00 m").And.EndWith("1 file already loaded");
    });

    /// <summary>F38: the same file by another spelling of its path was loaded again.</summary>
    [Fact]
    public async Task AddFilesAsync_TheSameFileByOtherSpellingsOfItsPath_AddsItOnce()
    {
        MainViewModel viewModel = Create();
        string model = Path.Combine(RepositoryFiles.Samples, "model.csv");

        await viewModel.AddFilesAsync([model, model.ToUpperInvariant(), Path.Combine(RepositoryFiles.Samples, ".", "model.csv")]);

        viewModel.Trajectories.Should().ContainSingle();
        viewModel.StatusText.Should().EndWith("· 1 file already loaded", "both other spellings name the one file");
    }

    /// <summary>F39: a workbook added again counted each of its sheets as a file already loaded.</summary>
    [Fact]
    public async Task AddFilesAsync_WorkbookOfSeveralSheetsAddedAgain_CountsOneFile()
    {
        MainViewModel viewModel = Create();
        string book = Path.Combine(_folder, "runs.xlsx");
        object?[]?[] sheet = [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]];
        ExcelFixture.Write(book, ("A", sheet), ("B", sheet), ("C", sheet));
        await viewModel.AddFilesAsync([book]);

        await viewModel.AddFilesAsync([book]);

        viewModel.Trajectories.Should().HaveCount(3);
        viewModel.StatusText.Should().EndWith("· 1 file already loaded");
    }

    /// <summary>F39: an unreadable file added again listed its failure twice.</summary>
    [Fact]
    public async Task AddFilesAsync_UnreadableFileAddedAgain_ListsItsFailureOnce()
    {
        MainViewModel viewModel = Create();
        string notes = Path.Combine(_folder, "notes.csv");
        await File.WriteAllTextAsync(notes, "just,two\nwords,here\n");

        await viewModel.AddFilesAsync([notes]);
        await viewModel.AddFilesAsync([notes]);

        viewModel.Failures.Should().ContainSingle();
    }

    /// <summary>F39: a file that failed while Excel held it kept its failure row after it loaded.</summary>
    [Fact]
    public async Task AddFilesAsync_FileThatFailedThenLoads_DropsItsFailure()
    {
        MainViewModel viewModel = Create();
        string run = Path.Combine(_folder, "run.csv");
        File.Copy(Path.Combine(RepositoryFiles.Samples, "model.csv"), run);
        using (new FileStream(run, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            await viewModel.AddFilesAsync([run]);
        }

        viewModel.Failures.Should().ContainSingle();

        await viewModel.AddFilesAsync([run]);

        viewModel.Trajectories.Should().ContainSingle();
        viewModel.Failures.Should().BeEmpty();
    }

    /// <summary>F40: a dropped folder whose files sat in subfolders loaded nothing, and said nothing.</summary>
    [Fact]
    public async Task AddFilesAsync_FolderWithFilesInSubfolders_LoadsThem()
    {
        MainViewModel viewModel = Create();
        string runs = Path.Combine(_folder, "Runs");
        foreach (string run in new[] { "Run1", "Run2" })
        {
            Directory.CreateDirectory(Path.Combine(runs, run));
            File.Copy(Path.Combine(RepositoryFiles.Samples, "model.csv"), Path.Combine(runs, run, "log.csv"));
        }

        await viewModel.AddFilesAsync([runs]);

        viewModel.Trajectories.Select(item => item.SourcePath).Should().Equal(
            Path.Combine(runs, "Run1", "log.csv"),
            Path.Combine(runs, "Run2", "log.csv"));
    }

    [Fact]
    public async Task AddFilesAsync_FolderWithNothingToRead_SaysSo()
    {
        MainViewModel viewModel = Create();
        string empty = Path.Combine(_folder, "Empty");
        Directory.CreateDirectory(empty);
        await File.WriteAllTextAsync(Path.Combine(empty, "readme.md"), "nothing here");

        await viewModel.AddFilesAsync([empty]);

        viewModel.StatusText.Should().Be("No CSV or Excel files were found in Empty.");
    }

    /// <summary>F50: Excel's ~$ owner file beside an open workbook was listed as a file that failed.</summary>
    [Fact]
    public async Task AddFilesAsync_FolderWithAnExcelOwnerFile_PassesItOver()
    {
        MainViewModel viewModel = Create();
        string folder = Path.Combine(_folder, "Open");
        Directory.CreateDirectory(folder);
        File.Copy(Path.Combine(RepositoryFiles.Samples, "run-moved-and-turned.xlsx"), Path.Combine(folder, "run.xlsx"));
        await File.WriteAllBytesAsync(Path.Combine(folder, "~$run.xlsx"), new byte[165]);

        await viewModel.AddFilesAsync([folder]);

        viewModel.Trajectories.Should().ContainSingle();
        viewModel.Failures.Should().BeEmpty();
    }

    /// <summary>F41: a comparison ending while a drop was still being read hid the busy indicator.</summary>
    [Fact]
    public async Task IsBusy_ComparisonEndsWhileADropIsStillBeingRead_StaysOn()
    {
        using var reading = new ManualResetEventSlim(initialState: true);
        MainViewModel viewModel = Create(load: path =>
        {
            reading.Wait();
            return TrajectoryFileLoader.Load(path);
        });
        await viewModel.AddFilesAsync([Path.Combine(RepositoryFiles.Samples, "model.csv"), Path.Combine(RepositoryFiles.Samples, "run-good.csv")]);
        reading.Reset();

        Task adding = viewModel.AddFilesAsync([Path.Combine(RepositoryFiles.Samples, "run-detour.csv")]);
        viewModel.ToleranceText = "3";
        await viewModel.CurrentComparison;

        viewModel.IsBusy.Should().BeTrue("the drop is still being read");
        reading.Set();
        await adding;
        viewModel.IsBusy.Should().BeFalse();
    }

    private static TrajectoryItemViewModel Item(MainViewModel viewModel, string name) =>
        viewModel.Trajectories.Single(item => item.Name == name);

    private static async Task<MainViewModel> LoadedSamples(StubConfirmation? confirmation = null)
    {
        MainViewModel viewModel = Create(confirmation: confirmation);
        await viewModel.AddFilesAsync([RepositoryFiles.Samples]);
        return viewModel;
    }

    private static MainViewModel Create(
        StubFileDialog? dialog = null,
        StubConfirmation? confirmation = null,
        MemorySettingsStore? store = null,
        Func<string, FileLoadResult>? load = null) =>
        new(
            dialog ?? new StubFileDialog(),
            confirmation ?? new StubConfirmation(answer: true),
            store ?? new MemorySettingsStore(AppSettings.Default),
            TestPalette.Create())
        {
            LoadFile = load ?? TrajectoryFileLoader.Load,
        };
}
