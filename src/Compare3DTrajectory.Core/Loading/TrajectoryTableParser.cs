using System.Globalization;
using System.Text.RegularExpressions;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Turns a table of cells into x, y, z points. A header row must name x, y and z, each by exactly one
/// column; a table of numbers without one is refused with a <see cref="MissingHeader"/>, never
/// guessed at. Every row that cannot be used is reported rather than dropped quietly.
/// </summary>
public static partial class TrajectoryTableParser
{
    /// <summary>How many rows down the header is looked for, past any title or comment above it.</summary>
    private const int HeaderSearchRows = 20;

    /// <summary>How many columns, or numbers, a refusal quotes before it says how many there are.</summary>
    private const int ColumnsQuoted = 12;

    private const int NumbersQuoted = 4;

    private const int EntitiesQuoted = 5;

    private static readonly string[] Axes = ["x", "y", "z"];

    /// <summary>Words in a header that say a column is a position, which beats one that is not.</summary>
    private static readonly HashSet<string> PositionWords =
        ["location", "loc", "position", "pos", "world", "ecef", "coord", "coords", "coordinate", "coordinates"];

    /// <summary>
    /// Words that say an x/y/z column is something other than a position: a rate, an angle, a force or
    /// an error estimate. Such a column is never read as the position.
    /// </summary>
    private static readonly HashSet<string> OtherQuantityWords =
    [
        "velocity", "vel", "v", "speed", "acceleration", "accel", "acc", "rate",
        "orientation", "rotation", "rot", "angle", "ang", "angular", "heading", "euler", "attitude",
        "yaw", "pitch", "roll", "psi", "theta", "phi", "omega", "gyro", "quat", "quaternion", "q",
        "force", "torque", "moment", "dir", "direction", "delta", "offset",
        "err", "error", "sigma", "std", "stddev", "var", "variance", "cov", "covariance", "uncertainty", "accuracy",
    ];

    /// <summary>Words that say a column tells one entity's rows from another's.</summary>
    private static readonly HashSet<string> EntityWords = ["entity", "marking", "callsign", "object", "vehicle", "platform"];

    public static TableParseResult Parse(RawTable table)
    {
        ArgumentNullException.ThrowIfNull(table);

        if (table.Rows.Count == 0)
        {
            return Failed("There are no rows.");
        }

        // The first row that reads as a header wins. A row that names every axis but cannot be used,
        // such as a title "x y z trajectory", is the header only when no row below it does better.
        int searched = Math.Min(HeaderSearchRows, table.Rows.Count);
        HeaderReading[] readings = [.. table.Rows.Take(searched).Select(row => ReadHeader(row.Cells))];
        int headerIndex = Array.FindIndex(readings, reading => reading.Choice is not null);
        if (headerIndex < 0)
        {
            headerIndex = Array.FindIndex(readings, reading => reading.NamesEveryAxis);
        }

        if (headerIndex < 0)
        {
            return RefuseWithoutHeader(table, searched);
        }

        TableRow headerRow = table.Rows[headerIndex];
        HeaderReading header = readings[headerIndex];
        if (header.Problem is not null)
        {
            return Refused(table, MissingHeaderFor(table, headerRow, header.Problem));
        }

        IReadOnlyList<TableRow> dataRows = [.. table.Rows.Skip(headerIndex + 1)];
        if (dataRows.Count == 0)
        {
            return Failed("There is a header row but no data under it.");
        }

        ColumnChoice choice = header.Choice!;
        if (MixedEntities(headerRow.Cells, choice, dataRows) is { } entities)
        {
            return Failed(entities);
        }

        bool decimalComma = UsesDecimalComma(table, choice, dataRows);
        var notes = new List<string>();
        if (headerIndex > 0)
        {
            notes.Add(DescribeRowsAbove(table, headerIndex));
        }

        if (decimalComma)
        {
            notes.Add("Numbers are written with a decimal comma.");
        }

        int headerWidth = table.IsWorksheet ? int.MaxValue : UsedWidth(headerRow.Cells);
        return ReadPoints(dataRows, choice, headerWidth, decimalComma, notes.Count > 0 ? string.Join(' ', notes) : null);
    }

