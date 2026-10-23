using System.Globalization;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Loading;

/// <summary>
/// Finds the rows that cannot be where anything was, so they are skipped and listed by line rather
/// than drawn, fitted and compared: values too large for any position, no-data markers, geocentric
/// points nowhere near the Earth's surface, and glitch rows that jump off the path and straight back.
/// </summary>
/// <remarks>
/// One such row used to spoil a whole file: it made a geocentric file local, took over the heading
/// fit, made every nearest-point query scan the whole path, or overflowed the 3D view.
/// </remarks>
internal static class PositionScreen
{
    /// <summary>100,000 km: farther out than this is no position on or around the Earth, in any frame.</summary>
    private const double MaximumCoordinate = 1e8;

    /// <summary>Below the floor of the deepest ocean trench, with room for the geoid.</summary>
    private const double MinimumGeocentricHeight = -12_000;

    /// <summary>Glitches are only looked for along a path long enough to have a typical step.</summary>
    private const int MinimumPointsForGlitches = 10;

    /// <summary>A step this many times the typical one, that comes straight back, is a glitch.</summary>
    private const double GlitchSteps = 20;

    /// <summary>
    /// A step this many times the typical one at the very start or end, into a path that then goes on
    /// as usual, is a row from before or after the run, such as where the entity was spawned. It is
    /// set high so that an entity that waits and then drives off is not mistaken for one.
    /// </summary>
    private const double StrayEndSteps = 100;

    /// <summary>The most rows one glitch may span.</summary>
    private const int LongestGlitch = 5;

    /// <summary>How near its start a glitch must come back to, in typical steps per row it spans.</summary>
    private const double ReturnSteps = 5;

    private static readonly HashSet<double> NoDataMarkers = [-999_999, -99_999, -9_999, -999, 999, 9_999, 99_999, 999_999];

    /// <summary>The rows kept, the rows skipped with why, and whether the kept rows are geocentric.</summary>
    public static Screened Screen(IReadOnlyList<Vec3> points, IReadOnlyList<int> lineNumbers)
    {
        ArgumentNullException.ThrowIfNull(points);
        ArgumentNullException.ThrowIfNull(lineNumbers);

        var reasons = new string?[points.Count];
        for (int i = 0; i < points.Count; i++)
        {
            reasons[i] = TooLarge(points[i]) ?? NoData(points[i]);
        }

        CoordinateKind kind = CoordinateKindDetector.Detect([.. Plausible(points, reasons).Select(i => points[i])]);
        if (kind == CoordinateKind.Geocentric)
        {
            foreach (int i in Plausible(points, reasons))
            {
                reasons[i] = OffTheEarth(points[i]);
            }
        }

        MarkGlitches(points, [.. Plausible(points, reasons)], reasons);

        var kept = new List<Vec3>(points.Count);
        var keptLines = new List<int>(points.Count);
        var skipped = new List<SkippedRow>();
        for (int i = 0; i < points.Count; i++)
        {
            if (reasons[i] is { } reason)
            {
                skipped.Add(new SkippedRow(lineNumbers[i], reason));
            }
            else
            {
                kept.Add(points[i]);
                keptLines.Add(lineNumbers[i]);
            }
        }

        return new Screened(kept, keptLines, skipped, kind);
    }

    private static IEnumerable<int> Plausible(IReadOnlyList<Vec3> points, string?[] reasons) =>
        Enumerable.Range(0, points.Count).Where(i => reasons[i] is null);

    /// <summary>An unset value as C++ (DBL_MAX) or a float (FLT_MAX) writes it, or anything of that size.</summary>
    private static string? TooLarge(Vec3 point)
    {
        foreach (double value in (double[])[point.X, point.Y, point.Z])
        {
            if (Math.Abs(value) > MaximumCoordinate)
            {
                return $"{Number(value)} is too large to be a position";
            }
        }

        return null;
    }

    private static string? NoData(Vec3 point) =>
        point.X == point.Y && point.Y == point.Z && NoDataMarkers.Contains(point.X)
            ? $"{Coordinates(point)} is a no-data marker, not a position"
            : null;

    private static string? OffTheEarth(Vec3 point)
    {
        if (!CoordinateKindDetector.IsInShell(point))
        {
            return $"{Coordinates(point)} is {Metres(point.Length / 1000)} km from the Earth's centre, nowhere near its surface";
        }

        double height = Wgs84.ToGeodetic(point).Height;
        return height < MinimumGeocentricHeight
            ? $"{Coordinates(point)} is {Metres(-height)} m below the WGS84 ellipsoid, deeper than any ground or sea"
            : null;
    }

