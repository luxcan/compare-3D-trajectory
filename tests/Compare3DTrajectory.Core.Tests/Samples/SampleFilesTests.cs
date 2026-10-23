using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core.Tests.Samples;

/// <summary>
/// Opens the committed sample files through the real loader and compares them the way the app does,
/// so the samples are proven to show exactly the faults they were made with.
/// </summary>
public class SampleFilesTests
{
    private static readonly string SamplesFolder = RepositoryFiles.Samples;

    private static readonly ComparisonSettings TwoMetres = new(2);

    /// <summary>
    /// Rewrites the samples folder. Skipped unless COMPARE3DTRAJECTORY_WRITE_SAMPLES=1, because the
    /// workbook would come out with new bytes every run.
    /// </summary>
    [SkippableFact]
    public void WriteSamples_WhenRequested_RewritesTheSamplesFolder()
    {
        Skip.IfNot(
            Environment.GetEnvironmentVariable("COMPARE3DTRAJECTORY_WRITE_SAMPLES") == "1",
            "Set COMPARE3DTRAJECTORY_WRITE_SAMPLES=1 to rewrite samples/.");

        SampleScenarios.WriteAll(SamplesFolder);
    }

    [Fact]
    public void Samples_GoodRun_HasNoIssues()
    {
        ComparisonResult result = CompareWithModel(SampleScenarios.Good);

        result.Issues.Should().BeEmpty();
        result.Summary.CoveragePercent.Should().Be(100);
    }

    [Fact]
    public void Samples_DetourRun_HasOneDetourAndTheStretchItMissed()
    {
        ComparisonResult result = CompareWithModel(SampleScenarios.Detour);

        result.Issues.Select(issue => issue.Kind).Should().BeEquivalentTo([IssueKind.Detour, IssueKind.Missed]);
        result.Issues.Single(issue => issue.Kind == IssueKind.Detour).WorstDeviation.Should().BeApproximately(25, 1.5);
    }

    [Fact]
    public void Samples_CutCornerRun_HasOneDetourAndTheMissedCorner()
    {
        ComparisonResult result = CompareWithModel(SampleScenarios.CutCorner);

        result.Issues.Select(issue => issue.Kind).Should().BeEquivalentTo([IssueKind.Detour, IssueKind.Missed]);
    }

    [Fact]
    public void Samples_MovedAndTurnedRun_FitsBackWithNoIssues()
    {
        ComparisonResult result = CompareWithModel(SampleScenarios.MovedAndTurned);

        result.Issues.Should().BeEmpty();
        result.Fit.Transform.HeadingDegrees.Should().BeApproximately(-SampleScenarios.PlantedTurnDegrees, 0.2);
        result.Fit.CentroidShift.Length.Should().BeGreaterThan(100);
    }

    [Fact]
    public void Samples_EveryFile_IsGeocentricAndFullyReadable()
    {
        foreach (string name in new[] { SampleScenarios.Model, SampleScenarios.Good, SampleScenarios.Detour, SampleScenarios.CutCorner, SampleScenarios.MovedAndTurned })
        {
            LoadedTrajectory trajectory = Load(name);

            trajectory.DetectedKind.Should().Be(CoordinateKind.Geocentric, name);
            trajectory.SkippedRows.Should().BeEmpty(name);
        }
    }

    private static ComparisonResult CompareWithModel(string runName)
    {
        var frame = new WorkingFrame();
        LoadedTrajectory model = Load(SampleScenarios.Model);
        LoadedTrajectory run = Load(runName);
        var reference = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(model.Points, model.DetectedKind));
        var prepared = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(run.Points, run.DetectedKind));

        return new ComparisonEngine().Compare(reference, prepared, TwoMetres, fitHeading: true);
    }

    private static LoadedTrajectory Load(string name)
    {
        FileLoadResult result = TrajectoryFileLoader.Load(Path.Combine(SamplesFolder, name));
        result.Failures.Should().BeEmpty(name);
        return result.Trajectories.Single();
    }
}
