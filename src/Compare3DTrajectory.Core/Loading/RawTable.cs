namespace Compare3DTrajectory.Core.Loading;

/// <summary>The cells of a CSV file or one worksheet, as text, before anything is interpreted.</summary>
public sealed record RawTable(string Name, IReadOnlyList<TableRow> Rows)
{
    /// <summary>
    /// True for a worksheet, whose cells keep their columns whatever they hold; false for delimited
    /// text, where a stray delimiter moves every later cell along.
    /// </summary>
    public bool IsWorksheet { get; init; }

    /// <summary>What separates the cells of delimited text; null for runs of whitespace, and for a worksheet.</summary>
    public char? Delimiter { get; init; }
}
