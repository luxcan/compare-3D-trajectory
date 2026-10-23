using System.Diagnostics;

namespace Compare3DTrajectory.App.Tests.Installer;

/// <summary>
/// Runs tools/install.ps1 in Windows PowerShell 5.1, the shell Windows opens by default, against
/// folders of the test's own: a stand-in dotnet on the PATH publishes a placeholder executable at
/// once, or fails when asked to, and the Start Menu entry goes to a folder of the test's, never to
/// the real Start Menu.
/// </summary>
public sealed class InstallScriptTests : IDisposable
{
    private const string StandInDotnet = """
        @echo off
        setlocal
        set "out="
        :next
        if "%~1"=="" goto publish
        if /i "%~1"=="--output" set "out=%~2"
        shift
        goto next
        :publish
        if "%FAKE_PUBLISH_FAIL%"=="1" exit /b 1
        if not exist "%out%" mkdir "%out%"
        > "%out%\Compare3DTrajectory.exe" echo published
        exit /b 0
        """;

    private readonly string _root = Path.Combine(Path.GetTempPath(), "Compare3DTrajectory.InstallTests", Guid.NewGuid().ToString("N"));

    public InstallScriptTests()
    {
        Directory.CreateDirectory(Bin);
        Directory.CreateDirectory(Menu);
        Directory.CreateDirectory(Temp);
        File.WriteAllText(Path.Combine(Bin, "dotnet.cmd"), StandInDotnet.ReplaceLineEndings("\r\n"));
    }

    private string Bin => Path.Combine(_root, "bin");

    private string Menu => Path.Combine(_root, "menu");

    private string Temp => Path.Combine(_root, "temp");

    private string Shortcut => Path.Combine(Menu, "Compare 3D Trajectory.lnk");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    /// <summary>F46: the script called .NET methods Windows PowerShell 5.1 does not have, and installed nothing.</summary>
    [Fact]
    public void Install_InWindowsPowerShell51_PublishesAndAddsTheStartMenuEntry()
    {
        string app = Folder("App");

        (int exitCode, string output) = Run(app);

        exitCode.Should().Be(0, output);
        File.Exists(Path.Combine(app, "Compare3DTrajectory.exe")).Should().BeTrue();
        ShortcutTarget().Should().Be(Path.Combine(app, "Compare3DTrajectory.exe"));
    }

    /// <summary>F47: the working copy was deleted before publishing, so a failed publish left nothing installed.</summary>
    [Fact]
    public void Install_WhenThePublishFails_KeepsTheCopyAlreadyInstalled()
    {
        string app = Installed("App");

        (int exitCode, _) = Run(app, failPublish: true);

        exitCode.Should().NotBe(0);
        File.ReadAllText(Path.Combine(app, "Compare3DTrajectory.exe")).Should().Be("working copy");
    }

    /// <summary>F48: uninstalling left the native libraries the single-file executable unpacks into %TEMP%.</summary>
    [Fact]
    public void Uninstall_RemovesTheUnpackedNativeLibraries()
    {
        string app = Installed("App");
        string unpacked = Path.Combine(Temp, ".net", "Compare3DTrajectory", "1YHcT7nYafoA");
        Directory.CreateDirectory(unpacked);
        File.WriteAllText(Path.Combine(unpacked, "wpfgfx_cor3.dll"), "native");

        (int exitCode, string output) = Run(app, uninstall: true);

        exitCode.Should().Be(0, output);
        Directory.Exists(Path.Combine(Temp, ".net", "Compare3DTrajectory")).Should().BeFalse();
        Directory.Exists(app).Should().BeFalse();
    }

    /// <summary>F49: uninstalling a copy in another folder deleted the Start Menu entry of the real install.</summary>
    [Fact]
    public void Uninstall_OfACopyInAnotherFolder_LeavesTheStartMenuEntryOfTheRealOne()
    {
        string real = Folder("Real");
        Run(real).ExitCode.Should().Be(0);
        string test = Installed("Test");

        (int exitCode, string output) = Run(test, uninstall: true);

        exitCode.Should().Be(0, output);
        ShortcutTarget().Should().Be(Path.Combine(real, "Compare3DTrajectory.exe"));
    }

    /// <summary>F49: processes were matched by a prefix of the folder, so a copy in "…\App.old" was closed too.</summary>
    [Fact]
    public void Install_LeavesACopyRunningFromAnotherFolderAlone()
    {
        string app = Folder("App");
        string old = Folder("App.old");
        Directory.CreateDirectory(old);
        string impostor = Path.Combine(old, "Compare3DTrajectory.exe");
        File.Copy(Path.Combine(Environment.SystemDirectory, "PING.EXE"), impostor);
        using Process running = Process.Start(new ProcessStartInfo(impostor, "-n 120 127.0.0.1") { CreateNoWindow = true, UseShellExecute = false })!;
        try
        {
            (int exitCode, string output) = Run(app);

            exitCode.Should().Be(0, output);
            running.HasExited.Should().BeFalse();
        }
        finally
        {
            running.Kill();
            running.WaitForExit();
        }
    }

    private string Folder(string name) => Path.Combine(_root, "Programs", name);

    /// <summary>A folder that already holds a working copy, as an earlier install leaves it.</summary>
    private string Installed(string name)
    {
        string app = Folder(name);
        Directory.CreateDirectory(app);
        File.WriteAllText(Path.Combine(app, "Compare3DTrajectory.exe"), "working copy");
        return app;
    }

    private (int ExitCode, string Output) Run(string installRoot, bool uninstall = false, bool failPublish = false)
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (string argument in new[] { "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(RepositoryFiles.Root, "tools", "install.ps1"), "-InstallRoot", installRoot, "-ShortcutFolder", Menu })
        {
            start.ArgumentList.Add(argument);
        }

        if (uninstall)
        {
            start.ArgumentList.Add("-Uninstall");
        }

        start.Environment["PATH"] = $"{Bin};{Environment.GetEnvironmentVariable("PATH")}";
        start.Environment["TEMP"] = Temp;
        start.Environment["TMP"] = Temp;
        if (failPublish)
        {
            start.Environment["FAKE_PUBLISH_FAIL"] = "1";
        }

        using Process process = Process.Start(start)!;
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(TimeSpan.FromSeconds(60)))
        {
            process.Kill();
            throw new TimeoutException("install.ps1 did not finish within a minute.");
        }

        return (process.ExitCode, output.Result + error.Result);
    }

    private string? ShortcutTarget()
    {
        if (!File.Exists(Shortcut))
        {
            return null;
        }

        dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell")!)!;
        return (string)shell.CreateShortcut(Shortcut).TargetPath;
    }
}
