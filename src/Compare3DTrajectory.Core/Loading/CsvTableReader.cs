using System.Text;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Reads delimited text into rows of cells, working out the delimiter from the file itself.
/// </summary>
public static class CsvTableReader
{
    private const int DelimiterSampleLines = 20;

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

        var lines = new List<(int LineNumber, string Text)>();
        int lineNumber = 0;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (!string.IsNullOrWhiteSpace(line))
            {
                lines.Add((lineNumber, line));
            }
        }

        char? delimiter = DetectDelimiter(lines.Take(DelimiterSampleLines).Select(line => line.Text).ToList());
        var rows = lines
            .Select(line => new TableRow(line.LineNumber, Split(line.Text, delimiter)))
            .ToList();

        return new RawTable(name, rows);
    }

    /// <summary>
    /// The candidate that appears on every sampled line, the most times on its sparsest line. None of
    /// them means the columns are separated by spaces, which is returned as null.
    /// </summary>
    internal static char? DetectDelimiter(IReadOnlyList<string> sampleLines)
    {
        char? best = null;
        int bestMinimum = 0;
        foreach (char candidate in CandidateDelimiters)
        {
            int minimum = sampleLines.Count == 0
                ? 0
                : sampleLines.Min(line => Split(line, candidate).Count - 1);
            if (minimum > bestMinimum)
            {
                best = candidate;
                bestMinimum = minimum;
            }
        }

        return best;
    }

    /// <summary>
    /// Splits one line on the delimiter, honouring double quotes ("a,b" is one cell, "" is a quote
    /// inside one). A null delimiter splits on runs of whitespace.
    /// </summary>
    internal static IReadOnlyList<string> Split(string line, char? delimiter)
    {
        if (delimiter is null)
        {
            return line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        }

        var cells = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;
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
            }
            else if (c == delimiter && !inQuotes)
            {
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        cells.Add(cell.ToString().Trim());
        return cells;
    }
}
