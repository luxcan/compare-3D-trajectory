using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.Core.Tests.Loading;

public class CsvTableReaderTests
{
    [Theory]
    [InlineData("1,2,3\n4,5,6", ',')]
    [InlineData("1;2;3\n4;5;6", ';')]
    [InlineData("1\t2\t3\n4\t5\t6", '\t')]
    [InlineData("1|2|3\n4|5|6", '|')]
    public void Read_DelimitedLines_DetectsTheDelimiter(string content, char expected)
    {
        RawTable table = CsvTableReader.Read(new StringReader(content), "t.csv");

        CsvTableReader.DetectDelimiter(content.Split('\n')).Should().Be(expected);
        table.Rows[1].Cells.Should().Equal("4", "5", "6");
    }

    [Fact]
    public void Read_SpaceSeparatedColumns_SplitsOnRunsOfWhitespace()
    {
        RawTable table = CsvTableReader.Read(new StringReader("  1.5   2.5  3.5\n4 5 6"), "t.txt");

        table.Rows[0].Cells.Should().Equal("1.5", "2.5", "3.5");
    }

    [Fact]
    public void Read_QuotedCells_KeepDelimitersAndDoubledQuotesInside()
    {
        RawTable table = CsvTableReader.Read(new StringReader("\"a, \"\"b\"\"\",\"2\",3\n1,2,3"), "t.csv");

        table.Rows[0].Cells.Should().Equal("a, \"b\"", "2", "3");
    }

    [Fact]
    public void Read_BlankLines_AreDroppedButLineNumbersStayTrue()
    {
        RawTable table = CsvTableReader.Read(new StringReader("x,y,z\n\n1,2,3\n   \n4,5,6"), "t.csv");

        table.Rows.Select(row => row.LineNumber).Should().Equal(1, 3, 5);
    }

    /// <summary>F3: a quoted name with a space in it used to split, moving every later column along.</summary>
    [Fact]
    public void Read_SpaceSeparatedQuotedCell_KeepsTheSpaceInside()
    {
        RawTable table = CsvTableReader.Read(new StringReader("Name Time X Y Z\n\"Tank 1\" 0.5 10 20 30"), "t.txt");

        table.Rows[1].Cells.Should().Equal("Tank 1", "0.5", "10", "20", "30");
    }

    /// <summary>F7: a line without the delimiter, above the header, used to veto it.</summary>
    [Theory]
    [InlineData("# exported by logger", ',')]
    [InlineData("Exercise 12, run 3", ';')]
    public void DetectDelimiter_TitleLineWithADifferentCount_DoesNotDecide(string title, char expected)
    {
        string header = string.Join(expected, "Time", "X", "Y", "Z");
        string row = string.Join(expected, "0", "1", "2", "3");

        CsvTableReader.DetectDelimiter([title, header, row, row]).Should().Be(expected);
        CsvTableReader.Read(new StringReader($"{title}\n{header}\n{row}"), "t.csv").Delimiter.Should().Be(expected);
    }

    [Fact]
    public void DetectDelimiter_DecimalCommasUnderATabbedHeader_PicksTheTab()
    {
        CsvTableReader.DetectDelimiter(["x\ty\tz", "1,5\t2,5\t3,5", "4,5\t5,5\t6,5"]).Should().Be('\t');
    }

    /// <summary>F76: a quoted cell holding a line break is one cell, and the lines after it keep their numbers.</summary>
    [Fact]
    public void Read_QuotedCellOverTwoLines_IsOneCellAndLaterLinesKeepTheirNumbers()
    {
        RawTable table = CsvTableReader.Read(
            new StringReader("Time,x,y,z,Comment\n0,1,2,3,ok\n1,4,5,6,\"turn\nstarts here\"\n2,7,8,9,ok"),
            "t.csv");

        table.Rows.Select(row => row.LineNumber).Should().Equal(1, 2, 3, 5);
        table.Rows[2].Cells[4].Should().Be("turn\nstarts here");
        table.Delimiter.Should().Be(',');
    }

    [Fact]
    public void Read_QuoteThatNeverCloses_LeavesTheLinesAfterItAlone()
    {
        RawTable table = CsvTableReader.Read(new StringReader("Pipe,x,y,z\n12\" pipe,1,2,3\nB,4,5,6\nC,7,8,9"), "t.csv");

        table.Rows.Select(row => row.LineNumber).Should().Equal(1, 2, 3, 4);
        table.Rows[3].Cells.Should().Equal("C", "7", "8", "9");
    }
}
