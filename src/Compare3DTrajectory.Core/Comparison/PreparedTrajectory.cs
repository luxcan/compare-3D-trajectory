using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Comparison;

/// <summary>
/// A trajectory in working metres, ready to be compared, with its search index built on first use.
/// </summary>
/// <remarks>
/// The index is built once and shared, because a reference is searched by every run compared
/// against it, possibly from several threads at once.
/// </remarks>
public sealed class PreparedTrajectory
{
    private readonly Lazy<PolylineIndex> _index;

    public PreparedTrajectory(Guid id, IReadOnlyList<Vec3> workingPoints)
    {
        ArgumentNullException.ThrowIfNull(workingPoints);
        Id = id;
        Polyline = new Polyline(workingPoints);
        _index = new Lazy<PolylineIndex>(() => new PolylineIndex(Polyline), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>Identifies the trajectory across reloads of its settings, and keys the fit cache.</summary>
    public Guid Id { get; }

    public IReadOnlyList<Vec3> Points => Polyline.Points;

    public Polyline Polyline { get; }

    public PolylineIndex Index => _index.Value;
}
