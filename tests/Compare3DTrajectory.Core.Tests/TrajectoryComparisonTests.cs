using System.Globalization;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;
using Compare3DTrajectory.Core.Tests.Loading;
using Compare3DTrajectory.Core.Tests.Samples;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests;

/// <summary>
/// The one call another .NET application makes: two file paths in, a report out, with the loading,
/// the East-North-Up frame and the fit all done inside.
/// </summary>
public sealed class TrajectoryComparisonTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static string Sample(string name) => Path.Combine(RepositoryFiles.Samples, name);

    [Fact]
    public void Compare_GoodRun_Passes()
    {
        ComparisonReport report = TrajectoryComparison.Compare(Sample(SampleScenarios.Model), Sample(SampleScenarios.Good));

        report.Passed.Should().BeTrue();
        report.Issues.Should().BeEmpty();
        report.Summary.CoveragePercent.Should().Be(100);
        report.Tolerance.Should().Be(2);
        report.Fit.IsFitted.Should().BeTrue();
    }

    [Fact]
    public void Compare_RunWithADetour_FailsNamingTheLinesOfEachIssueInItsOwnFile()
    {
        ComparisonReport report = TrajectoryComparison.Compare(Sample(SampleScenarios.Model), Sample(SampleScenarios.Detour));

        report.Passed.Should().BeFalse();
        ReportedIssue detour = report.Issues.Single(issue => issue.Kind == IssueKind.Detour);
        detour.SourcePath.Should().Be(Sample(SampleScenarios.Detour));
        detour.FirstLine.Should().Be(report.Run.LineNumbers[detour.Issue.FirstIndex]);
        detour.LastLine.Should().BeGreaterThan(detour.FirstLine);
        detour.WorstDeviation.Should().BeApproximately(25, 1.5);
        ReportedIssue missed = report.Issues.Single(issue => issue.Kind == IssueKind.Missed);
        missed.SourcePath.Should().Be(Sample(SampleScenarios.Model));
        missed.FirstLine.Should().Be(report.Reference.LineNumbers[missed.Issue.FirstIndex]);
    }

    [Fact]
    public void Compare_FittingOff_ComparesTheRunWhereTheFilePutsIt()
    {
        ComparisonReport report = TrajectoryComparison.Compare(
            Sample(SampleScenarios.Model),
            Sample(SampleScenarios.MovedAndTurned),
            new ComparisonOptions { FitHeading = false });

        report.Passed.Should().BeFalse();
        report.Fit.IsFitted.Should().BeFalse();
    }

    [Fact]
    public void Compare_WiderTolerance_IsUsed()
    {
        ComparisonReport report = TrajectoryComparison.Compare(
            Sample(SampleScenarios.Model),
            Sample(SampleScenarios.Detour),
            new ComparisonOptions { Tolerance = 30 });

        report.Passed.Should().BeTrue();
        report.Tolerance.Should().Be(30);
    }

    /// <summary>Decision: the refusal is thrown, with the window's own message, never returned to be ignored.</summary>
    [Fact]
    public void Compare_RunWithoutAHeader_ThrowsTheRefusalTheWindowShows()
    {
        string run = _folder.WriteText("headerless.csv", "1,2,3\n4,5,6\n");

        Action compare = () => TrajectoryComparison.Compare(Sample(SampleScenarios.Model), run);

        TrajectoryFileException thrown = compare.Should().Throw<TrajectoryFileException>().Which;
        thrown.Message.Should().Be(string.Join(
            Environment.NewLine,
            "Please add in the header for x, y, z.",
            "File: headerless.csv",
            "Columns found: none; line 1 already holds numbers (1, 2, 3)",
            "Example first line: x,y,z"));
        thrown.SourcePath.Should().Be(run);
        thrown.MissingHeader.Should().NotBeNull();
    }

    [Fact]
    public void Compare_ReferenceThatCannotBeRead_ThrowsNamingIt()
    {
        string missing = _folder.File("gone.csv");

        Action compare = () => TrajectoryComparison.Compare(missing, Sample(SampleScenarios.Good));

        TrajectoryFileException thrown = compare.Should().Throw<TrajectoryFileException>().Which;
        thrown.Message.Should().StartWith("gone.csv: ");
        thrown.SourcePath.Should().Be(missing);
        thrown.MissingHeader.Should().BeNull();
    }

    /// <summary>A workbook of several runs is ambiguous: which one was meant cannot be guessed.</summary>
    [Fact]
    public void Compare_WorkbookWithTwoTrajectories_Throws()
    {
        string path = _folder.File("two.xlsx");
        object?[]?[] sheet = [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]];
        ExcelFixture.Write(path, ("Run A", sheet), ("Run B", sheet));

        Action compare = () => TrajectoryComparison.Compare(path, Sample(SampleScenarios.Good));

        compare.Should().Throw<TrajectoryFileException>()
            .WithMessage("two.xlsx holds 2 trajectories (sheets Run A, Run B); compare a file that holds one.");
    }

    [Fact]
    public void Compare_WorkbookWithOneTrajectoryAndANotesSheet_UsesTheTrajectory()
    {
        string path = _folder.File("with-notes.xlsx");
        ExcelFixture.Write(path, ("Notes", [["exported from the range"]]), ("Run", [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]));

        ComparisonReport report = TrajectoryComparison.Compare(path, path);

        report.Passed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void Compare_ToleranceThatIsNotADistance_Throws(double tolerance)
    {
        Action compare = () => TrajectoryComparison.Compare(
            Sample(SampleScenarios.Model),
            Sample(SampleScenarios.Good),
            new ComparisonOptions { Tolerance = tolerance });

        compare.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// F61: the same 30 km pattern flown 20 km further north lies tilted and sunk in the reference's
    /// flat frame, and a fit that only turns about the vertical and moves left false issues on it.
    /// </summary>
    [Fact]
    public void Compare_SameShapeDisplacedOverTheEarth_Passes()
    {
        Route pattern = new Route { Height = _ => 3000 }.Straight(10_000).Turn(2_000, 90).Straight(8_000).Turn(2_000, 90).Straight(6_000);
        var here = new LocalFrame(Wgs84.ToEcef(new GeodeticPosition(1.3521, 103.8198, 0)));
        var north = new LocalFrame(Wgs84.ToEcef(new GeodeticPosition(1.3521 + (20_000 / 110_574.0), 103.8198, 0)));
        string reference = WriteEcef("reference.csv", pattern.Sample(50).Select(here.ToEcef));
        string run = WriteEcef("run.csv", pattern.Sample(50).Select(north.ToEcef));

        ComparisonReport report = TrajectoryComparison.Compare(reference, run, new ComparisonOptions { Tolerance = 5 });

        report.Passed.Should().BeTrue();
        report.Summary.MaxDeviation.Should().BeLessThan(0.5);
        report.Fit.CentroidShift.Length.Should().BeApproximately(20_000, 200);
        report.Fit.TurnDegrees.Should().BeApproximately(0, 0.01);
    }

    private string WriteEcef(string name, IEnumerable<Vec3> points) =>
        _folder.WriteText(name, "x,y,z\n" + string.Join('\n', points.Select(point => string.Create(CultureInfo.InvariantCulture, $"{point.X:R},{point.Y:R},{point.Z:R}"))));

    [Fact]
    public void Compare_Cancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Action compare = () => TrajectoryComparison.Compare(Sample(SampleScenarios.Model), Sample(SampleScenarios.Good), cancellationToken: cancellation.Token);

        compare.Should().Throw<OperationCanceledException>();
    }
}
