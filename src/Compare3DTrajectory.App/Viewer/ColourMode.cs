namespace Compare3DTrajectory.App.Viewer;

/// <summary>How runs are coloured in the 3D view.</summary>
public enum ColourMode
{
    /// <summary>Green within tolerance, amber up to twice it, red beyond.</summary>
    Deviation,

    /// <summary>Each file in its own colour, to tell many runs apart.</summary>
    File,
}