    private static TableParseResult ReadPoints(
        IReadOnlyList<TableRow> rows, ColumnChoice choice, int headerWidth, bool decimalComma, string? note)
    {
        var points = new List<Vec3>(rows.Count);
        var lineNumbers = new List<int>(rows.Count);
        var skipped = new List<SkippedRow>();

        foreach (TableRow row in rows)
        {
            string? problem = TryRead(row, choice, headerWidth, decimalComma, out Vec3 point);
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

        string? failure = null;
        if (points.Count < 2)
        {
            failure = $"Only {points.Count} readable point{(points.Count == 1 ? string.Empty : "s")}; a trajectory needs at least two.";
            if (skipped.Count > 0)
            {
                failure += $" Line {skipped[0].LineNumber}: {skipped[0].Reason}.";
            }
        }

        return new TableParseResult(points, lineNumbers, skipped, choice.Description, note, failure);
    }

    /// <summary>
    /// Reads one row's x, y, z. In delimited text a row with more cells than the header has a
    /// delimiter inside one of its values, so its x, y, z would be read from the wrong columns.
    /// </summary>
    private static string? TryRead(TableRow row, ColumnChoice choice, int headerWidth, bool decimalComma, out Vec3 point)
    {
        point = default;
        int width = UsedWidth(row.Cells);
        if (width > headerWidth)
        {
            return $"has {width} cells but the header has {headerWidth}";
        }

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
            if (decimalComma)
            {
                if (!TryParseDecimalComma(cell, out values[axis]))
                {
                    return $"'{Shorten(cell)}' in column {column + 1} is not a number with a decimal comma";
                }
            }
            else if (!TryParseNumber(cell, out values[axis]))
            {
                return $"'{Shorten(cell)}' in column {column + 1} is not a number";
            }
        }

        point = new Vec3(values[0], values[1], values[2]);
        return null;
    }

    private static bool TryParseNumber(string cell, out double value) =>
        double.TryParse(cell, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value);

    /// <summary>A number such as 1.234,5: a comma before the decimals, and dots only between groups of three digits.</summary>
    private static bool TryParseDecimalComma(string cell, out double value)
    {
        value = 0;
        return DecimalCommaNumber().IsMatch(cell)
            && TryParseNumber(cell.Replace(".", string.Empty, StringComparison.Ordinal).Replace(',', '.'), out value);
    }

    private static bool IsNumber(string cell) => TryParseNumber(cell, out _) || TryParseDecimalComma(cell, out _);

    /// <summary>
    /// Whether the x, y, z cells of delimited text use a decimal comma, as a semicolon-separated export
    /// from a German or French Excel does. A comma-separated file cannot, so it is never asked.
    /// </summary>
    private static bool UsesDecimalComma(RawTable table, ColumnChoice choice, IReadOnlyList<TableRow> rows) =>
        !table.IsWorksheet
        && table.Delimiter != ','
        && rows.Any(row => new[] { choice.X, choice.Y, choice.Z }.Any(column =>
            column < row.Cells.Count
            && row.Cells[column].Contains(',', StringComparison.Ordinal)
            && DecimalCommaNumber().IsMatch(row.Cells[column])));

    /// <summary>The number of cells up to the last one that holds anything, so trailing delimiters add none.</summary>
    private static int UsedWidth(IReadOnlyList<string> cells)
    {
        int width = cells.Count;
        while (width > 0 && cells[width - 1].Length == 0)
        {
            width--;
        }

        return width;
    }

