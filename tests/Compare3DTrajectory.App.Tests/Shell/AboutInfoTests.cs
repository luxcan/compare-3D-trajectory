using Compare3DTrajectory.App.Shell;

namespace Compare3DTrajectory.App.Tests.Shell;

/// <summary>
/// The About window is the one place the app says which build it is, and builds are made by hand
/// from the repository, so the version it reports has to be the one that is running.
/// </summary>
public class AboutInfoTests
{
    [Theory]
    [InlineData("1.0.0+dc078240a1b2c3d4e5f60718293a4b5c6d7e8f90", "Version 1.0.0 (dc07824)")]
    [InlineData("1.2.3-beta+abcdef0123", "Version 1.2.3-beta (abcdef0)")]
    [InlineData("1.0.0+abc", "Version 1.0.0 (abc)")]
    [InlineData("1.0.0+", "Version 1.0.0")]
    [InlineData("1.0.0", "Version 1.0.0")]
    public void DescribeVersion_CarriesTheCommitWhenTheBuildRecordedOne(string informational, string expected)
    {
        AboutInfo.DescribeVersion(informational, new Version(9, 9, 9)).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DescribeVersion_WithoutAnInformationalVersion_FallsBackToTheAssemblys(string? informational)
    {
        AboutInfo.DescribeVersion(informational, new Version(4, 5, 6, 7)).Should().Be("Version 4.5.6");
    }

    [Fact]
    public void DescribeVersion_WithNothingToGoOn_SaysSoRatherThanShowingNothing()
    {
        AboutInfo.DescribeVersion(null, null).Should().Be("Unknown version");
    }

    [Theory]
    [InlineData("2026-10-24", "24 October 2026")]
    [InlineData("2026-01-01", "1 January 2026")]
    public void DescribeBuildDate_WritesTheStampedDayOut(string stamped, string expected)
    {
        AboutInfo.DescribeBuildDate(stamped).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("24 October 2026")]
    [InlineData("not a date")]
    public void DescribeBuildDate_WithoutADay_SaysNothingRatherThanGuessing(string? stamped)
    {
        AboutInfo.DescribeBuildDate(stamped).Should().BeNull();
    }

    /// <summary>The version is set once, in Directory.Build.props, and the build stamps the day it was made.</summary>
    [Fact]
    public void Current_ReadsThisBuild()
    {
        AboutInfo about = AboutInfo.Current;

        about.Name.Should().Be("Compare 3D Trajectory");
        about.Version.Should().StartWith("Version 1.0.0");
        about.BuildDate.Should().NotBeNull();
        about.VersionAndBuild.Should().Be($"{about.Version} · Built {about.BuildDate}");
        about.Description.Should().NotBeEmpty();
        about.IssuesUrl.Should().Be("https://github.com/luxcan/compare-3D-trajectory/issues");
    }

    [Fact]
    public void VersionAndBuild_WithoutABuildDate_IsJustTheVersion()
    {
        (AboutInfo.Current with { BuildDate = null }).VersionAndBuild.Should().Be(AboutInfo.Current.Version);
    }
}
