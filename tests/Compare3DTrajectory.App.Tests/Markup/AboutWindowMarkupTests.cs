using System.Text.RegularExpressions;
using Compare3DTrajectory.App.Shell;

namespace Compare3DTrajectory.App.Tests.Markup;

/// <summary>
/// A binding to a property that does not exist leaves its text empty and says nothing, so every
/// binding in the About window must name something AboutInfo has.
/// </summary>
public partial class AboutWindowMarkupTests
{
    [Fact]
    public void AboutWindow_BindsOnlyToWhatAboutInfoHas()
    {
        string xaml = File.ReadAllText(Path.Combine(RepositoryFiles.AppSource, "Shell", "AboutWindow.xaml"));

        string[] bound = [.. BindingPath().Matches(xaml).Select(match => match.Groups["path"].Value).Distinct()];

        bound.Should().Contain(nameof(AboutInfo.VersionAndBuild));
        bound.Should().OnlyContain(path => typeof(AboutInfo).GetProperty(path) != null);
    }

    [GeneratedRegex(@"\{Binding (?<path>\w+)")]
    private static partial Regex BindingPath();
}
