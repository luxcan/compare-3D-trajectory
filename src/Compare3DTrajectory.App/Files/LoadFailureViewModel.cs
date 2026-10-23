using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.App.Files;

/// <summary>A file, or sheet, that could not be read, shown until it is dismissed.</summary>
public sealed class LoadFailureViewModel(LoadFailure failure)
{
    public string Name => failure.Name;

    public string Reason => failure.Reason;

    public string SourcePath => failure.SourcePath;
}
