using System.Numerics;
using System.Windows.Media;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// Turns trajectories and issues into the lines, dots and labels of the 3D view.
/// </summary>
public static class SceneBuilder
{
    /// <summary>
    /// The reference is drawn last and thinner than the runs, so where a run follows it the reference
    /// shows as a white core inside the run's colour, and where the run strays the two separate.
    /// </summary>
    public const double ReferenceThickness = 2;
    public const double RunThickness = 3.5;
    public const double SelectedExtraThickness = 1.5;
    public const double MissedThickness = 4.5;
    public const double HighlightThickness = 6;
    public const double GridThickness = 1;

    private const double ReferenceMarkerSize = 10;
    private const double RunMarkerSize = 7;
    private const int TargetGridLines = 10;

    /// <summary>A round spacing gives a dozen lines at most; anything near this is an extent float cannot hold.</summary>
    private const int MaximumGridLines = 100;

    public static SceneModel Build(
        IReadOnlyList<SceneTrajectory> trajectories,
        IReadOnlyList<SceneIssue> issues,
        ColourMode mode,
        double tolerance,
        PlotPalette palette)
    {
        ArgumentNullException.ThrowIfNull(trajectories);
        ArgumentNullException.ThrowIfNull(issues);
        ArgumentNullException.ThrowIfNull(palette);

        if (trajectories.Count == 0)
        {
            return SceneModel.Empty;
        }

        SceneTrajectory anchor = trajectories.FirstOrDefault(trajectory => trajectory.IsReference) ?? trajectories[0];
        var scene = new SceneModel(Vec3.Centroid(anchor.Points), [], [], [], 0);
        var lines = new List<SceneLine>();
        var markers = new List<SceneMarker>();
        var labels = new List<SceneLabel>();

        foreach (SceneTrajectory trajectory in trajectories.OrderBy(trajectory => trajectory.IsReference))
        {
            Vector3[] positions = [.. trajectory.Points.Select(scene.ToRender)];
            Color colour = trajectory.IsReference ? palette.Reference : trajectory.FileColour;
            IReadOnlyList<Color>? vertexColours = !trajectory.IsReference && mode == ColourMode.Deviation && trajectory.Deviations is { } deviations
                ? [.. deviations.Select(deviation => palette.ForDeviation(deviation, tolerance))]
                : null;
            double thickness = (trajectory.IsReference ? ReferenceThickness : RunThickness)
                + (trajectory.IsSelected ? SelectedExtraThickness : 0);

            lines.Add(new SceneLine(positions, colour, vertexColours, thickness, IsSegments: false, IsOverlay: trajectory.IsReference, trajectory.Describe));

            double markerSize = trajectory.IsReference ? ReferenceMarkerSize : RunMarkerSize;
            markers.Add(new SceneMarker(positions[0], colour, markerSize, IsSquare: false));
            markers.Add(new SceneMarker(positions[^1], colour, markerSize, IsSquare: true));
        }

        foreach (SceneIssue sceneIssue in issues)
        {
            TrajectoryIssue issue = sceneIssue.Issue;
            Color colour = issue.Kind == IssueKind.Missed ? palette.Missed : palette.Beyond;
            Vector3[] path = [.. issue.Path.Select(scene.ToRender)];
            if (issue.Kind == IssueKind.Missed && path.Length > 1)
            {
                lines.Add(new SceneLine(path, colour, null, MissedThickness, IsSegments: false, IsOverlay: true, null));
            }

            if (sceneIssue.IsSelected && path.Length > 1)
            {
                lines.Add(new SceneLine(path, palette.Highlight, null, HighlightThickness, IsSegments: false, IsOverlay: true, null));
            }

            labels.Add(new SceneLabel(
                sceneIssue.Number.ToString(System.Globalization.CultureInfo.CurrentCulture),
                scene.ToRender(issue.WorstPoint),
                palette.LabelText,
                sceneIssue.IsSelected ? palette.Highlight : colour));
        }

        (SceneLine grid, double spacing) = GroundGrid(lines, palette.Grid);
        lines.Insert(0, grid);

        return scene with { Lines = lines, Markers = markers, Labels = labels, GridSpacing = spacing };
    }

    /// <summary>
    /// A square grid on the level of the lowest point, about ten lines across, spaced at a round
    /// 1, 2 or 5 times a power of ten so the spacing is a number worth reading.
    /// </summary>
    /// <remarks>
    /// Lines are counted rather than stepped to, and only finite positions set the extent: a point
    /// beyond the range of float once made the loop step towards infinity, or stand still at a value
    /// too large for one more step to change, until memory ran out. With no sensible extent there is
    /// no grid.
    /// </remarks>
    private static (SceneLine Grid, double Spacing) GroundGrid(IReadOnlyList<SceneLine> lines, Color colour)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        foreach (Vector3 position in lines.SelectMany(line => line.Positions).Where(IsFinite))
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
        }

        double spacing = NiceStep(Math.Max(max.X - min.X, max.Y - min.Y) / TargetGridLines);
        double fromX = Math.Floor(min.X / spacing) * spacing;
        double toX = Math.Ceiling(max.X / spacing) * spacing;
        double fromY = Math.Floor(min.Y / spacing) * spacing;
        double toY = Math.Ceiling(max.Y / spacing) * spacing;
        double columns = Math.Round((toX - fromX) / spacing);
        double rows = Math.Round((toY - fromY) / spacing);
        if (!(min.X <= max.X && columns <= MaximumGridLines && rows <= MaximumGridLines))
        {
            return (new SceneLine([], colour, null, GridThickness, IsSegments: true, IsOverlay: false, null), 0);
        }

        float z = min.Z;
        var segments = new List<Vector3>();
        for (int column = 0; column <= columns; column++)
        {
            float x = (float)(fromX + (column * spacing));
            segments.Add(new Vector3(x, (float)fromY, z));
            segments.Add(new Vector3(x, (float)toY, z));
        }

        for (int row = 0; row <= rows; row++)
        {
            float y = (float)(fromY + (row * spacing));
            segments.Add(new Vector3((float)fromX, y, z));
            segments.Add(new Vector3((float)toX, y, z));
        }

        return (new SceneLine(segments, colour, null, GridThickness, IsSegments: true, IsOverlay: false, null), spacing);
    }

    private static bool IsFinite(Vector3 position) =>
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z);

    /// <summary><paramref name="rough"/> rounded to the nearest of 1, 2 or 5 × 10ⁿ.</summary>
    internal static double NiceStep(double rough)
    {
        if (!(rough > 0) || !double.IsFinite(rough))
        {
            return 1;
        }

        double power = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        double fraction = rough / power;
        double nice = fraction < 1.5 ? 1 : fraction < 3.5 ? 2 : fraction < 7.5 ? 5 : 10;
        return nice * power;
    }
}
