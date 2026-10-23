using System.Text;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Reads delimited text into rows of cells, working out the delimiter from the file itself.
/// </summary>
public static class CsvTableReader
{
    private const int DelimiterSampleLines = 20;

    /// <summary>
    /// The most lines one quoted cell may span. A quote that is still open after this many is taken
    /// for a stray one, such as an inch mark, and the lines are read one by one after all.
    /// </summary>
    private const int MaximumLinesInOneCell = 10;

    private static readonly char[] CandidateDelimiters = [',', ';', '\t', '|'];

    /// <summary>
    /// Reads a file. It is opened for shared reading, so a CSV that is still open in Excel can be read.
    /// </summary>
    public static RawTable ReadFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return Read(reader, Path.GetFileName(path));
    }

    public static RawTable Read(TextReader reader, string name)
    {
        ArgumentNullException.ThrowIfNull(reader);

        List<(int LineNumber, string Text)> records = ReadRecords(reader);
        char? delimiter = DetectDelimiter(records.Take(DelimiterSampleLines).Select(record => record.Text).ToList());
        var rows = records
            .Select(record => new TableRow(record.LineNumber, Split(record.Text, delimiter)))
            .ToList();

        return new RawTable(name, rows) { Delimiter = delimiter };
    }

    /// <summary>
    /// The non-blank records of the text, each with the line it starts on. A record is one line,
    /// unless a quoted cell holds a line break, when it runs on to the line that closes the quote.
    /// </summary>
    private static List<(int LineNumber, string Text)> ReadRecords(TextReader reader)
    {
        var lines = new List<string>();
        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        var records = new List<(int LineNumber, string Text)>();
        for (int first = 0; first < lines.Count; first++)
        {
            if (string.IsNullOrWhiteSpace(lines[first]))
            {
                continue;
            }

            int last = HasOddQuotes(lines[first]) ? ClosingLine(lines, first) : first;
            records.Add((first + 1, string.Join('\n', lines.GetRange(first, last - first + 1))));
            first = last;
        }

        return records;
    }

    /// <summary>
    /// The line that closes the quote left open on <paramref name="first"/>; <paramref name="first"/>
    /// itself when no line soon enough does, so a stray quote costs one line and no more.
    /// </summary>
    private static int ClosingLine(List<string> lines, int first)
    {
        for (int line = first + 1; line < lines.Count && line < first + MaximumLinesInOneCell; line++)
        {
            if (HasOddQuotes(lines[line]))
            {
                return line;
            }
        }

        return first;
    }

    private static bool HasOddQuotes(string text) => text.Count(c => c == '"') % 2 == 1;

    /// <summary>
    /// The candidate that splits the most lines into the same number of cells. Lines that disagree,
    /// such as a title or comment above the header, are outvoted rather than allowed to veto. None of
    /// them means the columns are separated by spaces, which is returned as null.
    /// </summary>
    internal static char? DetectDelimiter(IReadOnlyList<string> sampleLines)
    {
        char? best = null;
        (int Agreeing, int Count) bestVote = (0, 0);
        foreach (char candidate in CandidateDelimiters)
        {
            (int Agreeing, int Count) vote = Vote(sampleLines, candidate);
            if (vote.Count > 0 && 2 * vote.Agreeing >= sampleLines.Count && vote.CompareTo(bestVote) > 0)
            {
                best = candidate;
                bestVote = vote;
            }
        }

        return best;
    }

    /// <summary>
    /// The number of times the candidate appears on the most lines, and how many lines that is; of
    /// two numbers on as many lines, the larger.
    /// </summary>
    private static (int Agreeing, int Count) Vote(IReadOnlyList<string> lines, char candidate) =>
        lines
            .GroupBy(line => Split(line, candidate).Count - 1)
            .Select(group => (Agreeing: group.Count(), Count: group.Key))
            .DefaultIfEmpty()
            .Max();

    /// <summary>
    /// Splits one line on the delimiter, honouring double quotes ("a,b" is one cell, "" is a quote
    /// inside one). A null delimiter splits on runs of whitespace, where quotes still hold a cell
    /// together, so "Tank 1" stays one name.
    /// </summary>
    internal static IReadOnlyList<string> Split(string line, char? delimiter)
    {
        var cells = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;
        bool inCell = false;
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                inCell = true;
            }
            else if (!inQuotes && (delimiter is null ? char.IsWhiteSpace(c) : c == delimiter))
            {
                if (delimiter is not null || inCell)
                {
                    cells.Add(cell.ToString().Trim());
                }

                cell.Clear();
                inCell = false;
            }
            else
            {
                cell.Append(c);
                inCell = true;
            }
        }

        if (delimiter is not null || inCell)
        {
            cells.Add(cell.ToString().Trim());
        }

        return cells;
    }
}
