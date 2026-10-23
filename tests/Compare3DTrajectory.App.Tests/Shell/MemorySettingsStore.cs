using Compare3DTrajectory.App.Settings;

namespace Compare3DTrajectory.App.Tests.Shell;

internal sealed class MemorySettingsStore(AppSettings initial) : ISettingsStore
{
    public AppSettings Saved { get; private set; } = initial;

    public AppSettings Load() => Saved;

    public string? Save(AppSettings settings)
    {
        Saved = settings;
        return null;
    }
}
