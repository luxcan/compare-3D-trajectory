<#
.SYNOPSIS
    Installs Compare 3D Trajectory for the current user, or removes it again.

.DESCRIPTION
    Publishes a Release build - one self-contained Compare3DTrajectory.exe, so the machine needs no
    .NET install and no Excel - into a stable folder outside the repository, and adds a Start Menu
    entry for it.

    Re-run this after any code change to update the installed copy. Settings live separately in
    %LOCALAPPDATA%\Compare3DTrajectory and are never touched, so upgrading and uninstalling both keep
    them.

.PARAMETER InstallRoot
    Where the published application lives.

.PARAMETER Uninstall
    Remove the installed application and its Start Menu entry.

.EXAMPLE
    .\tools\install.ps1
    Installs or upgrades.

.EXAMPLE
    .\tools\install.ps1 -Uninstall
    Removes everything except the saved settings.
#>
[CmdletBinding()]
param(
    [string] $InstallRoot = "$env:LOCALAPPDATA\Programs\Compare3DTrajectory",
    [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'src\Compare3DTrajectory.App\Compare3DTrajectory.App.csproj'
$executableName = 'Compare3DTrajectory.exe'
$shortcutName = 'Compare 3D Trajectory.lnk'

if (-not [System.IO.Path]::IsPathFullyQualified($InstallRoot)) {
    throw "InstallRoot must be a full path. Windows resolves '$InstallRoot' against the current directory."
}
$InstallRoot = [System.IO.Path]::TrimEndingDirectorySeparator([System.IO.Path]::GetFullPath($InstallRoot))

$executablePath = Join-Path $InstallRoot $executableName
$startMenuLink = Join-Path ([Environment]::GetFolderPath('Programs')) $shortcutName

function Stop-InstalledApp {
    $running = @(Get-Process -Name 'Compare3DTrajectory' -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and $_.Path.StartsWith($InstallRoot, [StringComparison]::OrdinalIgnoreCase) })
    if ($running.Count -eq 0) { return }

    Write-Host "Closing the running application ($($running.Count) instance(s))..."
    foreach ($process in $running) { $process.CloseMainWindow() | Out-Null }
    foreach ($process in $running) { $process.WaitForExit(10000) | Out-Null }

    $stubborn = @($running | Where-Object { -not $_.HasExited })
    if ($stubborn.Count -gt 0) {
        throw 'Compare 3D Trajectory is still open and did not close. Close it and run this again.'
    }
}

function Remove-InstalledFiles {
    if (Test-Path -LiteralPath $startMenuLink) {
        Remove-Item -LiteralPath $startMenuLink -Force
        Write-Host "Removed $startMenuLink"
    }

    if (-not (Test-Path -LiteralPath $InstallRoot)) { return }

    # Only ever delete a folder that actually holds this application, so a mistyped -InstallRoot
    # cannot take something else with it.
    $isOurs = Test-Path -LiteralPath $executablePath
    $isEmpty = -not (Get-ChildItem -LiteralPath $InstallRoot -Force | Select-Object -First 1)
    if (-not ($isOurs -or $isEmpty)) {
        throw "$InstallRoot does not contain $executableName. Refusing to delete it."
    }

    Remove-Item -LiteralPath $InstallRoot -Recurse -Force
    Write-Host "Removed $InstallRoot"
}

function New-Shortcut {
    param([string] $Path)

    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($Path)
        $shortcut.TargetPath = $executablePath
        $shortcut.WorkingDirectory = $InstallRoot
        $shortcut.Description = 'Compare simulation trajectories in 3D and find where a run went wrong'
        $shortcut.IconLocation = "$executablePath,0"
        $shortcut.Save()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
    Write-Host "Created $Path"
}

Stop-InstalledApp

if ($Uninstall) {
    Remove-InstalledFiles
    Write-Host ''
    Write-Host 'Uninstalled. Your settings are still in %LOCALAPPDATA%\Compare3DTrajectory.'
    return
}

Remove-InstalledFiles

Write-Host "Publishing to $InstallRoot ..."
dotnet publish $projectPath --configuration Release --output $InstallRoot --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }
if (-not (Test-Path -LiteralPath $executablePath)) { throw "Publish finished but $executablePath is missing." }

New-Shortcut -Path $startMenuLink

$version = (Get-Item $executablePath).VersionInfo.ProductVersion
Write-Host ''
Write-Host "Compare 3D Trajectory $version is installed."
Write-Host '  Start Menu   search for "Compare 3D Trajectory"'
Write-Host "  Folder       $InstallRoot"
