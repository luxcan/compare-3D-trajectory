using System.Numerics;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// Everything the 3D view draws, as plain data, so what is drawn can be tested without a GPU.
/// </summary>
/// <param name="RenderOrigin">
/// Working metres subtracted before positions become floats. The GPU works in single precision,
/// which near this origin still resolves to well under a millimetre.
/// </param>
/// <param name="Lines">Paths, overlays and the ground grid.</param>
/// <param name="Markers">Start and end dots.</param>
/// <param name="Labels">Issue numbers.</param>
/// <param name="GridSpacing">Metres between grid lines, for the scale note; 0 when nothing is drawn.</param>
public sealed record SceneModel(
    Vec3 RenderOrigin,
    IReadOnlyList<SceneLine> Lines,
    IReadOnlyList<SceneMarker> Markers,
    IReadOnlyList<SceneLabel> Labels,
    double GridSpacing)
{
    public static SceneModel Empty { get; } = new(Vec3.Zero, [], [], [], 0);

    public bool IsEmpty => Lines.Count == 0;

    public Vector3 ToRender(Vec3 working) => new(
        (float)(working.X - RenderOrigin.X),
        (float)(working.Y - RenderOrigin.Y),
        (float)(working.Z - RenderOrigin.Z));
}
