using System.Windows.Media;
using Compare3DTrajectory.App.Files;
using Compare3DTrajectory.App.Issues;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.App.Tests.Issues;

public class IssueRowViewModelTests
{
    private static readonly TrajectoryItemViewModel Reference = Item("model.csv", 20);

    private static readonly TrajectoryItemViewModel Run = Item("run.csv", 10);

    [Theory]
    [InlineData(0, 4, "Off path at start")]
    [InlineData(5, 9, "Off path at end")]
    public void KindText_OffPathAtOneEnd_SaysWhichEnd(int first, int last, string kind)
    {
        Row(new TrajectoryIssue(IssueKind.OffPath, first, last, 10, 5, Vec3.Zero, [Vec3.Zero])).KindText.Should().Be(kind);
    }

    /// <summary>
    /// F66: a run off the path from its first point to its last, the clearest sign of a file read the
    /// wrong way, was labelled as if it had only started badly.
    /// </summary>
    [Fact]
    public void KindText_OffPathFromFirstPointToLast_SaysAllTheWay()
    {
        IssueRowViewModel row = Row(new TrajectoryIssue(IssueKind.OffPath, 0, 9, 90, 700, Vec3.Zero, [Vec3.Zero]));

        row.KindText.Should().Be("Off path all the way");
        row.WhereText.Should().Be("run lines 2–11");
    }

    private static IssueRowViewModel Row(TrajectoryIssue issue) => new(1, Run, Reference, issue);

    private static TrajectoryItemViewModel Item(string name, int count) => new(
        new LoadedTrajectory(
            name,
            $@"C:\Runs\{name}",
            [.. Enumerable.Range(0, count).Select(i => new Vec3(i, 0, 0))],
            [.. Enumerable.Range(2, count)],
            [],
            "x, y, z from columns 'x', 'y', 'z'",
            null,
            CoordinateKind.Local),
        Colors.Blue);
}
