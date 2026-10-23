using Compare3DTrajectory.App.Settings;
using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Tests.Settings;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "Compare3DTrajectory.AppTests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    [Fact]
    public void Load_NothingSavedYet_GivesTheDefaults()
    {
        new JsonSettingsStore(Path.Combine(_folder, "settings.json")).Load().Should().Be(AppSettings.Default);
    }

    [Fact]
    public void Save_ThenLoad_GivesTheSameSettings()
    {
        var store = new JsonSettingsStore(Path.Combine(_folder, "nested", "settings.json"));
        var settings = new AppSettings(7.5, false, ColourMode.File, @"D:\Runs");

        store.Save(settings).Should().BeNull();

        store.Load().Should().Be(settings);
    }

    [Theory]
    [InlineData("{ not json")]
    [InlineData("{ \"Tolerance\": -3, \"FitHeading\": true, \"ColourMode\": \"File\" }")]
    public void Load_DamagedOrNonsenseFile_GivesTheDefaults(string content)
    {
        Directory.CreateDirectory(_folder);
        string path = Path.Combine(_folder, "settings.json");
        File.WriteAllText(path, content);

        new JsonSettingsStore(path).Load().Should().Be(AppSettings.Default);
    }
}
