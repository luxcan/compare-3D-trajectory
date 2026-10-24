using System.Numerics;
using System.Windows.Media;
using Compare3DTrajectory.App.Viewer;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Tests.Viewer;

public class SceneBuilderTests
{
    private static readonly PlotPalette Palette = TestPalette.Create();

    private static readonly Vec3[] ReferencePoints = [new(1000, 2000, 30), new(1010, 2000, 30), new(1020, 2000, 30)];

    [Fact]
    public void Build_NoTrajectories_IsEmpty()
    {
        SceneBuilder.Build([], [], ColourMode.Deviation, 2, Palette).Should().BeSameAs(SceneModel.Empty);
    }

    [Fact]
    public void Build_Reference_IsCentredOnItsOwnCentroidAndDrawnOnTop()
    {
        SceneModel scene = SceneBuilder.Build([Reference()], [], ColourMode.Deviation, 2, Palette);

        scene.RenderOrigin.Should().Be(new Vec3(1010, 2000, 30));
        SceneLine reference = scene.Lines.Single(line => line.Describe is not null);
        reference.Positions.Should().Equal(new Vector3(-10, 0, 0), new Vector3(0, 0, 0), new Vector3(10, 0, 0));
        reference.Colour.Should().Be(Palette.Reference);
        reference.IsOverlay.Should().BeTrue();
        scene.Lines.Last(line => !line.IsSegments && line.Describe is not null).Should().BeSameAs(reference);
    }

    [Fact]
    public void Build_RunByDeviation_ColoursEachPointByItsBand()
    {
        SceneTrajectory run = Run([0.5, 3, 9]);

        SceneModel scene = SceneBuilder.Build([Reference(), run], [], ColourMode.Deviation, 2, Palette);

        SceneLine line = scene.Lines.First(line => line.Describe is not null);
        line.VertexColours.Should().Equal(Palette.Within, Palette.Near, Palette.Beyond);
        line.IsOverlay.Should().BeFalse();
    }

    [Fact]
    public void Build_RunByFile_UsesTheFilesColourThroughout()
    {
        SceneModel scene = SceneBuilder.Build([Reference(), Run([0.5, 3, 9])], [], ColourMode.File, 2, Palette);

        SceneLine line = scene.Lines.First(line => line.Describe is not null);
        line.VertexColours.Should().BeNull();
        line.Colour.Should().Be(Colors.Blue);
    }

    [Fact]
    public void Build_SelectedRun_IsDrawnThicker()
    {
        SceneTrajectory plain = Run([0, 0, 0]);
        SceneTrajectory selected = plain with { IsSelected = true };

        double Thickness(SceneTrajectory run) => SceneBuilder.Build([Reference(), run], [], ColourMode.File, 2, Palette)
            .Lines.First(line => line.Describe is not null).Thickness;

        Thickness(selected).Should().BeGreaterThan(Thickness(plain));
    }

    [Fact]
    public void Build_Issues_MarkMissedStretchesAndTheSelectionAndNumberEveryIssue()
    {
        var missed = new TrajectoryIssue(IssueKind.Missed, 0, 1, 10, 5, ReferencePoints[0], ReferencePoints[..2]);
        var detour = new TrajectoryIssue(IssueKind.Detour, 1, 2, 10, 4, ReferencePoints[1], ReferencePoints[1..]);

        SceneModel scene = SceneBuilder.Build(
            [Reference()],
            [new SceneIssue(1, missed, IsSelected: false), new SceneIssue(2, detour, IsSelected: true)],
            ColourMode.Deviation,
            2,
            Palette);

        scene.Lines.Should().Contain(line => line.Colour == Palette.Missed && line.IsOverlay);
        scene.Lines.Should().Contain(line => line.Colour == Palette.Highlight && line.IsOverlay);
        scene.Labels.Select(label => label.Text).Should().Equal("1", "2");
        scene.Labels[0].Position.Should().Be(new Vector3(-10, 0, 0));
    }

    /// <summary>Seen on screen: with the reference hidden, the stretches the runs missed, and their numbers, still traced it.</summary>
    [Fact]
    public void Build_ReferenceHidden_DrawsAMissedStretchOnlyOnceItIsPicked()
    {
        var missed = new TrajectoryIssue(IssueKind.Missed, 0, 1, 10, 5, ReferencePoints[0], ReferencePoints[..2]);
        var detour = new TrajectoryIssue(IssueKind.Detour, 1, 2, 10, 4, ReferencePoints[1], ReferencePoints[1..]);

        SceneModel hidden = SceneBuilder.Build(
            [Run([0.5, 3, 9])], [new SceneIssue(1, missed, IsSelected: false), new SceneIssue(2, detour, IsSelected: false)], ColourMode.Deviation, 2, Palette);
        SceneModel picked = SceneBuilder.Build([Run([0.5, 3, 9])], [new SceneIssue(1, missed, IsSelected: true)], ColourMode.Deviation, 2, Palette);

        hidden.Lines.Should().NotContain(line => line.Colour == Palette.Missed);
        hidden.Labels.Select(label => label.Text).Should().Equal("2");
        picked.Lines.Should().Contain(line => line.Colour == Palette.Highlight);
        picked.Labels.Select(label => label.Text).Should().Equal("1");
    }

