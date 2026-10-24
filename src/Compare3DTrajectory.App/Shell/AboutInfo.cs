using System.Globalization;
using System.Reflection;

namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// What the About window says about this build, after PdfTool's. Everything but the repository is
/// read from the assembly, so a build describes itself instead of repeating a version that would
/// have to be kept in step by hand: the one place it is set is Directory.Build.props.
/// </summary>
/// <param name="Name">The product name, as a person reads it.</param>
/// <param name="Version">The version, and the commit it was built from when that is known.</param>
/// <param name="BuildDate">The day the build was made, or null when it did not record one.</param>
/// <param name="Description">One line on what the application is for.</param>
/// <param name="RepositoryUrl">Where the source lives.</param>
internal sealed record AboutInfo(
    string Name,
    string Version,
    string? BuildDate,
    string Description,
    string RepositoryUrl)
{
    private const string Repository = "https://github.com/luxcan/compare-3D-trajectory";

    /// <summary>The project file stamps this; nothing else records when a build was made.</summary>
    private const string BuildDateKey = "BuildDate";

    public static AboutInfo Current { get; } = Read(typeof(AboutInfo).Assembly);

    /// <summary>The line under the product name, as the About window shows it.</summary>
    public string VersionAndBuild => BuildDate is null ? Version : $"{Version} · Built {BuildDate}";

    public string IssuesUrl => $"{RepositoryUrl}/issues";

    private static AboutInfo Read(Assembly assembly) => new(
        assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product ?? "Compare 3D Trajectory",
        DescribeVersion(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version),
        DescribeBuildDate(assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(metadata => metadata.Key == BuildDateKey)?.Value),
        assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()?.Description ?? string.Empty,
        Repository);

    /// <summary>
    /// The version, carrying the commit it came from when the build recorded one. Builds are made by
    /// hand from the repository, and "1.0.0" alone does not say which one is running.
    /// </summary>
    internal static string DescribeVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return assemblyVersion is null ? "Unknown version" : $"Version {assemblyVersion.ToString(fieldCount: 3)}";
        }

        // The SDK appends "+<commit sha>". Seven characters identify a commit as well as forty do,
        // and are short enough to read out.
        string[] parts = informationalVersion.Split('+', 2);
        return parts is [string version, { Length: > 0 } commit]
            ? $"Version {version} ({commit[..Math.Min(7, commit.Length)]})"
            : $"Version {parts[0]}";
    }

    /// <summary>
    /// The stamped build day, written out the way the About window reads it. A build that recorded
    /// nothing, or something that is not a date, says nothing rather than guessing at one.
    /// </summary>
    internal static string? DescribeBuildDate(string? stamped) =>
        DateTime.TryParseExact(stamped, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
            ? date.ToString("d MMMM yyyy", CultureInfo.InvariantCulture)
            : null;
}
