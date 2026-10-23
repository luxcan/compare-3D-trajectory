using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core.Tests.Loading;

public class TrajectoryTableParserTests
{
    /// <summary>The user's own wording, which every refusal for a missing header opens with.</summary>
    private const string Request = "Please add in the header for x, y, z.";

    [Fact]
    public void Parse_HeaderXyz_ReadsPointsAndNamesTheColumns()
    {
        TableParseResult result = Parse("x,y,z\n1,2,3\n4,5,6");

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6));
        result.LineNumbers.Should().Equal(2, 3);
        result.ColumnDescription.Should().Be("x, y, z from columns 'x', 'y', 'z'");
        result.Note.Should().BeNull();
        result.FailureReason.Should().BeNull();
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
    public void Parse_NoHeader_IsRefusedWithTheRequestTheFileTheColumnsAndAnExample()
    {
        TableParseResult result = Parse("1.5,-2e3,3\n4,5,6");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().Be(Lines(
            Request,
            "File: t.csv",
            "Columns found: none; line 1 already holds numbers (1.5, -2e3, 3)",
            "Example first line: x,y,z"));
        result.MissingHeader.Should().NotBeNull();
    }

    [Theory]
    [InlineData("1;2;3\n4;5;6", "Example first line: x;y;z")]
    [InlineData("1\t2\t3\n4\t5\t6", "Example first line: x, y, z separated by tabs")]
    [InlineData("1 2 3\n4 5 6", "Example first line: x y z")]
    public void Parse_NoHeader_GivesTheExampleInTheFilesOwnLayout(string content, string example)
    {
        Parse(content).FailureReason.Should().EndWith(example);
    }

    /// <summary>F1: the old fallback described the columns by a header row that was too short, and threw.</summary>
    [Theory]
    [InlineData("Recorded trajectory\n1 2 3\n4 5 6")]
    [InlineData("Trajectory export\n1 2 3\n4 5 6")]
    [InlineData("Run 3, Singapore\n1,2,3\n4,5,6")]
    public void Parse_TitleLineOverHeaderlessData_IsRefusedRatherThanThrowing(string content)
    {
        Parse(content).FailureReason.Should().StartWith(Request);
    }

    /// <summary>F4: pandas writes its default column labels 0, 1, 2 as the header.</summary>
    [Fact]
    public void Parse_HeaderOfNumericLabels_IsRefusedRatherThanReadAsAPoint()
    {
        TableParseResult result = Parse(",0,1,2\n0,-1514567.1,6191234.9,149876.5\n1,-1514568.1,6191233.9,149877.5");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().StartWith(Request);
    }

    /// <summary>F5: a column the reader itself takes for a velocity is never a position.</summary>
    [Fact]
    public void Parse_OnlyVelocityColumnsNameTheAxes_IsRefused()
    {
        TableParseResult result = Parse("t,VelX,VelY,VelZ\n0,1,2,3\n1,4,5,6");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().StartWith(Request)
            .And.Contain("No column is named x, y or z as a position; 'VelX', 'VelY', 'VelZ' are read as velocity or angle.");
    }

    /// <summary>F6, F13: a header that names some or none of the axes no longer hands over to column guessing.</summary>
    [Theory]
    [InlineData("Time,East,North,Up", "Columns found: 'Time', 'East', 'North', 'Up'", "No column is named x, y or z.")]
    [InlineData("Latitude,Longitude,Altitude", "Columns found: 'Latitude', 'Longitude', 'Altitude'", "No column is named x, y or z.")]
    [InlineData("Time,X,Y,Alt", "Columns found: 'Time', 'X', 'Y', 'Alt'", "No column is named z.")]
    [InlineData("Time,X,Altitude,Heading", "Columns found: 'Time', 'X', 'Altitude', 'Heading'", "No column is named y or z.")]
    public void Parse_HeaderThatDoesNotNameEveryAxis_IsRefusedAndListsTheColumns(string header, string found, string problem)
    {
        int columns = header.Split(',').Length;
        string row = string.Join(',', Enumerable.Range(1, columns));

        TableParseResult result = Parse($"{header}\n{row}\n{row}");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().Be(Lines(Request, "File: t.csv", found, problem, "Example first line: x,y,z"));
    }

    /// <summary>F7, F75: a comment or title above the header used to make the whole file fail.</summary>
    [Theory]
    [InlineData("# exported by logger")]
    [InlineData("Exercise 12, run 3")]
    [InlineData("Exercise 12; run 3")]
    public void Parse_CommentOrTitleLineAboveTheHeader_ReadsTheRowsUnderTheHeader(string title)
    {
        TableParseResult result = Parse($"{title}\nTime,X,Y,Z\n0,1,2,3\n1,4,5,6");

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6));
        result.LineNumbers.Should().Equal(3, 4);
        result.Note.Should().Be("Line 1, above the header, was not read.");
    }

    [Fact]
    public void Parse_TitleThatNamesTheAxesAboveTheRealHeader_ReadsTheRealHeader()
    {
        TableParseResult result = Parse("x y z trajectory\nTime,X,Y,Z\n0,1,2,3\n1,4,5,6");

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6));
    }

    /// <summary>F8: the incomplete first row of a headerless file used to vanish as a "header".</summary>
    [Theory]
    [InlineData("0,NaN,NaN,NaN\n1,1,2,3\n2,4,5,6")]
    [InlineData("1,2,\n4,5,6\n7,8,9")]
    [InlineData("1,,3\n4,5,6\n7,8,9")]
    [InlineData("0.000,,,\n0.1,10,20,30\n0.2,11,21,31")]
    public void Parse_IncompleteFirstRowOfAHeaderlessFile_IsRefused(string content)
    {
        TableParseResult result = Parse(content);

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().StartWith(Request);
    }

    /// <summary>F10: a German Excel CSV writes 1.234,5 for one thousand two hundred and thirty-four and a half.</summary>
    [Fact]
    public void Parse_SemicolonFileWithDecimalCommas_ReadsDotsAsThousandsSeparators()
    {
        TableParseResult result = Parse("x;y;z\n1.234;2.345;100\n1.250;2.360;101\n1.234,5;2.345;100");

        result.Points.Should().Equal(new Vec3(1234, 2345, 100), new Vec3(1250, 2360, 101), new Vec3(1234.5, 2345, 100));
        result.SkippedRows.Should().BeEmpty();
        result.Note.Should().Be("Numbers are written with a decimal comma.");
    }

    [Fact]
    public void Parse_SemicolonFileWithDecimalPoints_ReadsDotsAsDecimals()
    {
        TableParseResult result = Parse("Time;X;Y;Z\n0;1.5;2.5;3.5\n1;4.5;5.5;6.5");

        result.Points.Should().Equal(new Vec3(1.5, 2.5, 3.5), new Vec3(4.5, 5.5, 6.5));
        result.Note.Should().BeNull();
    }

    [Fact]
    public void Parse_DecimalCommaFileWithADecimalPoint_SkipsThatRowAndSaysWhy()
    {
        TableParseResult result = Parse("x;y;z\n1,5;2,5;3\n1.5;2,5;3\n4,5;5,5;6");

        result.Points.Should().Equal(new Vec3(1.5, 2.5, 3), new Vec3(4.5, 5.5, 6));
        result.SkippedRows.Should().Equal(new SkippedRow(3, "'1.5' in column 1 is not a number with a decimal comma"));
    }

    /// <summary>F14: two entities' rows, taken as one path, zig-zag between them and hide real errors.</summary>
    [Fact]
    public void Parse_InterleavedEntities_IsRefusedNamingThem()
    {
        TableParseResult result = Parse("Time,Entity,X,Y,Z\n0,A,0,0,0\n0,B,0,-400,0\n1,A,10,0,0\n1,B,10,-400,0\n2,A,20,0,0\n2,B,20,-400,0");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().Be(
            "Column 'Entity' holds 2 entities (A, B). One path through all their points would zig-zag between them; "
            + "save each entity to a file of its own.");
    }

    [Theory]
    [InlineData("Time,Entity,X,Y,Z\n0,A,0,0,0\n1,A,10,0,0\n2,A,20,0,0\n3,A,30,0,0")]
    [InlineData("ID,x,y,z\n1,0,0,0\n2,10,0,0\n3,20,0,0\n4,30,0,0")]
    public void Parse_OneEntityOrARowNumber_IsIgnored(string content)
    {
        Parse(content).Points.Should().HaveCount(4);
    }

    /// <summary>F20: two entities side by side used to give the leftmost without a word.</summary>
    [Fact]
    public void Parse_TwoColumnsEquallyGoodForAnAxis_IsRefusedNamingBoth()
    {
        TableParseResult result = Parse("Entity1 X,Entity1 Y,Entity1 Z,Entity2 X,Entity2 Y,Entity2 Z\n1,2,3,4,5,6\n1,2,3,4,5,6");

        result.Points.Should().BeEmpty();
        result.FailureReason.Should().StartWith(Request)
            .And.Contain("Two columns could be x: 'Entity1 X' and 'Entity2 X'; name only one of them x.");
    }

    /// <summary>F45: angle and velocity spellings the word list missed tied with plain X and won as the leftmost.</summary>
    [Theory]
    [InlineData("EulerX,EulerY,EulerZ,X,Y,Z", "0.1,0.2,0.3,7,8,9")]
    [InlineData("t,v_x,v_y,v_z,x,y,z", "0,0.1,0.2,0.3,7,8,9")]
    [InlineData("x,y,z,x_sigma,y_sigma,z_sigma", "7,8,9,0.1,0.2,0.3")]
    public void Parse_AnglesVelocitiesAndErrorsBesideThePosition_AreNotTakenForIt(string header, string row)
    {
        Parse($"{header}\n{row}\n{row}").Points[0].Should().Be(new Vec3(7, 8, 9));
    }

    /// <summary>F2: a row with a delimiter inside a value has its x, y, z one column further on.</summary>
    [Theory]
    [InlineData("Name,Time,X,Y,Z\nTank 1,0.5,10,20,30\nTank 1, Alpha,0.6,11,21,31\nTank 1,0.7,12,22,32")]
    [InlineData("Time Marking X Y Z\n0.5 Alpha 10 20 30\n0.6 Tank 1 11 21 31\n0.7 Alpha 12 22 32")]
    public void Parse_RowWithMoreCellsThanTheHeader_IsSkippedRatherThanReadShifted(string content)
    {
        TableParseResult result = Parse(content);

        result.Points.Should().Equal(new Vec3(10, 20, 30), new Vec3(12, 22, 32));
        result.SkippedRows.Should().Equal(new SkippedRow(3, "has 6 cells but the header has 5"));
    }

    /// <summary>F2: R's write.table leaves the row-name column out of the header.</summary>
    [Fact]
    public void Parse_HeaderShorterThanEveryRow_FailsSayingWhy()
    {
        TableParseResult result = Parse("\"x\",\"y\",\"z\"\n\"1\",10,20,30\n\"2\",11,21,31");

        result.FailureReason.Should().Be(
            "Only 0 readable points; a trajectory needs at least two. Line 2: has 4 cells but the header has 3.");
    }

    [Fact]
    public void Parse_TrailingDelimiters_AreNotExtraCells()
    {
        Parse("x,y,z,\n1,2,3,\n4,5,6,").Points.Should().HaveCount(2);
    }

    /// <summary>F76: a quoted cell may hold a line break, in a comment or in a wrapped header name.</summary>
    [Theory]
    [InlineData("Time,x,y,z,Comment\n0,1,2,3,ok\n1,4,5,6,\"turn\nstarts here\"\n2,7,8,9,ok", 2, 3, 5)]
    [InlineData("\"Position X\n(m)\",\"Position Y\n(m)\",\"Position Z\n(m)\"\n1,2,3\n4,5,6\n7,8,9", 5, 6, 7)]
    public void Parse_QuotedCellOverTwoLines_IsOneCell(string content, params int[] lines)
    {
        TableParseResult result = Parse(content);

        result.Points.Should().Equal(new Vec3(1, 2, 3), new Vec3(4, 5, 6), new Vec3(7, 8, 9));
        result.LineNumbers.Should().Equal(lines);
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
    public void Parse_OnlyOneReadablePoint_FailsGivingTheFirstBadRow()
    {
        TableParseResult result = Parse("x,y,z\n1,2,3\nfoo,bar,baz");

        result.FailureReason.Should().Be(
            "Only 1 readable point; a trajectory needs at least two. Line 3: 'foo' in column 1 is not a number.");
    }

    [Fact]
    public void Parse_HeaderOnly_FailsWithAReason()
    {
        Parse("x,y,z").FailureReason.Should().Be("There is a header row but no data under it.");
    }

    /// <summary>A table without three numbers on any row is not trying to be a trajectory, so it is not asked for a header.</summary>
    [Theory]
    [InlineData("1,2\n3,4")]
    [InlineData("just some text")]
    public void Parse_NoRowWithThreeNumbers_FailsWithoutAskingForAHeader(string content)
    {
        TableParseResult result = Parse(content);

        result.FailureReason.Should().Be("Could not find three numeric columns for x, y and z.");
        result.MissingHeader.Should().BeNull();
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines);

    private static TableParseResult Parse(string csv) =>
        TrajectoryTableParser.Parse(CsvTableReader.Read(new StringReader(csv), "t.csv"));
}