    /// <summary>
    /// Reads one row as a header. A header names an axis when one of its words is exactly that letter:
    /// <c>x</c>, <c>pos_x</c>, <c>WorldLocation.X</c>, <c>World Location X</c>, <c>PositionX</c> and
    /// <c>X (m)</c> all do. A position word beats none, and a column with more velocity or angle words
    /// than position words is never a position. Two columns equally good for one axis are a problem
    /// for the user to settle, not a guess for the reader to make.
    /// </summary>
    private static HeaderReading ReadHeader(IReadOnlyList<string> header)
    {
        var candidates = new List<(int Column, int Score)>[] { [], [], [] };
        var notPositions = new List<int>[] { [], [], [] };
        for (int column = 0; column < header.Count; column++)
        {
            string[] words = HeaderWords(header[column]);
            int score = words.Count(PositionWords.Contains) - words.Count(OtherQuantityWords.Contains);
            for (int axis = 0; axis < 3; axis++)
            {
                if (!words.Contains(Axes[axis]))
                {
                    continue;
                }

                if (score >= 0)
                {
                    candidates[axis].Add((column, score));
                }
                else
                {
                    notPositions[axis].Add(column);
                }
            }
        }

        if (candidates.Any(list => list.Count == 0))
        {
            return new HeaderReading(null, null, NamesEveryAxis: false, candidates, notPositions);
        }

        var problems = new List<string>();
        int[] chosen = new int[3];
        for (int axis = 0; axis < 3; axis++)
        {
            int best = candidates[axis].Max(candidate => candidate.Score);
            List<int> tied = [.. candidates[axis].Where(candidate => candidate.Score == best).Select(candidate => candidate.Column)];
            chosen[axis] = tied[0];
            if (tied.Count > 1)
            {
                string count = tied.Count switch { 2 => "Two", 3 => "Three", _ => tied.Count.ToString(CultureInfo.InvariantCulture) };
                problems.Add($"{count} columns could be {Axes[axis]}: {QuoteAll(tied.Select(column => header[column]), "and")}; "
                    + $"name only one of them {Axes[axis]}.");
            }
        }

        foreach (int column in chosen.Distinct().Where(column => chosen.Count(other => other == column) > 1))
        {
            problems.Add($"'{header[column]}' names more than one of x, y and z.");
        }

        if (problems.Count > 0)
        {
            return new HeaderReading(null, string.Join(' ', problems), NamesEveryAxis: true, candidates, notPositions);
        }

        var choice = new ColumnChoice(chosen[0], chosen[1], chosen[2]);
        choice = choice with { Description = $"x, y, z from columns '{header[choice.X]}', '{header[choice.Y]}', '{header[choice.Z]}'" };
        return new HeaderReading(choice, null, NamesEveryAxis: true, candidates, notPositions);
    }

    /// <summary>
    /// No row near the top names x, y and z. A table with numbers in it is a trajectory without its
    /// header, and is refused saying so; one without is not a trajectory at all.
    /// </summary>
    private static TableParseResult RefuseWithoutHeader(RawTable table, int searched)
    {
        if (!table.Rows.Any(row => row.Cells.Count(IsNumber) >= 3))
        {
            return Failed("Could not find three numeric columns for x, y and z.");
        }

        TableRow shown = table.Rows.Take(searched).FirstOrDefault(row => ReadHeader(row.Cells).NamesAnyAxis) ?? table.Rows[0];
        string[] cells = [.. shown.Cells.Where(cell => cell.Length > 0)];
        if (cells.All(IsNumber))
        {
            return Refused(table, MissingHeaderFor(table, shown, problem: null));
        }

        HeaderReading reading = ReadHeader(shown.Cells);
        string[] missing = [.. Enumerable.Range(0, 3).Where(axis => reading.Candidates[axis].Count == 0).Select(axis => Axes[axis])];
        string[] notPositions = [.. Enumerable.Range(0, 3)
            .Where(axis => reading.Candidates[axis].Count == 0)
            .SelectMany(axis => reading.NotPositions[axis])
            .Distinct()
            .Order()
            .Select(column => shown.Cells[column])];
        string problem = notPositions.Length == 0
            ? $"No column is named {JoinAxes(missing)}."
            : $"No column is named {JoinAxes(missing)} as a position; {QuoteAll(notPositions, null)} "
              + $"{(notPositions.Length == 1 ? "is" : "are")} read as velocity or angle.";
        return Refused(table, MissingHeaderFor(table, shown, problem));
    }

    private static MissingHeader MissingHeaderFor(RawTable table, TableRow row, string? problem)
    {
        string[] cells = [.. row.Cells.Where(cell => cell.Length > 0)];
        string found = cells.All(IsNumber)
            ? $"none; {(table.IsWorksheet ? "row" : "line")} {row.LineNumber} already holds numbers "
              + $"({string.Join(", ", cells.Take(NumbersQuoted))}{(cells.Length > NumbersQuoted ? ", …" : string.Empty)})"
            : QuoteAll(cells.Take(ColumnsQuoted), null) + (cells.Length > ColumnsQuoted ? $", … ({cells.Length} in all)" : string.Empty);
        return new MissingHeader(found, problem, Example(table));
    }

    /// <summary>A header the user can copy, in the file's own layout so that it splits like the rows under it.</summary>
    private static string Example(RawTable table) => table.IsWorksheet
        ? "Example first row: x, y, z in cells A1, B1, C1"
        : table.Delimiter switch
        {
            '\t' => "Example first line: x, y, z separated by tabs",
            null => "Example first line: x y z",
            char delimiter => $"Example first line: x{delimiter}y{delimiter}z",
        };

