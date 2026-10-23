using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Settings;

/// <summary>What the app remembers between sessions.</summary>
/// <param name="Tolerance">Metres a point may stray and still be on the path.</param>
/// <param name="FitHeading">Whether runs are lined up with the reference before comparing.</param>
/// <param name="ColourMode">How runs are coloured in the 3D view.</param>
/// <param name="LastFolder">Where the open dialog last looked; null until a file has been opened.</param>
public sealed record AppSettings(double Tolerance, bool FitHeading, ColourMode ColourMode, string? LastFolder)
{
    public static AppSettings Default { get; } = new(2.0, true, ColourMode.Deviation, null);
}
