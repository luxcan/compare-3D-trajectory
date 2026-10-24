using System.Xml.Linq;
using Compare3DTrajectory.App.Shell;

namespace Compare3DTrajectory.App.Tests.Markup;

/// <summary>
/// The window's own markup, read as XML. A binding to a property that does not exist draws nothing
/// and says nothing, so what the busy card is bound to is checked here, with where About sits.
/// </summary>
public class MainWindowMarkupTests
{
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    [Fact]
    public void LoadingCard_FollowsIsLoadingShownAndNeverTakesThePointer()
    {
        XElement card = Window().Descendants(Presentation + "TextBlock")
            .Single(text => (string?)text.Attribute("Text") == "Loading…")
            .Ancestors(Presentation + "Border")
            .First();

        ((string?)card.Attribute("Visibility")).Should().Be($"{{Binding {nameof(MainViewModel.IsLoadingShown)}, Converter={{StaticResource BoolToVisibility}}}}");
        ((string?)card.Attribute("IsHitTestVisible")).Should().Be("False", "the view underneath can still be turned while it shows");
    }

    /// <summary>The design handoff puts About at the far left of the bottom strip, as the only way to the version.</summary>
    [Fact]
    public void AboutButton_IsTheFirstThingInTheStatusBar()
    {
        XElement statusBar = Window().Root!.Elements(Presentation + "Grid").Single()
            .Elements(Presentation + "Border")
            .Single(border => (string?)border.Attribute("Grid.Row") == "2");

        XElement first = statusBar.Element(Presentation + "DockPanel")!.Elements().First();

        first.Name.Should().Be(Presentation + "Button");
        ((string?)first.Attribute("Content")).Should().Be("About");
        ((string?)first.Attribute("Click")).Should().Be("OnAboutRequested");
        ((string?)first.Attribute("DockPanel.Dock")).Should().Be("Left");
    }

    private static XDocument Window() => XDocument.Load(Path.Combine(RepositoryFiles.AppSource, "Shell", "MainWindow.xaml"));
}
