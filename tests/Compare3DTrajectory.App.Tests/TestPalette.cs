using System.Windows.Media;
using Compare3DTrajectory.App.Viewer;

namespace Compare3DTrajectory.App.Tests;

/// <summary>A palette of easily told-apart colours, so a test can say which band a vertex fell in.</summary>
internal static class TestPalette
{
    public static PlotPalette Create() => new(
        Reference: Colors.White,
        Grid: Colors.Gray,
        Highlight: Colors.Cyan,
        LabelText: Colors.Black,
        Within: Colors.Green,
        Near: Colors.Orange,
        Beyond: Colors.Red,
        Missed: Colors.Magenta,
        FileColours: [Colors.Blue, Colors.Yellow, Colors.Purple]);
}
