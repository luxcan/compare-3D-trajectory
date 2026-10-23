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
        string path = _folder.WriteText("ecef.csv", $"x,y,z\n{a.X:R},{a.Y:R},{a.Z:R}\n{b.X:R},{b.Y:R},{b.Z:R}\n");

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
            ("Run B", [["X", "Y", "Z"], [7.0, 8.0, 9.0], [10.0, 11.0, 12.0]]),
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
        string path = _folder.WriteText("open.csv", "x,y,z\n1,2,3\n4,5,6\n");
        using var heldOpen = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

        TrajectoryFileLoader.Load(path).Trajectories.Should().ContainSingle();
    }

    /// <summary>A refused sheet is named with its workbook, and the example says where the header goes.</summary>
    [Fact]
    public void Load_HeaderlessSheet_IsRefusedNamingTheSheetWithACellExample()
    {
        string path = _folder.File("runs.xlsx");
        ExcelFixture.Write(
            path,
            ("Run A", [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]),
            ("Run B", [[7.0, 8.0, 9.0], [10.0, 11.0, 12.0]]));

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().ContainSingle();
        LoadFailure failure = result.Failures.Should().ContainSingle().Subject;
        failure.Name.Should().Be("runs.xlsx › Run B");
        failure.Reason.Should().Be(string.Join(
            Environment.NewLine,
            "Please add in the header for x, y, z.",
            "File: runs.xlsx › Run B",
            "Columns found: none; row 1 already holds numbers (7, 8, 9)",
            "Example first row: x, y, z in cells A1, B1, C1"));
        failure.MissingHeader.Should().NotBeNull();
    }

    /// <summary>F11: a headerless time, X, Y, Z export near the equator was read as geocentric with time as X.</summary>
    [Fact]
    public void Load_HeaderlessTimeAndEcefColumns_IsRefusedRatherThanReadWithTimeAsX()
    {
        string path = _folder.WriteText("mak.csv", "0,-1520987.1,6192349.2,149262.3\n0.1,-1520988.1,6192348.2,149263.3\n");

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Single().Reason.Should().StartWith("Please add in the header for x, y, z.");
    }

    /// <summary>F73: a blank or error cell in row 1 of a headerless sheet made that row a "header" that vanished.</summary>
    [Fact]
    public void Load_HeaderlessSheetWithABlankFirstCell_IsRefused()
    {
        string path = _folder.File("blank.xlsx");
        ExcelFixture.Write(path, ("Data",
        [
            [-1_523_164.9, null, 149_264.1],
            [-1_523_165.9, 6_191_816.0, 149_265.1],
            [-1_523_166.9, 6_191_815.0, 149_266.1],
        ]));

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Single().MissingHeader.Should().NotBeNull();
    }

    /// <summary>F75: a title in A1 and the header further down and across, as a report is laid out in Excel.</summary>
    [Fact]
    public void Load_SheetWithATitleAboveAnOffsetHeader_ReadsTheRowsUnderTheHeader()
    {
        string path = _folder.File("report.xlsx");
        ExcelFixture.Write(path, ("Export",
        [
            ["Trajectory export 2026-10-23"],
            null,
            null,
            null,
            [null, null, "x", "y", "z"],
            [null, null, 1.0, 2.0, 3.0],
            [null, null, 4.0, 5.0, 6.0],
            [null, null, 7.0, 8.0, 9.0],
            [null, null, 10.0, 11.0, 12.0],
        ]));

        LoadedTrajectory trajectory = TrajectoryFileLoader.Load(path).Trajectories.Should().ContainSingle().Subject;

        trajectory.Points.Should().HaveCount(4);
        trajectory.LineNumbers.Should().Equal(6, 7, 8, 9);
        trajectory.Note.Should().Be("Row 1, above the header, was not read.");
    }

    /// <summary>A worksheet keeps its columns, so a value beyond the header's last column moves nothing.</summary>
    [Fact]
    public void Load_SheetRowWithANoteBeyondTheHeader_IsStillRead()
    {
        string path = _folder.File("notes.xlsx");
        ExcelFixture.Write(path, ("Data", [["x", "y", "z"], [1.0, 2.0, 3.0, null, "turn starts"], [4.0, 5.0, 6.0]]));

        TrajectoryFileLoader.Load(path).Trajectories.Single().Points.Should().HaveCount(2);
    }
}
