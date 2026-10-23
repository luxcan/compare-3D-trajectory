using System.Windows.Media;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// A trajectory as the scene needs it.
/// </summary>
/// <param name="Points">Working metres; for a compared run, after the fit.</param>
/// <param name="Deviations">Each point's distance from the reference, or null for the reference and runs not yet compared.</param>
/// <param name="FileColour">The file's own colour.</param>
/// <param name="IsReference">Drawn in the neutral reference colour, thicker.</param>
/// <param name="IsSelected">Drawn thicker, to pick it out among many.</param>
/// <param name="Describe">The hover text for the point at an index.</param>
public sealed record SceneTrajectory(
    IReadOnlyList<Vec3> Points,
    IReadOnlyList<double>? Deviations,
    Color FileColour,
    bool IsReference,
    bool IsSelected,
    Func<int, string> Describe);
