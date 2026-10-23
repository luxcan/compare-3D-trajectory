using System.Globalization;
using System.Text;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Compare3DTrajectory.App.Formatting;
using Compare3DTrajectory.Core.Comparison;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Loading;

namespace Compare3DTrajectory.App.Files;

/// <summary>One loaded trajectory: a row in the file list and a line in the 3D view.</summary>
public sealed partial class TrajectoryItemViewModel : ObservableObject
{
    private const int SkippedLinesShown = 3;

    public TrajectoryItemViewModel(LoadedTrajectory loaded, Color colour)
    {
        ArgumentNullException.ThrowIfNull(loaded);
        Loaded = loaded;
        Colour = colour;
        ColourBrush = new SolidColorBrush(colour);
        ColourBrush.Freeze();
        _kind = loaded.DetectedKind;
        _verdictText = "Not compared yet";
    }

    /// <summary>Identifies the trajectory to the comparison engine's cache.</summary>
    public Guid Id { get; } = Guid.NewGuid();

    public LoadedTrajectory Loaded { get; }

    public string Name => Loaded.Name;

    public string SourcePath => Loaded.SourcePath;

    public Color Colour { get; }

    public SolidColorBrush ColourBrush { get; }

    /// <summary>The trajectory in working metres; replaced when <see cref="Kind"/> changes.</summary>
    public PreparedTrajectory? Prepared { get; set; }

    /// <summary>How the file's x, y, z are read; detected on load and changeable from the row's menu.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DetailText), nameof(KindMenuText))]
    private CoordinateKind _kind;

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isReference;

    /// <summary>This run against the reference; null for the reference itself and before comparing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCompared))]
    private ComparisonResult? _result;

    [ObservableProperty]
    private string _verdictText;

    [ObservableProperty]
    private bool _hasIssues;

    [ObservableProperty]
    private string? _figuresText;

    [ObservableProperty]
    private string? _alignmentText;

    public bool IsCompared => Result is not null;

    /// <summary>Points, coordinate kind, columns used and any rows skipped.</summary>
    public string DetailText
    {
        get
        {
            var text = new StringBuilder();
            text.Append(CultureInfo.CurrentCulture, $"{Measure.Count(Loaded.Points.Count)} points · ");
            text.Append(Kind == CoordinateKind.Geocentric ? "geocentric (ECEF)" : "local x, y, z");
            text.Append(CultureInfo.CurrentCulture, $" · {Loaded.ColumnDescription}");
            if (Loaded.SkippedRows.Count > 0)
            {
                IEnumerable<string> lines = Loaded.SkippedRows.Take(SkippedLinesShown)
                    .Select(row => row.LineNumber.ToString(CultureInfo.CurrentCulture));
                string more = Loaded.SkippedRows.Count > SkippedLinesShown ? ", …" : string.Empty;
                text.Append(CultureInfo.CurrentCulture, $" · skipped {Measure.Plural(Loaded.SkippedRows.Count, "row")} (lines {string.Join(", ", lines)}{more})");
            }

            if (Loaded.Note is not null)
            {
                text.Append(CultureInfo.CurrentCulture, $" · {Loaded.Note}");
            }

            return text.ToString();
        }
    }

    /// <summary>Every skipped row with its reason, for the detail line's tooltip.</summary>
    public string? SkippedRowsToolTip => Loaded.SkippedRows.Count == 0
        ? null
        : string.Join(Environment.NewLine, Loaded.SkippedRows.Select(row => $"Line {row.LineNumber}: {row.Reason}"));

    public string KindMenuText => Kind == CoordinateKind.Geocentric
        ? "Treat as local x, y, z (metres, Z up)"
        : "Treat as geocentric (ECEF, HLA WorldLocation)";

    /// <summary>Why the last comparison of this run failed; null when it did not.</summary>
    public string? ComparisonFailure { get; private set; }

    /// <summary>Shows this row as the one the others are measured against.</summary>
    public void ShowAsReference()
    {
        Result = null;
        ComparisonFailure = null;
        VerdictText = "Reference";
        HasIssues = false;
        FiguresText = null;
        AlignmentText = null;
    }

    public void ShowResult(ComparisonResult result, double tolerance)
    {
        ArgumentNullException.ThrowIfNull(result);
        Result = result;
        ComparisonFailure = null;
        VerdictText = ResultText.Verdict(result, tolerance);
        HasIssues = result.Issues.Count > 0;
        FiguresText = ResultText.Figures(result.Summary);
        AlignmentText = ResultText.Alignment(result.Fit);
    }

    public void ShowNotCompared(string reason)
    {
        Result = null;
        ComparisonFailure = null;
        VerdictText = reason;
        HasIssues = false;
        FiguresText = null;
        AlignmentText = null;
    }

    /// <summary>Shows that this run, and only this run, could not be compared, and why.</summary>
    public void ShowFailure(string reason)
    {
        ShowNotCompared($"Comparison failed: {reason}");
        ComparisonFailure = reason;
    }

    /// <summary>The points to draw: after the fit once compared, as loaded before that.</summary>
    public IReadOnlyList<Vec3> DrawnPoints => Result?.AlignedPoints ?? Prepared?.Points ?? [];

    /// <summary>The hover text for one point of this trajectory.</summary>
    public string DescribePoint(int index)
    {
        Vec3 original = Loaded.Points[index];
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.CurrentCulture, $"{Name} · point {Measure.Count(index + 1)} (line {Loaded.LineNumbers[index]})");
        if (Result is not null)
        {
            text.AppendLine(CultureInfo.CurrentCulture, $"Off the reference by {Measure.Metres(Result.Deviations[index])}");
        }

        text.Append(CultureInfo.CurrentCulture, $"x, y, z in file: {original.X:0.###}, {original.Y:0.###}, {original.Z:0.###}");
        if (Kind == CoordinateKind.Geocentric)
        {
            GeodeticPosition position = Wgs84.ToGeodetic(original);
            text.AppendLine();
            text.Append(CultureInfo.CurrentCulture, $"Lat {position.LatitudeDegrees:0.000000}°, lon {position.LongitudeDegrees:0.000000}°, height {Measure.Metres(position.Height)}");
        }

        return text.ToString();
    }
}
