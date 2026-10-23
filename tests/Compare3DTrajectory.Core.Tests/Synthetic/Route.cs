using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Tests.Synthetic;

/// <summary>
/// A made-up path in East-North-Up metres, built from straights and turns, that can be sampled at any
/// spacing and bent in known places, so a test knows exactly what the comparison ought to find.
/// </summary>
/// <remarks>
/// The default shape mixes left and right turns of different sizes, so it has no rotational symmetry
/// and its mirror image is a different path. Both matter for alignment tests.
/// </remarks>
internal sealed class Route
{
    private readonly List<Leg> _legs = [];

    public double Length => _legs.Sum(leg => leg.Length);

    /// <summary>Climbs gently with a bump, so the path is genuinely three-dimensional.</summary>
    public Func<double, double> Height { get; init; } = s => (0.04 * s) + (6 * Math.Sin(s / 120));

    public static Route Standard() => new Route()
        .Straight(400)
        .Turn(radius: 100, degrees: 90)
        .Straight(300)
        .Turn(radius: 80, degrees: -90)
        .Straight(250)
        .Turn(radius: 60, degrees: 135)
        .Straight(200);

    public Route Straight(double length)
    {
        _legs.Add(new Leg(length, 0));
        return this;
    }

    /// <summary>Positive degrees turn left (anticlockwise from above), negative turn right.</summary>
    public Route Turn(double radius, double degrees)
    {
        double radians = degrees * Math.PI / 180;
        _legs.Add(new Leg(radius * Math.Abs(radians), Math.Sign(radians) / radius));
        return this;
    }

    /// <summary>Where the path is <paramref name="s"/> metres along, and which way it is heading.</summary>
    public (Vec3 Position, double Heading) At(double s)
    {
        double x = 0;
        double y = 0;
        double heading = 0;
        double remaining = Math.Clamp(s, 0, Length);
        foreach (Leg leg in _legs)
        {
            double travelled = Math.Min(remaining, leg.Length);
            if (leg.Curvature == 0)
            {
                x += travelled * Math.Cos(heading);
                y += travelled * Math.Sin(heading);
            }
            else
            {
                double radius = 1 / leg.Curvature;
                double next = heading + (travelled * leg.Curvature);
                x += radius * (Math.Sin(next) - Math.Sin(heading));
                y -= radius * (Math.Cos(next) - Math.Cos(heading));
                heading = next;
            }

            remaining -= travelled;
            if (remaining <= 0)
            {
                break;
            }
        }

        return (new Vec3(x, y, Height(s)), heading);
    }

    /// <summary>Points every <paramref name="spacing"/> metres, the last one exactly at the end.</summary>
    public List<Vec3> Sample(double spacing) => SampleWith(spacing, (s, position, _) => position);

    /// <summary>
    /// Samples the path, letting <paramref name="shape"/> move each point: it is given the distance
    /// along, the true position and the heading there.
    /// </summary>
    public List<Vec3> SampleWith(double spacing, Func<double, Vec3, double, Vec3> shape)
    {
        var points = new List<Vec3>();
        int count = (int)Math.Ceiling(Length / spacing);
        for (int i = 0; i <= count; i++)
        {
            double s = Math.Min(i * spacing, Length);
            (Vec3 position, double heading) = At(s);
            points.Add(shape(s, position, heading));
        }

        return points;
    }

    /// <summary>
    /// Pushes the path sideways to the left by up to <paramref name="peak"/> metres between two
    /// distances along it, as a smooth half-sine: a detour that leaves and comes back.
    /// </summary>
    public static Vec3 Detour(double s, Vec3 position, double heading, double from, double to, double peak)
    {
        if (s <= from || s >= to)
        {
            return position;
        }

        double offset = peak * Math.Sin(Math.PI * (s - from) / (to - from));
        return position + new Vec3(-Math.Sin(heading), Math.Cos(heading), 0) * offset;
    }

    private readonly record struct Leg(double Length, double Curvature);
}
