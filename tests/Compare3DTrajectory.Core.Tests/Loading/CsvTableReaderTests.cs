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
}
