using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.App.Settings;
using Compare3DTrajectory.App.Shell;
using Compare3DTrajectory.App.Viewer;
using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;

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
        MemorySettingsStore? store = null) =>
        new(
            dialog ?? new StubFileDialog(),
            confirmation ?? new StubConfirmation(answer: true),
            store ?? new MemorySettingsStore(AppSettings.Default),
            TestPalette.Create());
}
