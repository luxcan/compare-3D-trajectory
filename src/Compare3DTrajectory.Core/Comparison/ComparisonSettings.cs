namespace Compare3DTrajectory.Core.Comparison;

/// <summary>What counts as "on the path".</summary>
public sealed record ComparisonSettings
{
    public ComparisonSettings(double tolerance)
    {
        if (!(tolerance > 0) || !double.IsFinite(tolerance))
        {
            throw new ArgumentOutOfRangeException(nameof(tolerance), tolerance, "Tolerance must be a positive number of metres.");
        }

        Tolerance = tolerance;
    }

    /// <summary>How far, in metres, a point may be from the other path and still be on it.</summary>
    public double Tolerance { get; }
}
