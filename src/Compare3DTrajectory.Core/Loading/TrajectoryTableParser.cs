using System.Globalization;
using System.Text.RegularExpressions;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Turns a table of cells into x, y, z points: finds a header if there is one, picks the three
/// columns, and reports every row it could not use rather than dropping it quietly.
/// </summary>
public static partial class TrajectoryTableParser
{
    /// <summary>Words in a header that say a column is a position, which beats one that is not.</summary>
    private static readonly HashSet<string> PositionWords =
        ["location", "loc", "position", "pos", "world", "ecef", "coord", "coords", "coordinate", "coordinates"];

    /// <summary>Words that say an x/y/z column is something other than a position.</summary>
    private static readonly HashSet<string> OtherQuantityWords =
        ["velocity", "vel", "speed", "acceleration", "accel", "acc", "orientation", "rotation", "rot", "angle", "heading", "rate"];

    public static TableParseResult Parse(RawTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Rows.Count == 0)
        {
            return Failed("There are no rows.");
        }

        TableRow first = table.Rows[0];
        bool hasHeader = NumericColumns(first).Count < 3;
        IReadOnlyList<TableRow> dataRows = hasHeader ? table.Rows.Skip(1).ToList() : table.Rows;
        if (dataRows.Count == 0)
        {
            return Failed("There is a header row but no data under it.");
        }

        string? note = null;
        ColumnChoice? choice = hasHeader ? ChooseByName(first.Cells) : null;
        if (choice is null)
        {
            List<int> numeric = NumericColumns(dataRows[0]);
            if (numeric.Count < 3)
            {
                return Failed(
                    $"Could not find three numeric columns for x, y and z (line {dataRows[0].LineNumber} has {numeric.Count}).");
            }

            choice = new ColumnChoice(numeric[0], numeric[1], numeric[2]);
            string names = hasHeader ? DescribeByHeader(first.Cells, choice) : DescribeByPosition(choice);
            if (numeric.Count > 3)
            {
                note = $"{numeric.Count} numeric columns found; the first three were used as x, y, z.";
            }

            choice = choice with { Description = names };
        }

        return ReadPoints(dataRows, choice, note);
    }

    private static TableParseResult ReadPoints(IReadOnlyList<TableRow> rows, ColumnChoice choice, string? note)
    {
        var points = new List<Vec3>(rows.Count);
        var lineNumbers = new List<int>(rows.Count);
        var skipped = new List<SkippedRow>();

        foreach (TableRow row in rows)
        {
            string? problem = TryRead(row, choice, out Vec3 point);
            if (problem is null)
            {
                points.Add(point);
                lineNumbers.Add(row.LineNumber);
            }
            else
            {
                skipped.Add(new SkippedRow(row.LineNumber, problem));
            }
        }

        string? failure = points.Count < 2
            ? $"Only {points.Count} readable point{(points.Count == 1 ? string.Empty : "s")}; a trajectory needs at least two."
            : null;

        return new TableParseResult(points, lineNumbers, skipped, choice.Description, note, failure);
    }

    private static string? TryRead(TableRow row, ColumnChoice choice, out Vec3 point)
    {
        point = default;
        double[] values = new double[3];
        int[] columns = [choice.X, choice.Y, choice.Z];
        for (int axis = 0; axis < 3; axis++)
        {
            int column = columns[axis];
            if (column >= row.Cells.Count || row.Cells[column].Length == 0)
            {
                return $"no value in column {column + 1}";
            }

            string cell = row.Cells[column];
            if (!TryParseNumber(cell, out values[axis]))
            {
                return $"'{Shorten(cell)}' in column {column + 1} is not a number";
            }
        }

        point = new Vec3(values[0], values[1], values[2]);
        return null;
    }

    private static bool TryParseNumber(string cell, out double value) =>
        double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    private static List<int> NumericColumns(TableRow row)
    {
        var columns = new List<int>();
        for (int i = 0; i < row.Cells.Count; i++)
        {
            if (TryParseNumber(row.Cells[i], out _))
            {
                columns.Add(i);
            }
        }

        return columns;
    }

    /// <summary>
    /// Finds x, y and z among the header names. A header names an axis when one of its words is
    /// exactly that letter: <c>x</c>, <c>pos_x</c>, <c>WorldLocation.X</c>, <c>World Location X</c>,
    /// <c>PositionX</c> and <c>X (m)</c> all do. When several columns name the same axis, a position
    /// word wins over none, and none wins over a velocity or angle word; ties go to the leftmost.
    /// </summary>
    internal static ColumnChoice? ChooseByName(IReadOnlyList<string> header)
    {
        int[] chosen = [-1, -1, -1];
        int[] scores = [int.MinValue, int.MinValue, int.MinValue];
        string[] axes = ["x", "y", "z"];

        for (int column = 0; column < header.Count; column++)
        {
            string[] words = [.. HeaderWord().Matches(header[column]).Select(match => match.Value.ToLowerInvariant())];
            int score = words.Count(PositionWords.Contains) - words.Count(OtherQuantityWords.Contains);
            for (int axis = 0; axis < 3; axis++)
            {
                if (words.Contains(axes[axis]) && score > scores[axis])
                {
                    chosen[axis] = column;
                    scores[axis] = score;
                }
            }
        }

        if (chosen.Any(column => column < 0) || chosen.Distinct().Count() < 3)
        {
            return null;
        }

        var choice = new ColumnChoice(chosen[0], chosen[1], chosen[2]);
        return choice with { Description = DescribeByHeader(header, choice) };
    }

    private static string DescribeByHeader(IReadOnlyList<string> header, ColumnChoice choice) =>
        $"x, y, z from columns '{header[choice.X]}', '{header[choice.Y]}', '{header[choice.Z]}'";

    private static string DescribeByPosition(ColumnChoice choice) =>
        $"x, y, z from columns {choice.X + 1}, {choice.Y + 1}, {choice.Z + 1}";

    private static string Shorten(string cell) => cell.Length <= 24 ? cell : cell[..21] + "...";

    private static TableParseResult Failed(string reason) =>
        new([], [], [], string.Empty, null, reason);

    /// <summary>Words of a header: runs of letters split at camel-case humps, or runs of digits.</summary>
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+")]
    private static partial Regex HeaderWord();

    internal sealed record ColumnChoice(int X, int Y, int Z)
    {
        public string Description { get; init; } = string.Empty;
    }
}
