using System.Globalization;
using System.IO.Compression;
using System.Text;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests.Loading;

/// <summary>
/// Rows that cannot be a position, such as an unset value, a no-data marker or a one-row glitch,
/// are skipped and listed by line, so one of them can no longer spoil a whole file or comparison.
/// </summary>
public sealed class ImplausibleRowTests : IDisposable
{
    private static readonly LocalFrame Singapore = new(Wgs84.ToEcef(new GeodeticPosition(1.3521, 103.8198, 30)));

    private static readonly ComparisonSettings TwoMetres = new(2);

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    /// <summary>F12: three uninitialised 0,0,0 WorldLocations used to make a whole ECEF file local.</summary>
    [Fact]
    public void Load_GeocentricFileWithZeroRows_StaysGeocentricAndListsThem()
    {
        List<Vec3> route = Route.Standard().Sample(10);
        List<string> rows = Rows(route.Select(Singapore.ToEcef));
        rows.InsertRange(0, ["0,0,0", "0,0,0", "0,0,0"]);

        LoadedTrajectory loaded = Load("run.csv", rows);

        loaded.DetectedKind.Should().Be(CoordinateKind.Geocentric);
        loaded.Points.Should().HaveCount(route.Count);
        loaded.SkippedRows.Select(row => row.LineNumber).Should().Equal(2, 3, 4);
        loaded.SkippedRows[0].Reason.Should().Be("0, 0, 0 is 0 km from the Earth's centre, nowhere near its surface");
    }

    /// <summary>F12: the same zero row, end to end, against the committed model.</summary>
    [Fact]
    public void Compare_GoodSampleRunWithAZeroRow_HasNoIssues()
    {
        List<string> lines = [.. File.ReadAllLines(Path.Combine(RepositoryFiles.Samples, "run-good.csv"))];
        lines.Insert(1, "0,0,0");
        string run = _folder.WriteText("run-good-with-zero.csv", string.Join('\n', lines));

        ComparisonResult result = Compare(Path.Combine(RepositoryFiles.Samples, "model.csv"), run);

        result.Issues.Should().BeEmpty();
    }

    /// <summary>F53, F56: an unset value as C++ or a float writes it used to load silently and then throw or hang.</summary>
    [Theory]
    [InlineData("1.7976931348623157E+308,0,0", "1.79769E+308 is too large to be a position")]
    [InlineData("3.4028235E+38,3.4028235E+38,3.4028235E+38", "3.40282E+38 is too large to be a position")]
    [InlineData("1e30,1e30,1e30", "1E+30 is too large to be a position")]
    [InlineData("1e39,1e39,1e39", "1E+39 is too large to be a position")]
    [InlineData("0,1e200,0", "1E+200 is too large to be a position")]
    public void Load_RowWithAValueTooLargeForAPosition_IsSkippedAndSaysWhy(string row, string reason)
    {
        List<string> rows = Rows(Route.Standard().Sample(10));
        rows[20] = row;

        LoadedTrajectory loaded = Load("run.csv", rows);

        loaded.SkippedRows.Should().Equal(new SkippedRow(22, reason));
    }

    [Fact]
    public void Load_WorkbookRowWithAnUnsetValue_IsSkipped()
    {
        string path = _folder.File("run.xlsx");
        List<object?[]?> rows = [["x", "y", "z"], .. Route.Standard().Sample(10).Select(point => new object?[] { point.X, point.Y, point.Z })];
        rows[21] = [double.MaxValue, double.MaxValue, double.MaxValue];
        ExcelFixture.Write(path, ("Run", [.. rows]));

        LoadedTrajectory loaded = TrajectoryFileLoader.Load(path).Trajectories.Single();

        loaded.SkippedRows.Should().ContainSingle().Which.LineNumber.Should().Be(22);
    }

