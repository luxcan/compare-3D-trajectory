using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HelixToolkit;
using HelixToolkit.Maths;
using HelixToolkit.SharpDX;
using HelixToolkit.Wpf.SharpDX;
using Color = System.Windows.Media.Color;
using HitTestResult = HelixToolkit.SharpDX.HitTestResult;
using PerspectiveCamera = HelixToolkit.Wpf.SharpDX.PerspectiveCamera;
using ProjectionCamera = HelixToolkit.Wpf.SharpDX.ProjectionCamera;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>
/// Draws a <see cref="SceneModel"/> with HelixToolkit and reports what is under the pointer. It holds
/// no state of its own beyond the GPU objects: everything drawn comes from the scene.
/// </summary>
public partial class TrajectoryViewport : UserControl
{
    public static readonly DependencyProperty SceneProperty = DependencyProperty.Register(
        nameof(Scene),
        typeof(SceneModel),
        typeof(TrajectoryViewport),
        new PropertyMetadata(SceneModel.Empty, (d, _) => ((TrajectoryViewport)d).Rebuild()));

    /// <summary>
    /// How many pixels at the bottom of the view are covered by something drawn over it, such as the
    /// legend; framing keeps what it frames above them.
    /// </summary>
    public static readonly DependencyProperty CoveredBelowProperty = DependencyProperty.Register(
        nameof(CoveredBelow),
        typeof(double),
        typeof(TrajectoryViewport),
        new PropertyMetadata(0.0));

    /// <summary>
    /// Added to <see cref="CoveredBelow"/>: the legend's 10-pixel margin below it, and a little room
    /// between it and what is framed.
    /// </summary>
    private const double CoverClearance = 18;

    private const double ZoomMilliseconds = 400;
    private const double HoverHitThickness = 8;

    /// <summary>
    /// Pulls overlay lines towards the camera in the depth test. Enough to win against a run recorded a
    /// few tenths of a metre off the reference at any normal viewing distance; close up, a run that
    /// really is nearer the camera is still drawn in front.
    /// </summary>
    private const int OverlayDepthBias = -2000;
    private static readonly TimeSpan HoverInterval = TimeSpan.FromMilliseconds(40);

    private readonly Dictionary<Element3D, SceneLine> _hoverable = [];
    private readonly TrailingThrottle _hover;
    private DefaultEffectsManager? _effects;

    /// <summary>
    /// Each trajectory's line by the arrays it was built from. A new scene that brings the same
    /// arrays, as <see cref="SceneCache"/> does for a trajectory that has not changed, keeps the line
    /// already on the GPU, so showing or hiding one file leaves every other file's line alone.
    /// </summary>
    private Dictionary<KeptLine, LineGeometryModel3D> _kept = [];

