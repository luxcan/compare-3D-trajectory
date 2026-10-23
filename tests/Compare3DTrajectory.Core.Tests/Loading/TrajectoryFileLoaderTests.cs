using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core.Tests.Loading;

public sealed class TrajectoryFileLoaderTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private static void AssertWorkbookPoints(FileLoadResult result, string expectedName)
    {
        result.Failures.Should().BeEmpty();
        LoadedTrajectory trajectory = result.Trajectories.Should().ContainSingle().Subject;
        trajectory.Name.Should().Be(expectedName);
        trajectory.Points.Should().Equal(WorkbookPoints);
        trajectory.LineNumbers.Should().Equal(2, 4);
        trajectory.DetectedKind.Should().Be(CoordinateKind.Geocentric);
    }

    [Fact]
    public void Load_Csv_GivesOneTrajectoryNamedAfterTheFile()
    {
        string path = _folder.WriteText("run 1.csv", "x,y,z\n1,2,3\n4,5,6\n");

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Failures.Should().BeEmpty();
        LoadedTrajectory trajectory = result.Trajectories.Should().ContainSingle().Subject;
        trajectory.Name.Should().Be("run 1.csv");
        trajectory.SourcePath.Should().Be(path);
        trajectory.DetectedKind.Should().Be(CoordinateKind.Local);
    }

    [Fact]
    public void Load_CsvOfGeocentricPoints_DetectsGeocentric()
    {
        Vec3 a = Wgs84.ToEcef(new GeodeticPosition(1.35, 103.82, 10));
        Vec3 b = Wgs84.ToEcef(new GeodeticPosition(1.351, 103.821, 15));
        string path = _folder.WriteText("ecef.csv", $"{a.X:R},{a.Y:R},{a.Z:R}\n{b.X:R},{b.Y:R},{b.Z:R}\n");

        LoadedTrajectory trajectory = TrajectoryFileLoader.Load(path).Trajectories.Single();

        trajectory.DetectedKind.Should().Be(CoordinateKind.Geocentric);
        trajectory.Points[0].Should().Be(a);
    }

    private static readonly Vec3[] WorkbookPoints =
    [
        new(-1_514_567.12345678, 6_191_234.98765432, 149_876.5),
        new(-1_514_570.0, 6_191_230.0, 149_880.25),
    ];

    [Fact]
    public void Load_Xlsx_ReadsNumbersExactlyWithRowNumbers()
    {
        string path = _folder.File("book.xlsx");
        ExcelFixture.Write(path, ("Data",
        [
            ["WorldLocation.X", "WorldLocation.Y", "WorldLocation.Z"],
            [WorkbookPoints[0].X, WorkbookPoints[0].Y, WorkbookPoints[0].Z],
            null,
            [WorkbookPoints[1].X, WorkbookPoints[1].Y, WorkbookPoints[1].Z],
        ]));

        AssertWorkbookPoints(TrajectoryFileLoader.Load(path), "book.xlsx");
    }

    /// <summary>
    /// The fixture was saved by Excel itself as an Excel 97-2003 workbook, holding the same cells as
    /// the .xlsx test: a header, a row, a blank row and a row.
    /// </summary>
    [Fact]
    public void Load_LegacyXls_ReadsNumbersExactlyWithRowNumbers()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "legacy-format.xls");

        AssertWorkbookPoints(TrajectoryFileLoader.Load(path), "legacy-format.xls");
    }
    [Fact]
    public void Load_WorkbookWithSeveralSheets_GivesATrajectoryPerSheetAndSkipsEmptyOnes()
    {
        string path = _folder.File("runs.xlsx");
        ExcelFixture.Write(
            path,
            ("Run A", [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]),
            ("Empty", []),
            ("Run B", [[7.0, 8.0, 9.0], [10.0, 11.0, 12.0]]),
            ("Notes", [["just some text"]]));

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Select(trajectory => trajectory.Name).Should().Equal("runs.xlsx › Run A", "runs.xlsx › Run B");
        result.Failures.Should().ContainSingle().Which.Name.Should().Be("runs.xlsx › Notes");
    }

    [Fact]
    public void Load_UnsupportedExtension_FailsWithAReason()
    {
        string path = _folder.WriteText("track.kml", "<kml/>");

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Single().Reason.Should().Be("'.kml' files are not supported; use CSV or Excel.");
    }

    [Fact]
    public void Load_MissingFile_FailsWithTheSystemsReason()
    {
        FileLoadResult result = TrajectoryFileLoader.Load(_folder.File("gone.csv"));

        result.Failures.Single().Reason.Should().Contain("gone.csv");
    }

    [Fact]
    public void Load_FileThatIsNotReallyAWorkbook_FailsWithAReason()
    {
        string path = _folder.WriteText("fake.xlsx", "x,y,z\n1,2,3\n");

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Should().ContainSingle();
    }

    [Fact]
    public void Load_CsvStillOpenForWritingElsewhere_IsStillRead()
    {
        string path = _folder.WriteText("open.csv", "1,2,3\n4,5,6\n");
        using var heldOpen = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        TrajectoryFileLoader.Load(path).Trajectories.Should().ContainSingle();
    }
}
