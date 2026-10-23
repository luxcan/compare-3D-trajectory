using System.Numerics;
using System.Windows.Media;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>Text that always faces the camera, such as an issue's number at its worst point.</summary>
public sealed record SceneLabel(string Text, Vector3 Position, Color Foreground, Color Background);