    /// <summary>The 3D view keeps a line only when it gets the very same arrays again, so an unchanged trajectory must.</summary>
    [Fact]
    public void Build_SameTrajectoryWithTheSameCache_HandsBackTheSameLine()
    {
        var cache = new SceneCache();
        SceneTrajectory run = Run([0.5, 3, 9]);
        SceneLine RunLine(double tolerance) => SceneBuilder.Build([Reference(), run], [], ColourMode.Deviation, tolerance, Palette, cache: cache)
            .Lines.First(line => line.Describe is not null);

        SceneLine first = RunLine(2);
        SceneLine again = RunLine(2);
        SceneLine wider = RunLine(5);

        again.Should().Be(first);
        again.Positions.Should().BeSameAs(first.Positions);
        wider.Positions.Should().BeSameAs(first.Positions, "a new tolerance recolours the run, it does not move it");
        wider.VertexColours.Should().NotBeSameAs(first.VertexColours);
    }

    [Fact]
    public void Build_CentreGiven_CentresOnItRatherThanOnWhatIsDrawn()
    {
        SceneModel scene = SceneBuilder.Build([Run([0, 0, 0])], [], ColourMode.Deviation, 2, Palette, centre: new Vec3(1000, 2000, 30));

        scene.RenderOrigin.Should().Be(new Vec3(1000, 2000, 30));
        scene.Lines.First(line => line.Describe is not null).Positions[0].Should().Be(Vector3.Zero);
    }

    [Fact]
    public void Build_AnyScene_HasAGroundGridAtTheLowestPointAndStartAndEndMarkers()
    {
        SceneModel scene = SceneBuilder.Build([Reference()], [], ColourMode.Deviation, 2, Palette);

        SceneLine grid = scene.Lines.Single(line => line.IsSegments);
        grid.Positions.Should().OnlyContain(position => position.Z == 0);
        scene.GridSpacing.Should().Be(2);
        scene.Markers.Should().HaveCount(2);
        scene.Markers.Select(marker => marker.IsSquare).Should().Equal(false, true);
    }

    /// <summary>
    /// F52: a point out at 1e39 metres overflows float, and the grid loop stepped towards infinity,
    /// or stood still at a value too large for one more step to change, until memory ran out.
    /// </summary>
    [Fact]
    public async Task Build_PointBeyondTheRangeOfFloat_StillFinishesWithABoundedGrid()
    {
        Vec3[] points = [.. ReferencePoints, new Vec3(1e39, 1e39, 1e39)];
        var reference = new SceneTrajectory(points, null, Colors.Blue, IsReference: true, IsSelected: false, index => $"reference {index}");

        SceneModel scene = await Task.Run(() => SceneBuilder.Build([reference], [], ColourMode.Deviation, 2, Palette))
            .WaitAsync(TimeSpan.FromSeconds(10));

        scene.Lines.Single(line => line.IsSegments).Positions.Count.Should().BeLessThan(1000);
    }

    /// <summary>
    /// Seen on screen: a straight leg running due north has no width, so every grid line across it
    /// had no length, and the view showed no ground at all.
    /// </summary>
    [Fact]
    public void Build_StraightLegDueNorth_StillHasAGroundOfSomeWidth()
    {
        Vec3[] leg = [new(0, -5000, 0), new(0, 0, 0), new(0, 5000, 0)];
        var reference = new SceneTrajectory(leg, null, Colors.Blue, IsReference: true, IsSelected: false, index => $"reference {index}");

        SceneModel scene = SceneBuilder.Build([reference], [], ColourMode.Deviation, 2, Palette);

        SceneLine grid = scene.Lines.Single(line => line.IsSegments);
        float width = grid.Positions.Max(position => position.X) - grid.Positions.Min(position => position.X);
        width.Should().BeGreaterThanOrEqualTo((float)(4 * scene.GridSpacing));
    }

    [Theory]
    [InlineData(0.3, 0.2)]
    [InlineData(1.2, 1)]
    [InlineData(2.4, 2)]
    [InlineData(37, 50)]
    [InlineData(80, 100)]
    [InlineData(0, 1)]
    public void NiceStep_RoughSpacing_RoundsToOneTwoOrFive(double rough, double expected)
    {
        SceneBuilder.NiceStep(rough).Should().BeApproximately(expected, 1e-12);
    }

    private static SceneTrajectory Reference() =>
        new(ReferencePoints, null, Colors.Blue, IsReference: true, IsSelected: false, index => $"reference {index}");

    private static SceneTrajectory Run(double[] deviations) =>
        new(ReferencePoints, deviations, Colors.Blue, IsReference: false, IsSelected: false, index => $"run {index}");
}