    /// <summary>F51, F55: a -9999 row took over the heading fit and made every nearest-point query slow.</summary>
    [Fact]
    public void Load_NoDataMarkerRow_IsSkippedAndSaysWhy()
    {
        List<string> rows = Rows(Route.Standard().Sample(10));
        rows[30] = "-9999,-9999,-9999";

        LoadedTrajectory loaded = Load("run.csv", rows);

        loaded.SkippedRows.Should().Equal(new SkippedRow(32, "-9999, -9999, -9999 is a no-data marker, not a position"));
    }

    /// <summary>F51: with the row skipped, the fit lines the run up as if it had never been there.</summary>
    [Fact]
    public void Compare_RunWithANoDataMarkerRow_HasNoIssues()
    {
        Route route = Route.Standard();
        string reference = _folder.WriteText("model.csv", Csv(Rows(route.Sample(5))));
        List<string> run = Rows(Noise.Jitter(route.Sample(4), sigma: 0.2, seed: 7));
        run[197] = "-9999,-9999,-9999";
        string runPath = _folder.WriteText("run.csv", Csv(run));

        ComparisonResult result = Compare(reference, runPath);

        result.Issues.Should().BeEmpty();
        result.Fit.Transform.HeadingDegrees.Should().BeApproximately(0, 0.5);
    }

    /// <summary>F53: the same with an unset value, which used to throw IndexOutOfRangeException.</summary>
    [Fact]
    public void Compare_RunWithAnUnsetValue_HasNoIssues()
    {
        Route route = Route.Standard();
        string reference = _folder.WriteText("model.csv", Csv(Rows(route.Sample(5))));
        List<string> run = Rows(Noise.Jitter(route.Sample(4), sigma: 0.2, seed: 7));
        run[100] = "1.7976931348623157E+308,1.7976931348623157E+308,1.7976931348623157E+308";
        string runPath = _folder.WriteText("run.csv", Csv(run));

        Compare(reference, runPath).Issues.Should().BeEmpty();
    }

    /// <summary>F57: a reference row moved 500 m sideways used to be charged to every run as a miss and a detour.</summary>
    [Fact]
    public void Load_GlitchRowInTheReference_IsSkippedAndSaysWhy()
    {
        List<Vec3> route = Route.Standard().Sample(10);
        List<string> rows = Rows(route);
        rows[80] = Row(route[80] + Sideways(route, 80, 500));

        LoadedTrajectory loaded = Load("model.csv", rows);

        loaded.SkippedRows.Should().Equal(new SkippedRow(82, "jumps 500 m off the path and straight back"));
    }

    [Fact]
    public void Compare_GoodRunAgainstAReferenceWithAGlitchRow_HasNoIssues()
    {
        Route route = Route.Standard();
        List<Vec3> model = route.Sample(10);
        List<string> rows = Rows(model);
        rows[80] = Row(model[80] + Sideways(model, 80, 500));
        string reference = _folder.WriteText("model.csv", Csv(rows));
        string run = _folder.WriteText("run.csv", Csv(Rows(Noise.Jitter(route.Sample(1), sigma: 0.3, seed: 3))));

        ComparisonResult result = Compare(reference, run);

        result.Issues.Should().BeEmpty();
        result.Summary.CoveragePercent.Should().Be(100);
    }

    [Theory]
    [InlineData(0, "is 1,000 m from where the path starts")]
    [InlineData(-1, "is 1,000 m from where the path ends")]
    public void Load_SpawnRowAtEitherEnd_IsSkipped(int index, string reason)
    {
        List<Vec3> route = Route.Standard().Sample(5);
        List<string> rows = Rows(route);
        int at = index < 0 ? rows.Count + index : index;
        Vec3 next = route[index < 0 ? at - 1 : at + 1];
        rows[at] = Row(next + new Vec3(1000, 0, 0));

        LoadedTrajectory loaded = Load("run.csv", rows);

        loaded.SkippedRows.Should().Equal(new SkippedRow(at + 2, reason));
    }