    public TrajectoryViewport()
    {
        InitializeComponent();
        _hover = new TrailingThrottle(HoverInterval, () => DateTime.UtcNow, (delay, run) =>
        {
            var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = delay };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                run();
            };
            timer.Start();
        });
    }

    public SceneModel Scene
    {
        get => (SceneModel)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    public double CoveredBelow
    {
        get => (double)GetValue(CoveredBelowProperty);
        set => SetValue(CoveredBelowProperty, value);
    }

    /// <summary>Frames everything drawn, keeping the current viewing direction.</summary>
    public void FitAll()
    {
        Vector3[] drawn = [.. Scene.Lines.Where(line => !line.IsSegments).SelectMany(line => line.Positions)];
        if (drawn.Length > 0 && View.Camera is ProjectionCamera camera)
        {
            MoveTo(camera, CameraFraming.FitAll(drawn, camera.LookDirection, camera.UpDirection, FieldOfView(camera), WidthOverHeight, ShareCovered));
        }
    }

    /// <summary>Frames one box, given in render coordinates.</summary>
    public void FocusOn(Vector3 min, Vector3 max)
    {
        if (View.Camera is ProjectionCamera camera)
        {
            MoveTo(camera, CameraFraming.Focus(min, max, camera.LookDirection, camera.UpDirection, FieldOfView(camera), WidthOverHeight, ShareCovered));
        }
    }

    private double WidthOverHeight => View.ActualWidth > 0 && View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 1;

    private double ShareCovered => CoveredBelow > 0 && View.ActualHeight > 0 ? (CoveredBelow + CoverClearance) / View.ActualHeight : 0;

    private static double FieldOfView(ProjectionCamera camera) => camera is PerspectiveCamera perspective ? perspective.FieldOfView : 45;

    /// <summary>
    /// Moves the camera, looking the same way as before. HelixToolkit's own ZoomExtents also turned
    /// the camera, which lost the user's chosen angle every time an issue was picked.
    /// </summary>
    private static void MoveTo(ProjectionCamera camera, CameraPlacement placement) =>
        camera.AnimateTo(placement.Position, placement.LookDirection, placement.UpDirection, ZoomMilliseconds);

    /// <summary>
    /// The GPU device lives only while the control is on screen; leaving it to the finaliser would
    /// hold video memory until some later collection.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _effects ??= new DefaultEffectsManager();
        View.EffectsManager = _effects;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        View.EffectsManager = null;
        _effects?.Dispose();
        _effects = null;
    }

    /// <summary>
    /// Draws the scene. Trajectory lines it shares with the scene before are kept and only restyled;
    /// the ground grid, issue marks, start and end dots and labels are small and drawn afresh.
    /// </summary>
    private void Rebuild()
    {
        GroundLayer.Children.Clear();
        MarkLayer.Children.Clear();
        _hoverable.Clear();
        HoverCard.Visibility = Visibility.Collapsed;
        SceneModel scene = Scene;
        var kept = new Dictionary<KeptLine, LineGeometryModel3D>();

        // A line needs two positions; the grid has none when the scene's extent is too large to draw one.
        foreach (SceneLine line in scene.Lines.Where(line => line.Positions.Count >= 2))
        {
            if (line.Describe is null)
            {
                (line.IsSegments ? GroundLayer : MarkLayer).Children.Add(NewLine(line));
                continue;
            }

            var key = new KeptLine(line.Positions, line.VertexColours, line.IsOverlay, Occurrence: 0);
            while (kept.ContainsKey(key))
            {
                key = key with { Occurrence = key.Occurrence + 1 };
            }

            if (_kept.Remove(key, out LineGeometryModel3D? model))
            {
                Restyle(model, line);
            }
            else
            {
                model = NewLine(line);
                (line.IsOverlay ? ReferenceLayer : RunLayer).Children.Add(model);
            }

            kept[key] = model;
            _hoverable[model] = line;
        }

        foreach (LineGeometryModel3D gone in _kept.Values)
        {
            RunLayer.Children.Remove(gone);
            ReferenceLayer.Children.Remove(gone);
        }

        _kept = kept;

        foreach (SceneMarker marker in scene.Markers)
        {
            MarkLayer.Children.Add(new PointGeometryModel3D
            {
                Geometry = new PointGeometry3D { Positions = new Vector3Collection([marker.Position]) },
                Color = marker.Colour,
                Size = new Size(marker.Size, marker.Size),
                Figure = marker.IsSquare ? PointFigure.Rect : PointFigure.Ellipse,
                IsHitTestVisible = false,
            });
        }

        if (scene.Labels.Count > 0)
        {
            var text = new BillboardText3D();
            foreach (SceneLabel label in scene.Labels)
            {
                text.TextInfo.Add(new TextInfo(label.Text, label.Position)
                {
                    Foreground = ToColor4(label.Foreground),
                    Background = ToColor4(label.Background),
                    Scale = 0.9f,
                });
            }

            MarkLayer.Children.Add(new BillboardTextModel3D { Geometry = text, FixedSize = true, IsHitTestVisible = false });
        }
    }

    private static LineGeometryModel3D NewLine(SceneLine line)
    {
        var model = new LineGeometryModel3D { Geometry = LineGeometry(line), Smoothness = 0.6, HitTestThickness = HoverHitThickness };
        Restyle(model, line);
        return model;
    }

    /// <summary>Everything about a line that can change without building its geometry again.</summary>
    private static void Restyle(LineGeometryModel3D model, SceneLine line)
    {
        model.Color = line.VertexColours is null ? line.Colour : Colors.White;
        model.Thickness = line.Thickness;
        model.IsHitTestVisible = line.Describe is not null;
        model.DepthBias = line.IsOverlay ? OverlayDepthBias : 0;
    }

    private static LineGeometry3D LineGeometry(SceneLine line)
    {
        var indices = new IntCollection();
        int step = line.IsSegments ? 2 : 1;
        for (int i = 0; i + 1 < line.Positions.Count; i += step)
        {
            indices.Add(i);
            indices.Add(i + 1);
        }

        var geometry = new LineGeometry3D
        {
            Positions = new Vector3Collection(line.Positions),
            Indices = indices,
        };

        if (line.VertexColours is not null)
        {
            geometry.Colors = new Color4Collection(line.VertexColours.Select(ToColor4));
        }

        return geometry;
    }

    /// <summary>Shows what is under the pointer, unless a drag is turning or moving the view.</summary>
    private void OnViewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed || e.RightButton == MouseButtonState.Pressed || e.MiddleButton == MouseButtonState.Pressed)
        {
            _hover.Cancel();
            HoverCard.Visibility = Visibility.Collapsed;
            return;
        }

        Point pointer = e.GetPosition(View);
        _hover.Submit(() => ShowWhatIsAt(pointer));
    }

    private void ShowWhatIsAt(Point pointer)
    {
        foreach (HitTestResult hit in View.FindHits(pointer))
        {
            if (hit is LineHitTestResult lineHit && hit.ModelHit is Element3D element && _hoverable.TryGetValue(element, out SceneLine? line))
            {
                int vertex = lineHit.LineIndex + (lineHit.LineHitPointScalar > 0.5 ? 1 : 0);
                ShowHover(line.Describe!(Math.Clamp(vertex, 0, line.Positions.Count - 1)), pointer);
                return;
            }
        }

        HoverCard.Visibility = Visibility.Collapsed;
    }

    private void OnViewMouseLeave(object sender, MouseEventArgs e)
    {
        _hover.Cancel();
        HoverCard.Visibility = Visibility.Collapsed;
    }

    /// <summary>Places the card beside the pointer, flipping to the other side near the edges.</summary>
    private void ShowHover(string text, Point pointer)
    {
        HoverText.Text = text;
        HoverCard.Visibility = Visibility.Visible;
        HoverCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Size size = HoverCard.DesiredSize;
        double left = pointer.X + 16 + size.Width > ActualWidth ? pointer.X - 16 - size.Width : pointer.X + 16;
        double top = pointer.Y + 16 + size.Height > ActualHeight ? pointer.Y - 16 - size.Height : pointer.Y + 16;
        Canvas.SetLeft(HoverCard, Math.Max(0, left));
        Canvas.SetTop(HoverCard, Math.Max(0, top));
    }

    private static Color4 ToColor4(Color colour) => new(colour.R / 255f, colour.G / 255f, colour.B / 255f, colour.A / 255f);

    /// <summary>
    /// What a kept line was built from. The lists compare by reference, so only the very same arrays
    /// match, and Occurrence tells apart two lines that happen to share them.
    /// </summary>
    private readonly record struct KeptLine(IReadOnlyList<Vector3> Positions, IReadOnlyList<Color>? VertexColours, bool IsOverlay, int Occurrence);
}
