using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.App.Files;

/// <summary>The dialog shown when a load ends with files refused for want of a header naming x, y and z.</summary>
public static class RefusalText
{
    public static string Title => MissingHeader.Request;

    /// <summary>Every refused file with what was found in it, then the example header once for all of them.</summary>
    public static string Describe(IReadOnlyList<LoadFailure> refused)
    {
        ArgumentNullException.ThrowIfNull(refused);

        var lines = new List<string> { $"{Measure.Plural(refused.Count, "file")} {(refused.Count == 1 ? "was" : "were")} not loaded:" };
        foreach (LoadFailure failure in refused)
        {
            lines.Add(string.Empty);
            lines.Add(failure.Name);
            lines.AddRange(failure.MissingHeader?.Findings ?? [failure.Reason]);
        }

        lines.Add(string.Empty);
        lines.AddRange(refused.Select(failure => failure.MissingHeader?.Example).OfType<string>().Distinct());
        return string.Join(Environment.NewLine, lines);
    }
}
