using Compare3DTrajectory.App.Settings;

namespace Compare3DTrajectory.App.Tests.Shell;

/// <summary>Settings held in memory; <paramref name="saveProblem"/>, when given, is what every save reports.</summary>
internal sealed class MemorySettingsStore(AppSettings initial, string? saveProblem = null) : ISettingsStore
{
    public AppSettings Saved { get; private set; } = initial;

    public AppSettings Load() => Saved;

    public string? Save(AppSettings settings)
    {
        if (saveProblem is null)
        {
            Saved = settings;
        }

        return saveProblem;
    }
}
