using System.Numerics;
using System.Windows.Media;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// One line to draw, in render coordinates.
/// </summary>
/// <param name="Positions">Joined in order, or in pairs when <paramref name="IsSegments"/>.</param>
/// <param name="Colour">The whole line's colour, used when there are no vertex colours.</param>
/// <param name="VertexColours">One colour per position, or null for a plain line.</param>
/// <param name="Thickness">In pixels on screen.</param>
/// <param name="IsSegments">True for separate segments (the grid), false for one path.</param>
/// <param name="IsOverlay">
/// Drawn in front of lines at the same place: the reference over the runs following it, and the
/// issue marks over both. Without it the depth test hides whichever comes second.
/// </param>
/// <param name="Describe">Text for the point at an index, shown on hover; null when the line has nothing to say.</param>
public sealed record SceneLine(
    IReadOnlyList<Vector3> Positions,
    Color Colour,
    IReadOnlyList<Color>? VertexColours,
    double Thickness,
    bool IsSegments,
    bool IsOverlay,
    Func<int, string>? Describe);
