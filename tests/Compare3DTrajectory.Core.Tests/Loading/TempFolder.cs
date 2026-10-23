namespace Compare3DTrajectory.Core.Tests.Loading;

/// <summary>A fresh folder under the temp directory, deleted again when the test is done.</summary>
internal sealed class TempFolder : IDisposable
{
    public TempFolder()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "Compare3DTrajectory.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public string WriteText(string name, string content)
    {
        string path = File(name);
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    public void Dispose() => Directory.Delete(Path, recursive: true);
}
