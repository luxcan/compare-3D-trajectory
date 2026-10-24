using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// Render positions and deviation colours already worked out, kept for as long as the trajectory's
/// own points and deviations are. A redraw that leaves a trajectory as it was then hands the view the
/// very same arrays, and the view keeps that trajectory's line instead of building it again.
/// </summary>
public sealed class SceneCache
{
    private readonly ConditionalWeakTable<IReadOnlyList<Vec3>, Placed> _placed = new();
    private readonly ConditionalWeakTable<IReadOnlyList<double>, Banded> _banded = new();

    /// <summary>The points as the GPU takes them, about the scene's origin, and the box round them.</summary>
    public (Vector3[] Positions, RenderBox Box) Place(IReadOnlyList<Vec3> points, SceneModel scene)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(scene);
        if (!_placed.TryGetValue(points, out Placed? placed) || placed.Origin != scene.RenderOrigin)
        {
            Vector3[] positions = [.. points.Select(scene.ToRender)];
            placed = new Placed(scene.RenderOrigin, positions, RenderBox.OfFinite(positions));
            _placed.AddOrUpdate(points, placed);
        }

        return (placed.Positions, placed.Box);
    }

    /// <summary>Each deviation's traffic-light band at the tolerance.</summary>
    public Color[] Band(IReadOnlyList<double> deviations, double tolerance, PlotPalette palette)
    {
        ArgumentNullException.ThrowIfNull(deviations);
        ArgumentNullException.ThrowIfNull(palette);
        if (!_banded.TryGetValue(deviations, out Banded? banded) || banded.Tolerance != tolerance || banded.Palette != palette)
        {
            banded = new Banded(tolerance, palette, [.. deviations.Select(deviation => palette.ForDeviation(deviation, tolerance))]);
            _banded.AddOrUpdate(deviations, banded);
        }

        return banded.Colours;
    }

    private sealed record Placed(Vec3 Origin, Vector3[] Positions, RenderBox Box);

    private sealed record Banded(double Tolerance, PlotPalette Palette, Color[] Colours);
}
