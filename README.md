# Compare 3D Trajectory

A Windows desktop app for checking a simulation run against the trajectory it was meant to follow.
Load any number of CSV or Excel files of world x, y, z. Pick one as the reference. Every other file
is lined up with it and compared in a 3D view you can turn and zoom, and every place a run went wrong
is listed.

## What it finds

| Issue | Meaning |
| --- | --- |
| **Detour** | The run left the reference path by more than the tolerance and came back. |
| **Off path at start / end** | The run began or finished away from the reference path. |
| **Missed** | A stretch of the reference the run never came within tolerance of, such as a cut corner. |

Runs are coloured point by point: green within tolerance, amber up to twice it, red beyond.
Selecting an issue zooms the view to it. Hovering a line shows the point's line number in its file,
its original x, y, z and, for geocentric files, its latitude, longitude and height.

## Lining runs up

With **Fit position and heading** on, each run is first moved and turned about the vertical to fit
the reference best, so only differences of shape are reported. It is never tilted or scaled, so a
left turn can never be fitted onto a right turn. Each row says how far its run was moved and turned,
so a run that was simply in the wrong place is still visible. Turn the fit off to compare positions
exactly as recorded.

## Files

- **Formats:** `.csv`, `.txt` or `.tsv` text, or `.xlsx`, `.xlsm` and `.xls` workbooks. Every sheet
  of a workbook with points on it becomes its own trajectory. Excel does not need to be installed.
- **Layout:** comma, semicolon, tab, `|` or space separated, found automatically. Semicolon, tab
  and space files may use a decimal comma (`1.234,5`). A title or comment above the header is
  passed over.
- **Header:** required. It must name x, y and z, each by exactly one column, such as `x`, `pos_x`,
  `PositionX`, `WorldLocation.X`, `World Location X` or `X (m)`. A position column wins over a plain
  one, and a velocity, angle or error column (`VelX`, `EulerX`, `x_sigma`) is never a position. Other
  columns, such as time or an id, are ignored wherever they sit. A file of numbers without such a
  header is refused with "Please add in the header for x, y, z", the columns found and an example
  first line, and so is one where two columns could be the same axis.
- **One entity per file:** a file whose Entity, Marking or similar column holds several entities is
  refused, since one path through all of them would zig-zag between them.
- **Bad rows:** skipped and listed by line on the file's row: rows that are not numbers or have more
  cells than the header, and rows that cannot be a position, such as an unset value (`1.79769E+308`),
  a no-data marker (`-9999, -9999, -9999`), a geocentric `0, 0, 0`, or a glitch row that jumps off
  the path and straight back.
- **Coordinates:** geocentric (ECEF) metres, such as the HLA RPR FOM `WorldLocation`, are recognised
  automatically when most rows lie on or near the Earth's surface. They are compared in a local
  East-North-Up frame, so "up" is the true vertical. Any other file is read as metres with Z up. A
  file's right-click menu switches between the two.
- **One run at a time:** a run that cannot be compared says so on its own row; the others are still
  compared and drawn.

Drop files or whole folders onto the window, use **Add files…**, or pass paths on the command line.
`samples/` holds a model and four runs with known faults to try it on.

## Install

```powershell
.\tools\install.ps1
```

Publishes a single self-contained `Compare3DTrajectory.exe` (no .NET install needed) to
`%LOCALAPPDATA%\Programs\Compare3DTrajectory` and adds **Compare 3D Trajectory** to the Start menu.
It runs in Windows PowerShell 5.1 as well as PowerShell 7. A new build replaces the installed one
only once its publish has succeeded. `-Uninstall` removes it again, with the native libraries it
unpacks into `%TEMP%`. Settings are kept in `%LOCALAPPDATA%\Compare3DTrajectory`.

## Use from another .NET application

Reference `src/Compare3DTrajectory.Core` (plain .NET 10, no UI) and make one call. It reads and
screens both files, lays geocentric ones out in one East-North-Up frame and fits the run, exactly
as the window does:

```csharp
ComparisonReport report = TrajectoryComparison.Compare(
    referencePath, runPath, new ComparisonOptions { Tolerance = 2, FitHeading = true });

if (!report.Passed)
{
    foreach (ReportedIssue issue in report.Issues)
    {
        // issue.Kind, issue.SourcePath, issue.FirstLine..issue.LastLine, issue.WorstDeviation
    }
}
```

A file that gives no trajectory throws `TrajectoryFileException`; one of numbers without a header
throws it with the same "Please add in the header for x, y, z" message the window shows. To let
someone look, start the window with the same paths, reference first:
`Compare3DTrajectory.exe reference.csv run.csv`.

## Develop

```powershell
dotnet test
```

- **`src/Compare3DTrajectory.Core`:** loading, geodesy, alignment and comparison. Plain .NET with no
  UI, and fully unit-tested.
- **`src/Compare3DTrajectory.App`:** the WPF window, with HelixToolkit for the 3D view.
- **Regenerating the samples:** run `dotnet test` with `COMPARE3DTRAJECTORY_WRITE_SAMPLES=1` set.