    /// <summary>
    /// Several entities logged into one file, told apart by a column such as Entity or Marking. Drawn
    /// as one path, their points zig-zag from one entity to the next, and a real stray can lie on a
    /// zig-zag and read as no error at all. A column with a different value on every row is a row
    /// number, not an entity, and is ignored.
    /// </summary>
    private static string? MixedEntities(IReadOnlyList<string> header, ColumnChoice choice, IReadOnlyList<TableRow> rows)
    {
        for (int column = 0; column < header.Count; column++)
        {
            if (column == choice.X || column == choice.Y || column == choice.Z || !IsEntityColumn(header[column]))
            {
                continue;
            }

            List<string> entities = [.. rows
                .Select(row => column < row.Cells.Count ? row.Cells[column] : string.Empty)
                .Where(cell => cell.Length > 0)
                .Distinct(StringComparer.Ordinal)];
            if (entities.Count >= 2 && 2 * entities.Count <= rows.Count)
            {
                string listed = string.Join(", ", entities.Take(EntitiesQuoted)) + (entities.Count > EntitiesQuoted ? ", …" : string.Empty);
                return $"Column '{header[column]}' holds {entities.Count} entities ({listed}). "
                    + "One path through all their points would zig-zag between them; save each entity to a file of its own.";
            }
        }

        return null;
    }

    private static bool IsEntityColumn(string name)
    {
        string[] words = HeaderWords(name);
        return words.Any(EntityWords.Contains) || words is ["id"] or ["name"];
    }

    private static string DescribeRowsAbove(RawTable table, int headerIndex)
    {
        string kind = table.IsWorksheet ? "Row" : "Line";
        int first = table.Rows[0].LineNumber;
        int last = table.Rows[headerIndex - 1].LineNumber;
        return headerIndex == 1
            ? $"{kind} {first}, above the header, was not read."
            : $"{kind}s {first}–{last}, above the header, were not read.";
    }

    private static string JoinAxes(string[] axes) => axes.Length == 1 ? axes[0] : $"{string.Join(", ", axes[..^1])} or {axes[^1]}";

    /// <summary>'a', 'b', 'c', or with a conjunction, 'a', 'b' and 'c'.</summary>
    private static string QuoteAll(IEnumerable<string> names, string? conjunction)
    {
        string[] quoted = [.. names.Select(name => $"'{name}'")];
        return conjunction is null || quoted.Length < 2
            ? string.Join(", ", quoted)
            : $"{string.Join(", ", quoted[..^1])} {conjunction} {quoted[^1]}";
    }

    private static string[] HeaderWords(string name) =>
        [.. HeaderWord().Matches(name).Select(match => match.Value.ToLowerInvariant())];

    private static string Shorten(string cell) => cell.Length <= 24 ? cell : cell[..21] + "...";

    private static TableParseResult Failed(string reason) =>
        new([], [], [], string.Empty, null, reason);

    private static TableParseResult Refused(RawTable table, MissingHeader missing) =>
        Failed(missing.Describe(table.Name)) with { MissingHeader = missing };

    /// <summary>Words of a header: runs of letters split at camel-case humps, or runs of digits.</summary>
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|\d+")]
    private static partial Regex HeaderWord();

    [GeneratedRegex(@"^[+-]?(?:\d{1,3}(?:\.\d{3})+|\d+)(?:,\d+)?(?:[eE][+-]?\d+)?$")]
    private static partial Regex DecimalCommaNumber();

    internal sealed record ColumnChoice(int X, int Y, int Z)
    {
        public string Description { get; init; } = string.Empty;
    }

    /// <param name="Choice">The x, y and z columns; null when the row names too few or names one ambiguously.</param>
    /// <param name="Problem">Why a row that names every axis still cannot be used.</param>
    /// <param name="NamesEveryAxis">Whether every axis is named by some column, which makes this the header.</param>
    /// <param name="Candidates">Per axis, the columns that could be its position, with their scores.</param>
    /// <param name="NotPositions">Per axis, the columns that name it but are velocities or angles.</param>
    private sealed record HeaderReading(
        ColumnChoice? Choice,
        string? Problem,
        bool NamesEveryAxis,
        IReadOnlyList<(int Column, int Score)>[] Candidates,
        IReadOnlyList<int>[] NotPositions)
    {
        public bool NamesAnyAxis => Candidates.Any(list => list.Count > 0) || NotPositions.Any(list => list.Count > 0);
    }
}
