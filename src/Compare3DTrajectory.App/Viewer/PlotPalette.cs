using System.Windows;
using System.Windows.Media;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>The colours the 3D view draws with, read from Theme/Plot.xaml.</summary>
public sealed record PlotPalette(
    Color Reference,
    Color Grid,
    Color Highlight,
    Color LabelText,
    Color Within,
    Color Near,
    Color Beyond,
    Color Missed,
    IReadOnlyList<Color> FileColours)
{
    public const int FileColourCount = 10;

    public static PlotPalette FromResources(ResourceDictionary resources)
    {
        ArgumentNullException.ThrowIfNull(resources);
        Color Read(string key) => resources[key] is Color colour
            ? colour
            : throw new InvalidOperationException($"Theme/Plot.xaml has no colour '{key}'.");

        return new PlotPalette(
            Read("PlotReferenceColor"),
            Read("PlotGridColor"),
            Read("PlotHighlightColor"),
            Read("PlotLabelTextColor"),
            Read("DeviationWithinColor"),
            Read("DeviationNearColor"),
            Read("DeviationBeyondColor"),
            Read("MissedColor"),
            [.. Enumerable.Range(0, FileColourCount).Select(i => Read($"FileColor{i}"))]);
    }

    public Color FileColour(int index) => FileColours[index % FileColours.Count];

    /// <summary>The traffic-light band a distance from the reference falls in.</summary>
    public Color ForDeviation(double deviation, double tolerance) =>
        deviation <= tolerance ? Within : deviation <= 2 * tolerance ? Near : Beyond;
}