    /// <summary>F63: points 40 km under the ellipsoid passed the shell test and compared cleanly.</summary>
    [Fact]
    public void Load_GeocentricRowsDeepUnderground_AreSkipped()
    {
        var underground = new LocalFrame(Wgs84.ToEcef(new GeodeticPosition(1.3521, 103.8198, -40_000)));
        string path = _folder.WriteText("deep.csv", Csv(Rows(Route.Standard().Sample(10).Select(underground.ToEcef))));

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Single().Reason.Should().StartWith("Only 0 readable points").And.Contain("below the WGS84 ellipsoid");
    }

    [Fact]
    public void Load_LocalFileThatStartsAtTheOrigin_KeepsEveryRow()
    {
        Load("run.csv", Rows(Route.Standard().Sample(10))).SkippedRows.Should().BeEmpty();
    }

    /// <summary>A gap in the recording is one long step that never comes back, so it is not a glitch.</summary>
    [Fact]
    public void Load_RunWithAGapInTheRecording_KeepsBothSides()
    {
        List<Vec3> route = Route.Standard().Sample(1);
        route.RemoveRange(500, 80);

        Load("run.csv", Rows(route)).SkippedRows.Should().BeEmpty();
    }

    /// <summary>F9: a workbook copied while it was still being written has a sheet cut off mid-tag.</summary>
    [Fact]
    public void Load_WorkbookWithTruncatedSheetXml_FailsWithAReasonInsteadOfThrowing()
    {
        string path = _folder.File("partial.xlsx");
        ExcelFixture.Write(path, ("Run", [["x", "y", "z"], [1.0, 2.0, 3.0], [4.0, 5.0, 6.0]]));
        using (ZipArchive zip = ZipFile.Open(path, ZipArchiveMode.Update))
        {
            ZipArchiveEntry sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!;
            string xml;
            using (var reader = new StreamReader(sheet.Open()))
            {
                xml = reader.ReadToEnd();
            }

            sheet.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("xl/worksheets/sheet1.xml").Open());
            writer.Write(xml[..(xml.Length / 2)]);
        }

        FileLoadResult result = TrajectoryFileLoader.Load(path);

        result.Trajectories.Should().BeEmpty();
        result.Failures.Should().ContainSingle().Which.Name.Should().Be("partial.xlsx");
    }

    private LoadedTrajectory Load(string name, List<string> rows)
    {
        FileLoadResult result = TrajectoryFileLoader.Load(_folder.WriteText(name, Csv(rows)));
        result.Failures.Should().BeEmpty();
        return result.Trajectories.Single();
    }

    private static ComparisonResult Compare(string referencePath, string runPath)
    {
        var frame = new WorkingFrame();
        LoadedTrajectory reference = TrajectoryFileLoader.Load(referencePath).Trajectories.Single();
        LoadedTrajectory run = TrajectoryFileLoader.Load(runPath).Trajectories.Single();
        var preparedReference = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(reference.Points, reference.DetectedKind));
        var preparedRun = new PreparedTrajectory(Guid.NewGuid(), frame.ToWorking(run.Points, run.DetectedKind));
        return new ComparisonEngine().Compare(preparedReference, preparedRun, TwoMetres, fitHeading: true);
    }

    /// <summary>A level move square to the path at point <paramref name="index"/>.</summary>
    private static Vec3 Sideways(List<Vec3> path, int index, double metres)
    {
        Vec3 along = path[index + 1] - path[index - 1];
        return new Vec3(-along.Y, along.X, 0) * (metres / Math.Sqrt((along.X * along.X) + (along.Y * along.Y)));
    }

    private static List<string> Rows(IEnumerable<Vec3> points) => [.. points.Select(Row)];

    private static string Row(Vec3 point) => string.Create(CultureInfo.InvariantCulture, $"{point.X:R},{point.Y:R},{point.Z:R}");

    private static string Csv(IEnumerable<string> rows) => new StringBuilder("x,y,z\n").AppendJoin('\n', rows).Append('\n').ToString();
}
