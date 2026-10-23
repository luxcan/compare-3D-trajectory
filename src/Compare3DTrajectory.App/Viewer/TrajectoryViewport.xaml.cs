using System.Numerics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
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
    private DefaultEffectsManager? _effects;
    private DateTime _lastHover;

    public TrajectoryViewport() => InitializeComponent();

    public SceneModel Scene
    {
        get => (SceneModel)GetValue(SceneProperty);
        set => SetValue(SceneProperty, value);
    }

    /// <summary>
    /// A whole route is long and flat, so its bounding sphere is far bigger than what the camera sees
    /// of it from above at an angle; coming in to this share of the sphere's distance fills the view
    /// without cutting off the ends.
    /// </summary>
    private const double FitAllCloseness = 0.75;

    /// <summary>Frames everything drawn, keeping the current viewing direction.</summary>
    public void FitAll()
    {
        if (BoundsOf(Scene.Lines.Where(line => !line.IsSegments).SelectMany(line => line.Positions)) is { } bounds)
        {
            Frame(bounds, FitAllCloseness);
        }
    }

    /// <summary>Frames one box, given in render coordinates.</summary>
    public void FocusOn(Vector3 min, Vector3 max)
    {
        if (BoundsOf([min, max]) is { } bounds)
        {
            Frame(bounds, 1);
        }
    }

    /// <summary>
    /// Moves the camera so the box's bounding sphere just fits the narrower of the two fields of view,
    /// looking the same way as before with Z still up. HelixToolkit's own ZoomExtents also turned the
    /// camera, which lost the user's chosen angle every time an issue was picked.
    /// </summary>
    private void Frame(Rect3D bounds, double closeness)
    {
        if (View.Camera is not ProjectionCamera camera)
        {
            return;
        }

        var centre = new Point3D(bounds.X + (bounds.SizeX / 2), bounds.Y + (bounds.SizeY / 2), bounds.Z + (bounds.SizeZ / 2));
        double radius = Math.Max(1, 0.5 * Math.Sqrt((bounds.SizeX * bounds.SizeX) + (bounds.SizeY * bounds.SizeY) + (bounds.SizeZ * bounds.SizeZ)));
        // Direct3D's convention: the camera's field of view is the vertical one.
        double vertical = (camera is PerspectiveCamera perspective ? perspective.FieldOfView : 45) * Math.PI / 180;
        double widthOverHeight = View.ActualWidth > 0 && View.ActualHeight > 0 ? View.ActualWidth / View.ActualHeight : 1;
        double horizontal = 2 * Math.Atan(Math.Tan(vertical / 2) * widthOverHeight);
        double distance = closeness * radius / Math.Sin(Math.Min(horizontal, vertical) / 2);

        Vector3D direction = camera.LookDirection;
        direction.Normalize();
        camera.AnimateTo(centre - (direction * distance), direction * distance, new Vector3D(0, 0, 1), ZoomMilliseconds);
    }

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

    private void Rebuild()
    {
        SceneRoot.Children.Clear();
        _hoverable.Clear();
        HoverCard.Visibility = Visibility.Collapsed;
        SceneModel scene = Scene;

        // A line needs two positions; the grid has none when the scene's extent is too large to draw one.
        foreach (SceneLine line in scene.Lines.Where(line => line.Positions.Count >= 2))
        {
            var model = new LineGeometryModel3D
            {
                Geometry = LineGeometry(line),
                Color = line.VertexColours is null ? line.Colour : Colors.White,
                Thickness = line.Thickness,
                Smoothness = 0.6,
                IsHitTestVisible = line.Describe is not null,
                HitTestThickness = HoverHitThickness,
                DepthBias = line.IsOverlay ? OverlayDepthBias : 0,
            };
            SceneRoot.Children.Add(model);
            if (line.Describe is not null)
            {
                _hoverable[model] = line;
            }
        }

        foreach (SceneMarker marker in scene.Markers)
        {
            SceneRoot.Children.Add(new PointGeometryModel3D
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

            SceneRoot.Children.Add(new BillboardTextModel3D { Geometry = text, FixedSize = true, IsHitTestVisible = false });
        }
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
            HoverCard.Visibility = Visibility.Collapsed;
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (now - _lastHover < HoverInterval)
        {
            return;
        }

        _lastHover = now;
        Point pointer = e.GetPosition(View);
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

    private void OnViewMouseLeave(object sender, MouseEventArgs e) => HoverCard.Visibility = Visibility.Collapsed;

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

    private static Rect3D? BoundsOf(IEnumerable<Vector3> positions)
    {
        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);
        bool any = false;
        foreach (Vector3 position in positions)
        {
            min = Vector3.Min(min, position);
            max = Vector3.Max(max, position);
            any = true;
        }

        if (!any)
        {
            return null;
        }

        Vector3 size = max - min;
        return new Rect3D(min.X, min.Y, min.Z, size.X, size.Y, size.Z);
    }

    private static Color4 ToColor4(Color colour) => new(colour.R / 255f, colour.G / 255f, colour.B / 255f, colour.A / 255f);
}
