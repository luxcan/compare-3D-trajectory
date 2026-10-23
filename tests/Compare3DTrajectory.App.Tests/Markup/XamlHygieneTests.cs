using System.Text.RegularExpressions;

namespace Compare3DTrajectory.App.Tests.Markup;

/// <summary>
/// Rules read from the XAML as text. WPF reports a missing resource or a stray colour only at run
/// time, if at all - a binding to a resource that does not exist simply draws nothing - so these are
/// checked before anything runs.
/// </summary>
public partial class XamlHygieneTests
{
    private static readonly string[] XamlFiles = Directory.GetFiles(RepositoryFiles.AppSource, "*.xaml", SearchOption.AllDirectories)
        .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .ToArray();

    [Fact]
    public void Colours_OutsideTheTheme_AreNeverLiterals()
    {
        string[] offenders =
        [
            .. XamlFiles
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Theme{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => HexColour().IsMatch(WithoutComments(File.ReadAllText(path))))
                .Select(Path.GetFileName)!,
        ];

        offenders.Should().BeEmpty("colours belong in Theme/, reached by key");
    }

    [Fact]
    public void ResourceKeys_UsedAnywhere_AreDefinedSomewhere()
    {
        var defined = new HashSet<string>(StringComparer.Ordinal);
        var used = new List<(string File, string Key)>();
        foreach (string path in XamlFiles)
        {
            string xaml = WithoutComments(File.ReadAllText(path));
            defined.UnionWith(DefinedKey().Matches(xaml).Select(match => match.Groups["key"].Value));
            used.AddRange(UsedKey().Matches(xaml).Select(match => (Path.GetFileName(path), match.Groups["key"].Value)));
        }

        used.Where(use => !defined.Contains(use.Key)).Should().BeEmpty("every {StaticResource} and {DynamicResource} must name a key the app defines");
    }

    [Fact]
    public void XamlFiles_AreFound()
    {
        XamlFiles.Select(Path.GetFileName).Should().Contain(["MainWindow.xaml", "Controls.xaml", "Plot.xaml", "TrajectoryViewport.xaml"]);
    }

    private static string WithoutComments(string xaml) => Comment().Replace(xaml, string.Empty);

    [GeneratedRegex("<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex Comment();

    [GeneratedRegex("#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?\\b")]
    private static partial Regex HexColour();

    [GeneratedRegex("x:Key=\"(?<key>[^\"{]+)\"")]
    private static partial Regex DefinedKey();

    [GeneratedRegex("\\{(?:Static|Dynamic)Resource\\s+(?<key>[A-Za-z][\\w.]*)\\}")]
    private static partial Regex UsedKey();
}
