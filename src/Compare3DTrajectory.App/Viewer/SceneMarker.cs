using System.Numerics;
using System.Windows.Media;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>A dot drawn at a fixed size on screen: round at a path's start, square at its end.</summary>
public sealed record SceneMarker(Vector3 Position, Color Colour, double Size, bool IsSquare);
