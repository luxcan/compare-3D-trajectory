using System.Numerics;

namespace Compare3DTrajectory.App.Viewer;

/// <summary>The box round render positions; with nothing in it, Min lies above Max.</summary>
public readonly record struct RenderBox(Vector3 Min, Vector3 Max)
{
    public static RenderBox Empty { get; } = new(new Vector3(float.MaxValue), new Vector3(float.MinValue));

    public bool IsEmpty => !(Min.X <= Max.X);

    /// <summary>
    /// The box round the finite positions only: a point beyond the range of float would make it
    /// endless, and the ground grid with it.
    /// </summary>
    public static RenderBox OfFinite(IEnumerable<Vector3> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        Vector3 min = Empty.Min;
        Vector3 max = Empty.Max;
        foreach (Vector3 position in positions)
        {
            if (float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z))
            {
                min = Vector3.Min(min, position);
                max = Vector3.Max(max, position);
            }
        }

        return new RenderBox(min, max);
    }

    public RenderBox Including(RenderBox other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));
}
