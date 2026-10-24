using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Compare3DTrajectory.App.Shell;

/// <summary>
/// Names the build and where it came from. It is the one place the version is shown, opened from
/// the About button at the left end of the status bar, where the design handoff puts it.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        DataContext = AboutInfo.Current;

        // A Hyperlink does not follow itself, and RequestNavigate bubbles, so both links arrive here.
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnLinkRequested));
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        DarkTitleBar.Apply(this);
    }

    /// <summary>UseShellExecute is what hands the address to the browser rather than trying to run it.</summary>
    private static void OnLinkRequested(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        using Process? browser = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
    }
}
