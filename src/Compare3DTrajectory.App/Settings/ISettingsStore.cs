namespace Compare3DTrajectory.App.Settings;

public interface ISettingsStore
{
    AppSettings Load();

    /// <summary>Saves the settings; returns why it could not, or null when it did.</summary>
    string? Save(AppSettings settings);
}
