namespace Compare3DTrajectory.App.Viewer;

/// <summary>One segment of the colour-mode selector; the segmented style shows Name and tips Description.</summary>
public sealed record ColourModeOption(ColourMode Mode, string Name, string Description)
{
    public static IReadOnlyList<ColourModeOption> All { get; } =
    [
        new(ColourMode.Deviation, "Deviation", "Green within tolerance, amber up to twice it, red beyond."),
        new(ColourMode.File, "File", "Each run in its own colour, to tell many runs apart."),
    ];

    /// <summary>The option for a mode; the first, for a mode the app does not know, so bad settings never stop it starting.</summary>
    public static ColourModeOption For(ColourMode mode) => All.FirstOrDefault(option => option.Mode == mode) ?? All[0];
}