    /// <summary>
    /// Marks glitches among the plausible points, taken in file order: a short burst of rows that
    /// leaves the path by a long jump and comes straight back near where it left, and a stray row or
    /// two at either end, joined to the rest by one very long step.
    /// </summary>
    private static void MarkGlitches(IReadOnlyList<Vec3> points, List<int> path, string?[] reasons)
    {
        int count = path.Count;
        if (count < MinimumPointsForGlitches)
        {
            return;
        }

        double[] steps = [.. Enumerable.Range(0, count - 1).Select(k => points[path[k]].DistanceTo(points[path[k + 1]]))];
        double[] moving = [.. steps.Where(step => step > 0).Order()];
        if (moving.Length == 0)
        {
            return;
        }

        double typical = moving[moving.Length / 2];
        int left = 0;
        while (left < count - 1)
        {
            int last = steps[left] > GlitchSteps * typical ? LastRowAway(points, path, left, typical) : -1;
            if (last < 0)
            {
                left++;
                continue;
            }

            Vec3 from = points[path[left]];
            Vec3 back = points[path[last + 1]];
            for (int k = left + 1; k <= last; k++)
            {
                Vec3 point = points[path[k]];
                double off = point.DistanceTo(Polyline.ClosestPointOnSegment(point, from, back, out _));
                reasons[path[k]] = $"jumps {Metres(off)} m off the path and straight back";
            }

            left = last + 1;
        }

        MarkStrayEnd(points, path, steps, typical, reasons, fromStart: true);
        MarkStrayEnd(points, path, steps, typical, reasons, fromStart: false);
    }

    /// <summary>
    /// After a long jump away from row <paramref name="left"/>, the last row before the path comes
    /// back near it; -1 when it does not come back within <see cref="LongestGlitch"/> rows, as after a
    /// gap in the recording, where the path simply goes on from further along.
    /// </summary>
    private static int LastRowAway(IReadOnlyList<Vec3> points, List<int> path, int left, double typical)
    {
        Vec3 from = points[path[left]];
        for (int last = left + 1; last <= Math.Min(left + LongestGlitch, path.Count - 2); last++)
        {
            if (points[path[last + 1]].DistanceTo(from) <= (last - left + 1) * ReturnSteps * typical)
            {
                return last;
            }
        }

        return -1;
    }

    private static void MarkStrayEnd(IReadOnlyList<Vec3> points, List<int> path, double[] steps, double typical, string?[] reasons, bool fromStart)
    {
        int count = path.Count;
        for (int stray = 1; stray <= LongestGlitch; stray++)
        {
            int jump = fromStart ? stray - 1 : count - 1 - stray;
            if (steps[jump] <= StrayEndSteps * typical)
            {
                continue;
            }

            IEnumerable<int> after = fromStart
                ? Enumerable.Range(jump + 1, Math.Min(LongestGlitch, count - 2 - jump))
                : Enumerable.Range(Math.Max(0, jump - LongestGlitch), Math.Min(LongestGlitch, jump));
            if (after.Any(k => steps[k] > GlitchSteps * typical))
            {
                return;
            }

            Vec3 end = points[path[fromStart ? stray : count - 1 - stray]];
            string where = fromStart ? "starts" : "ends";
            for (int k = 0; k < stray; k++)
            {
                int index = path[fromStart ? k : count - 1 - k];
                reasons[index] ??= $"is {Metres(points[index].DistanceTo(end))} m from where the path {where}";
            }

            return;
        }
    }

    private static string Coordinates(Vec3 point) => $"{Number(point.X)}, {Number(point.Y)}, {Number(point.Z)}";

    private static string Number(double value) => value.ToString("G6", CultureInfo.InvariantCulture);

    private static string Metres(double value) => value.ToString("#,0", CultureInfo.InvariantCulture);

    /// <param name="Points">The rows kept, in file order.</param>
    /// <param name="LineNumbers">The line each kept row came from.</param>
    /// <param name="Skipped">The rows that cannot be a position, with why.</param>
    /// <param name="Kind">Whether the kept rows are geocentric or local.</param>
    public sealed record Screened(IReadOnlyList<Vec3> Points, IReadOnlyList<int> LineNumbers, IReadOnlyList<SkippedRow> Skipped, CoordinateKind Kind);
}
