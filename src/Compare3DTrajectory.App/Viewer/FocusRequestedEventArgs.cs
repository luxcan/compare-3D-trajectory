using System.Numerics;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>Asks the 3D view to frame a box, given in render coordinates.</summary>
public sealed class FocusRequestedEventArgs(Vector3 min, Vector3 max) : EventArgs
{
    public Vector3 Min { get; } = min;

    public Vector3 Max { get; } = max;
}
