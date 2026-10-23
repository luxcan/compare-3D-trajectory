using System.Text.Json;
using System.Text.Json.Serialization;

namespace Compare3DTrajectory.App.Settings;

/// <summary>
/// Keeps the settings as JSON in the user's local application data, never beside the executable,
/// which may be somewhere the user cannot write.
/// </summary>
public sealed class JsonSettingsStore(string path) : ISettingsStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>%LOCALAPPDATA%\Compare3DTrajectory\settings.json for the current user.</summary>
    public static JsonSettingsStore ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Compare3DTrajectory",
        "settings.json"));

    /// <summary>
    /// The saved settings, or the defaults when there are none yet or the file cannot be used. A
    /// damaged settings file is not worth stopping the app over: the next save replaces it.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(path))
            {
                return AppSettings.Default;
            }

            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Options);
            return loaded is not null && loaded.Tolerance > 0 && double.IsFinite(loaded.Tolerance)
                ? loaded
                : AppSettings.Default;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return AppSettings.Default;
        }
    }

    public string? Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, Options));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return exception.Message;
        }
    }
}
