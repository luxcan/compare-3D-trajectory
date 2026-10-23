namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Why a table of numbers was refused: no row of it names x, y and z. The wording is the user's own
/// request, and the window and the Core entry point say it the same way.
/// </summary>
/// <param name="ColumnsFound">What the table holds where the header should be, as the user is shown it.</param>
/// <param name="Problem">What is wrong with those columns; null when there is no header at all.</param>
/// <param name="Example">A first line, or row, that would be accepted, in the file's own layout.</param>
public sealed record MissingHeader(string ColumnsFound, string? Problem, string Example)
{
    public const string Request = "Please add in the header for x, y, z";

    /// <summary>What was found, one line each: the columns, then what is wrong with them.</summary>
    public IReadOnlyList<string> Findings =>
        Problem is null ? [$"Columns found: {ColumnsFound}"] : [$"Columns found: {ColumnsFound}", Problem];

    /// <summary>The whole refusal for one file or sheet: the request, the file, what was found and the example.</summary>
    public string Describe(string name) =>
        string.Join(Environment.NewLine, [$"{Request}.", $"File: {name}", .. Findings, Example]);
}
