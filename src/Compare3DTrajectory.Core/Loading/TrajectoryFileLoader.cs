using System.Xml;
using ExcelDataReader.Exceptions;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>Reads a CSV or Excel file into trajectories, one per usable table.</summary>
public static class TrajectoryFileLoader
{
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase) { ".csv", ".txt", ".tsv" };

    private static readonly HashSet<string> ExcelExtensions = new(StringComparer.OrdinalIgnoreCase) { ".xlsx", ".xlsm", ".xls" };

    /// <summary>The file types offered by the open dialog and accepted from a drop.</summary>
    public static IReadOnlyList<string> SupportedExtensions { get; } = [.. TextExtensions, .. ExcelExtensions];

    public static bool IsSupported(string path) =>
        SupportedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static FileLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string fileName = Path.GetFileName(path);

        if (!IsSupported(path))
        {
            return Failure(fileName, path, $"'{Path.GetExtension(path)}' files are not supported; use CSV or Excel.");
        }

        IReadOnlyList<RawTable> tables;
        try
        {
            tables = TextExtensions.Contains(Path.GetExtension(path))
                ? [CsvTableReader.ReadFile(path)]
                : ExcelTableReader.ReadFile(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
                                          or ExcelReaderException or InvalidDataException or NotSupportedException
                                          or XmlException)
        {
            return Failure(fileName, path, exception.Message);
        }

        return FromTables(fileName, path, tables);
    }

    /// <summary>
    /// Builds the result from tables already read. An empty worksheet is skipped without comment:
    /// workbooks are full of blank Sheet2s. A sheet with rows that are not a trajectory is reported.
    /// </summary>
    internal static FileLoadResult FromTables(string fileName, string path, IReadOnlyList<RawTable> tables)
    {
        var withRows = tables.Where(table => table.Rows.Count > 0).ToList();
        if (withRows.Count == 0)
        {
            return Failure(fileName, path, "The file is empty.");
        }

        var trajectories = new List<LoadedTrajectory>();
        var failures = new List<LoadFailure>();
        foreach (RawTable table in withRows)
        {
            string name = withRows.Count == 1 ? fileName : $"{fileName} › {table.Name}";
            TableParseResult parsed = TrajectoryTableParser.Parse(table with { Name = name });
            if (parsed.FailureReason is not null)
            {
                failures.Add(new LoadFailure(name, path, parsed.FailureReason) { MissingHeader = parsed.MissingHeader });
                continue;
            }

            trajectories.Add(new LoadedTrajectory(
                name,
                path,
                parsed.Points,
                parsed.LineNumbers,
                parsed.SkippedRows,
                parsed.ColumnDescription,
                parsed.Note,
                parsed.DetectedKind));
        }

        return new FileLoadResult(trajectories, failures);
    }

    private static FileLoadResult Failure(string name, string path, string reason) =>
        new([], [new LoadFailure(name, path, reason)]);
}
