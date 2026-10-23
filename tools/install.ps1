<#
.SYNOPSIS
    Installs Compare 3D Trajectory for the current user, or removes it again.

.DESCRIPTION
    Publishes a Release build - one self-contained Compare3DTrajectory.exe, so the machine needs no
    .NET install and no Excel - into a stable folder outside the repository, and adds a Start Menu
    entry for it.

    Re-run this after any code change to update the installed copy. The new build is published
    beside the installed one and only replaces it once the publish has succeeded, so a failed build
    leaves the working copy in place. Settings live separately in %LOCALAPPDATA%\Compare3DTrajectory
    and are never touched, so upgrading and uninstalling both keep them.

    Runs in Windows PowerShell 5.1, the shell Windows opens by default, and in PowerShell 7.

.PARAMETER InstallRoot
    Where the published application lives.

.PARAMETER ShortcutFolder
    Where the Start Menu entry goes; the current user's Start Menu programs folder unless given.

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
    [string] $ShortcutFolder = [Environment]::GetFolderPath('Programs'),
    [switch] $Uninstall
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path $PSScriptRoot -Parent
$projectPath = Join-Path $repoRoot 'src\Compare3DTrajectory.App\Compare3DTrajectory.App.csproj'
$appName = 'Compare3DTrajectory'
$executableName = "$appName.exe"
$shortcutName = 'Compare 3D Trajectory.lnk'

# Only .NET Framework methods, so no Path.IsPathFullyQualified or TrimEndingDirectorySeparator.
if ($InstallRoot -notmatch '^([A-Za-z]:\\|\\\\)') {
    throw "InstallRoot must be a full path. Windows resolves '$InstallRoot' against the current directory."
}
$InstallRoot = [System.IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')

$executablePath = Join-Path $InstallRoot $executableName
$stagingRoot = "$InstallRoot.publishing"
$startMenuLink = Join-Path $ShortcutFolder $shortcutName

# The single-file executable unpacks WPF's native libraries here on first run, into a new folder
# for every build, and nothing else ever removes them.
$unpackedNatives = Join-Path ([System.IO.Path]::GetTempPath()) ".net\$appName"

function Stop-InstalledApp {
    # This install's executable and no other: a copy running from "...\Compare3DTrajectory.old" or
    # a test folder beside it is somebody else's.
    $running = @(Get-Process -Name $appName -ErrorAction SilentlyContinue |
        Where-Object { $_.Path -and [string]::Equals($_.Path, $executablePath, [StringComparison]::OrdinalIgnoreCase) })
    if ($running.Count -eq 0) { return }

    Write-Host "Closing the running application ($($running.Count) instance(s))..."
    foreach ($process in $running) { $process.CloseMainWindow() | Out-Null }
    foreach ($process in $running) { $process.WaitForExit(10000) | Out-Null }

    $stubborn = @($running | Where-Object { -not $_.HasExited })
    if ($stubborn.Count -gt 0) {
        throw 'Compare 3D Trajectory is still open and did not close. Close it and run this again.'
    }
}

function Get-ShortcutTarget {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) { return $null }
    $shell = New-Object -ComObject WScript.Shell
    try { return $shell.CreateShortcut($Path).TargetPath }
    finally { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null }
}

# The Start Menu entry is this install's only when it points at this install's executable, so a
# second copy installed or removed elsewhere never takes over or deletes the real one's.
function Test-OurShortcut {
    $target = Get-ShortcutTarget -Path $startMenuLink
    return $target -and [string]::Equals($target, $executablePath, [StringComparison]::OrdinalIgnoreCase)
}

function Remove-OurShortcut {
    if (Test-OurShortcut) {
        Remove-Item -LiteralPath $startMenuLink -Force
        Write-Host "Removed $startMenuLink"
    }
}

function Remove-Folder {
    param([string] $Path)

    if (-not (Test-Path -LiteralPath $Path)) { return }

    # Only ever delete a folder that actually holds this application, so a mistyped -InstallRoot
    # cannot take something else with it.
    $isOurs = Test-Path -LiteralPath (Join-Path $Path $executableName)
    $isEmpty = -not (Get-ChildItem -LiteralPath $Path -Force | Select-Object -First 1)
    if (-not ($isOurs -or $isEmpty)) {
        throw "$Path does not contain $executableName. Refusing to delete it."
    }

    Remove-Item -LiteralPath $Path -Recurse -Force
    Write-Host "Removed $Path"
}

function Remove-UnpackedNatives {
    if (-not (Test-Path -LiteralPath $unpackedNatives)) { return }
    try {
        Remove-Item -LiteralPath $unpackedNatives -Recurse -Force
        Write-Host "Removed $unpackedNatives"
    }
    catch {
        Write-Warning "Could not remove $unpackedNatives, which another running copy may be using: $($_.Exception.Message)"
    }
}

function New-Shortcut {
    $target = Get-ShortcutTarget -Path $startMenuLink
    if ($target -and -not (Test-OurShortcut) -and (Test-Path -LiteralPath $target)) {
        Write-Warning "The Start Menu entry belongs to the copy in $(Split-Path $target -Parent) and was left alone."
        return
    }

    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($startMenuLink)
        $shortcut.TargetPath = $executablePath
        $shortcut.WorkingDirectory = $InstallRoot
        $shortcut.Description = 'Compare simulation trajectories in 3D and find where a run went wrong'
        $shortcut.IconLocation = "$executablePath,0"
        $shortcut.Save()
    }
    finally {
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
    }
    Write-Host "Created $startMenuLink"
}

if ($Uninstall) {
    Stop-InstalledApp
    Remove-OurShortcut
    Remove-Folder -Path $InstallRoot
    Remove-UnpackedNatives
    Write-Host ''
    Write-Host 'Uninstalled. Your settings are still in %LOCALAPPDATA%\Compare3DTrajectory.'
    return
}

# Publish beside the installed copy first; it is replaced only once the new build is complete.
Remove-Folder -Path $stagingRoot
Write-Host "Publishing to $stagingRoot ..."
dotnet publish $projectPath --configuration Release --output $stagingRoot --nologo
if ($LASTEXITCODE -ne 0) {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
    throw "dotnet publish failed with exit code $LASTEXITCODE. The installed copy, if any, is unchanged."
}
if (-not (Test-Path -LiteralPath (Join-Path $stagingRoot $executableName))) {
    throw "Publish finished but $executableName is missing from $stagingRoot."
}

Stop-InstalledApp
Remove-Folder -Path $InstallRoot
Move-Item -LiteralPath $stagingRoot -Destination $InstallRoot
Remove-UnpackedNatives
New-Shortcut

$version = (Get-Item -LiteralPath $executablePath).VersionInfo.ProductVersion
Write-Host ''
Write-Host "Compare 3D Trajectory $version is installed."
Write-Host '  Start Menu   search for "Compare 3D Trajectory"'
Write-Host "  Folder       $InstallRoot"
