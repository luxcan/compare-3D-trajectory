using Compare3DTrajectory.App.Shell;

namespace Compare3DTrajectory.App.Tests.Shell;

public sealed class UnpackedLibrariesTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Compare3DTrajectory.UnpackTests", Guid.NewGuid().ToString("N"));

    public UnpackedLibrariesTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public void FindThisBuildsFolder_PublishedExe_IsTheFolderItUnpackedInto()
    {
        const string Own = @"C:\Users\someone\AppData\Local\Temp\.net\Compare3DTrajectory\Z0p75uGltaQb";

        UnpackedLibraries.FindThisBuildsFolder(Own + @"\;", @"D:\Apps\", @"D:\Apps\Compare3DTrajectory.exe").Should().Be(Own);
    }

    [Fact]
    public void FindThisBuildsFolder_OrdinaryBuild_IsNone()
    {
        const string App = @"C:\repo\src\Compare3DTrajectory.App\bin\Release\net10.0-windows\";
        string search = string.Join(
            Path.PathSeparator,
            App,
            @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.2\",
            @"C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App\10.0.2\");

        UnpackedLibraries.FindThisBuildsFolder(search, App, App + "Compare3DTrajectory.exe").Should().BeNull();
    }

    /// <summary>An app kept in a folder named after itself must never have that folder's neighbours taken for other builds.</summary>
    [Fact]
    public void FindThisBuildsFolder_AppInAFolderNamedAfterIt_IsNone()
    {
        const string App = @"D:\Tools\Compare3DTrajectory\1.0\";

        UnpackedLibraries.FindThisBuildsFolder(App + Path.PathSeparator, App, App + "Compare3DTrajectory.exe").Should().BeNull();
        UnpackedLibraries.FindThisBuildsFolder(@"D:\Tools\Compare3DTrajectory\0.9\", App, App + "Compare3DTrajectory.exe").Should().BeNull();
    }

    [Fact]
    public void ClearAllBut_OtherBuildsUnused_DeletesThemAndKeepsThisOne()
    {
        string own = Build("own");
        Build("older");
        Build("oldest");

        UnpackedLibraries.ClearAllBut(_folder, own).Should().BeEmpty();

        Directory.EnumerateDirectories(_folder).Should().Equal(own);
        Directory.EnumerateFiles(own).Should().HaveCount(2);
    }

    /// <summary>
    /// A copy of an older build still running has some of its libraries loaded. Deleting the others
    /// under it would crash it the moment it loads one, so its folder is left whole.
    /// </summary>
    [Fact]
    public void ClearAllBut_BuildInUse_IsLeftWhole()
    {
        string own = Build("own");
        string running = Build("running");
        string older = Build("older");

        // A loaded library cannot be opened for writing; a file open for reading only stands in for one.
        using (new FileStream(Path.Combine(running, "wpfgfx_cor3.dll"), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            UnpackedLibraries.ClearAllBut(_folder, own).Should().Equal(running);
        }

        Directory.EnumerateFiles(running).Should().HaveCount(2, "nothing of a build in use is deleted");
        Directory.Exists(older).Should().BeFalse();
    }

    [Fact]
    public void ClearAllBut_FolderGone_ClearsNothing()
    {
        UnpackedLibraries.ClearAllBut(Path.Combine(_folder, "missing"), Path.Combine(_folder, "missing", "own")).Should().BeEmpty();
    }

    /// <summary>A build's folder as the host leaves it, with two of the native libraries it unpacks.</summary>
    private string Build(string name)
    {
        string folder = Directory.CreateDirectory(Path.Combine(_folder, name)).FullName;
        File.WriteAllText(Path.Combine(folder, "wpfgfx_cor3.dll"), "native");
        File.WriteAllText(Path.Combine(folder, "vcruntime140_cor3.dll"), "native");
        return folder;
    }
}
