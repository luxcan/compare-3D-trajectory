namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// The published single-file exe unpacks its native libraries into a folder of its own build under
/// %TEMP%\.net\Compare3DTrajectory, and every new build adds another. This clears the folders of
/// other builds, leaving whole any that a copy still running is using.
/// </summary>
public static class UnpackedLibraries
{
    /// <summary>The folder the .NET host unpacks into, under which each app has one of its own.</summary>
    private const string UnpackRootName = ".net";

    /// <summary>Clears other builds' folders beside this run's; does nothing for any build but the published exe.</summary>
    public static void ClearOtherBuilds()
    {
        string? own = FindThisBuildsFolder(AppContext.GetData("NATIVE_DLL_SEARCH_DIRECTORIES") as string, AppContext.BaseDirectory, Environment.ProcessPath);
        if (own is not null)
        {
            ClearAllBut(Path.GetDirectoryName(own)!, own);
        }
    }

    /// <summary>
    /// The folder this run's native libraries were unpacked into: the host of the published exe
    /// lists it among the native library folders, as ...\.net\&lt;app&gt;\&lt;build&gt;. Any other
    /// build unpacks nothing, and an ordinary folder is never taken for one, so then this is null.
    /// </summary>
    internal static string? FindThisBuildsFolder(string? nativeSearchDirectories, string baseDirectory, string? processPath)
    {
        if (string.IsNullOrEmpty(nativeSearchDirectories) || string.IsNullOrEmpty(processPath))
        {
            return null;
        }

        string appName = Path.GetFileNameWithoutExtension(processPath);
        string appFolder = Path.TrimEndingDirectorySeparator(baseDirectory);
        foreach (string entry in nativeSearchDirectories.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string folder = Path.TrimEndingDirectorySeparator(entry);
            string? appUnpackFolder = Path.GetDirectoryName(folder);
            if (appUnpackFolder is not null
                && !SamePath(folder, appFolder)
                && SameName(Path.GetFileName(appUnpackFolder), appName)
                && SameName(Path.GetFileName(Path.GetDirectoryName(appUnpackFolder)), UnpackRootName))
            {
                return folder;
            }
        }

        return null;
    }

    /// <summary>
    /// Deletes every folder in <paramref name="appUnpackFolder"/> but <paramref name="keep"/>, and
    /// returns those left because a running copy is using them.
    /// </summary>
    internal static IReadOnlyList<string> ClearAllBut(string appUnpackFolder, string keep)
    {
        List<string> folders;
        try
        {
            folders = [.. Directory.EnumerateDirectories(appUnpackFolder).Where(folder => !SamePath(folder, keep))];
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing to clear when the folder has gone, or cannot be listed.
            return [];
        }

        return [.. folders.Where(folder => !TryDeleteUnused(folder))];
    }

    /// <summary>
    /// Deletes a folder if no running copy is using it. A copy has some of its libraries loaded, and
    /// a loaded library cannot be opened for writing, so every file is first opened for writing with
    /// no reading allowed: if one refuses, the folder is left whole. Held open, they also stop a copy
    /// starting meanwhile from loading them, and are deleted while held.
    /// </summary>
    private static bool TryDeleteUnused(string folder)
    {
        var held = new List<FileStream>();
        try
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            {
                held.Add(new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete));
            }

            foreach (FileStream stream in held)
            {
                File.Delete(stream.Name);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // In use by a running copy: left for a later start to clear.
            return false;
        }
        finally
        {
            foreach (FileStream stream in held)
            {
                stream.Dispose();
            }
        }

        try
        {
            Directory.Delete(folder, recursive: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Its files are gone; the empty folder is cleared on a later start.
            return false;
        }
    }

    private static bool SamePath(string left, string right) =>
        string.Equals(Path.TrimEndingDirectorySeparator(left), Path.TrimEndingDirectorySeparator(right), StringComparison.OrdinalIgnoreCase);

    private static bool SameName(string? left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
