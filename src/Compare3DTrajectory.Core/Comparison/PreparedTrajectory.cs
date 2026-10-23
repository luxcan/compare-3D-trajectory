using Compare3DTrajectory.Core.Alignment;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// A trajectory in working metres, ready to be compared, with its search index built on first use.
/// </summary>
/// <remarks>
/// <para>The index is built once and shared, because a reference is searched by every run compared
/// against it, possibly from several threads at once.</para>
///
/// <para>A run is fitted in its own level frame, given as <c>levelPoints</c>: East-North-Up at its own
/// first point when it is geocentric. In the reference's frame a run recorded tens of kilometres away
/// lies tilted by the curve of the Earth, which a fit that only turns about the vertical cannot undo.
/// Without level points the working points serve for both.</para>
/// </remarks>
public sealed class PreparedTrajectory
{
    private readonly Lazy<PolylineIndex> _index;

    public PreparedTrajectory(Guid id, IReadOnlyList<Vec3> workingPoints, IReadOnlyList<Vec3>? levelPoints = null)
    {
        ArgumentNullException.ThrowIfNull(workingPoints);
        Id = id;
        Polyline = new Polyline(workingPoints);
        if (levelPoints is null)
        {
            Level = Polyline;
        }
        else
        {
            if (levelPoints.Count != workingPoints.Count)
            {
                throw new ArgumentException("There must be one level point for every working point.", nameof(levelPoints));
            }

            Level = new Polyline(levelPoints);
            AsRecorded = HeadingTransform.FitPairs(levelPoints, workingPoints);
        }

        _index = new Lazy<PolylineIndex>(() => new PolylineIndex(Polyline), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Identifies the trajectory across reloads of its settings, and in the window's rows.</summary>
    public Guid Id { get; }

    public IReadOnlyList<Vec3> Points => Polyline.Points;

    public Polyline Polyline { get; }

    /// <summary>The trajectory in its own level frame, for fitting; the working points when it has none of its own.</summary>
    public Polyline Level { get; }

    /// <summary>Carries <see cref="Level"/> back onto the working points: the run exactly as recorded.</summary>
    public HeadingTransform AsRecorded { get; }

    public PolylineIndex Index => _index.Value;
}
