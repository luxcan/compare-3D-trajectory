namespace Compare3DTrajectory.Tests;

/// <summary>Finds files in the repository from wherever the test binary runs. Linked into every test project.</summary>
internal static class RepositoryFiles
{
    public static string Root { get; } = FindRoot();

    public static string Samples => Path.Combine(Root, "samples");

    public static string AppSource => Path.Combine(Root, "src", "Compare3DTrajectory.App");

    private static string FindRoot()
    {
        for (DirectoryInfo? folder = new(AppContext.BaseDirectory); folder is not null; folder = folder.Parent)
        {
            if (File.Exists(Path.Combine(folder.FullName, "Compare3DTrajectory.sln")))
            {
                return folder.FullName;
            }
        }

        throw new InvalidOperationException($"No Compare3DTrajectory.sln above {AppContext.BaseDirectory}.");
    }
}
