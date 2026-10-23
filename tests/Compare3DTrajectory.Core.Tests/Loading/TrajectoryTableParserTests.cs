using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core.Tests.Loading;

public class TrajectoryTableParserTests
{
    [Fact]
    public void Parse_HeaderXyz_ReadsPointsAndNamesTheColumns()
    {
        TableParseResult result = Parse("x,y,z\n1,2,3\n4,5,6");

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6));
        result.LineNumbers.Should().Equal(2, 3);
        result.ColumnDescription.Should().Be("x, y, z from columns 'x', 'y', 'z'");
        result.FailureReason.Should().BeNull();
    }

    [Fact]
    public void Parse_NoHeader_ReadsTheFirstRowAsData()
    {
        TableParseResult result = Parse("1.5,-2e3,3\n4,5,6");

        result.Points.Should().Equal(new Vec3(1.5, -2000, 3), new Vec3(4, 5, 6));
        result.ColumnDescription.Should().Be("x, y, z from columns 1, 2, 3");
    }

    [Theory]
    [InlineData("Time,WorldLocation.X,WorldLocation.Y,WorldLocation.Z")]
    [InlineData("time;World Location X;World Location Y;World Location Z")]
    [InlineData("t\tpos_x\tpos_y\tpos_z")]
    [InlineData("t,PositionX,PositionY,PositionZ")]
    [InlineData("t,X (m),Y (m),Z (m)")]
    public void Parse_NamedColumnsAfterATimeColumn_SkipsTheTimeColumn(string header)
    {
        char delimiter = header.Contains(';') ? ';' : header.Contains('\t') ? '\t' : ',';
        string row = string.Join(delimiter, "0.5", "10", "20", "30");

        TableParseResult result = Parse($"{header}\n{row}\n{row}");

        result.Points[0].Should().Be(new Vec3(10, 20, 30));
        result.Note.Should().BeNull();
    }

    [Fact]
    public void Parse_VelocityColumnsBeforePositionColumns_PrefersPosition()
    {
        TableParseResult result = Parse("Velocity X,Velocity Y,Velocity Z,Position X,Position Y,Position Z\n1,1,1,7,8,9\n1,1,1,7,8,9");

        result.Points[0].Should().Be(new Vec3(7, 8, 9));
    }

    [Fact]
    public void Parse_HeaderWithoutAxisNames_FallsBackToTheFirstThreeNumericColumns()
    {
        TableParseResult result = Parse("name,east,north,up\nA,1,2,3\nB,4,5,6");

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6));
        result.ColumnDescription.Should().Be("x, y, z from columns 'east', 'north', 'up'");
    }

    [Fact]
    public void Parse_FourNumericColumnsAndNoHeader_UsesTheFirstThreeAndSaysSo()
    {
        TableParseResult result = Parse("1,2,3,4\n5,6,7,8");

        result.Points[0].Should().Be(new Vec3(1, 2, 3));
        result.Note.Should().Be("4 numeric columns found; the first three were used as x, y, z.");
    }

    [Fact]
    public void Parse_BadRows_AreReportedWithLineNumbersAndReasons()
    {
        TableParseResult result = Parse("x,y,z\n1,2,3\n1,abc,3\n4,5\n7,8,9\n1,NaN,2");

        result.Points.Should().HaveCount(2);
        result.SkippedRows.Should().Equal(
            new SkippedRow(3, "'abc' in column 2 is not a number"),
            new SkippedRow(4, "no value in column 3"),
            new SkippedRow(6, "'NaN' in column 2 is not a number"));
    }

    [Fact]
    public void Parse_OnlyOneReadablePoint_FailsWithAReason()
    {
        TableParseResult result = Parse("x,y,z\n1,2,3\nfoo,bar,baz");

        result.FailureReason.Should().Be("Only 1 readable point; a trajectory needs at least two.");
    }

    [Fact]
    public void Parse_HeaderOnly_FailsWithAReason()
    {
        Parse("x,y,z").FailureReason.Should().Be("There is a header row but no data under it.");
    }

    [Fact]
    public void Parse_TwoNumericColumns_FailsWithAReason()
    {
        Parse("1,2\n3,4").FailureReason.Should().StartWith("Could not find three numeric columns");
    }

    private static TableParseResult Parse(string csv) =>
        TrajectoryTableParser.Parse(CsvTableReader.Read(new StringReader(csv), "t.csv"));
}
