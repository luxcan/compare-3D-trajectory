using System.Globalization;
using System.Text;
using Compare3DTrajectory.Core.Geo;
using Compare3DTrajectory.Core.Geometry;
using Compare3DTrajectory.Core.Tests.Loading;
using Compare3DTrajectory.Core.Tests.Synthetic;

namespace Compare3DTrajectory.Core.Tests.Samples;

/// <summary>
/// The files in the repository's samples folder: one model and four recorded runs of the standard
/// route, in geocentric metres near Singapore, each with a known fault planted, and each written in a
/// different layout so that opening them exercises the reader too.
/// </summary>
internal static class SampleScenarios
{
    public const string Model = "model.csv";
    public const string Good = "run-good.csv";
    public const string Detour = "run-detour.csv";
    public const string CutCorner = "run-cut-corner.tsv";
    public const string MovedAndTurned = "run-moved-and-turned.xlsx";

    /// <summary>The move planted in <see cref="MovedAndTurned"/>: 35° anticlockwise, then 400 m east, 250 m south, 3 m up.</summary>
    public const double PlantedTurnDegrees = 35;

    public static readonly Vec3 PlantedOffset = new(400, -250, 3);

    private static readonly LocalFrame Frame = new(Wgs84.ToEcef(new GeodeticPosition(1.3521, 103.8198, 30)));

    public static void WriteAll(string folder)
    {
        Route route = Route.Standard();
        List<Vec3> recorded = Noise.Jitter(route.Sample(1), sigma: 0.3, seed: 42);

        WriteDelimited(Path.Combine(folder, Model), ",", ["x", "y", "z"], route.Sample(10), time: false);
        WriteDelimited(Path.Combine(folder, Good), ",", ["PositionX", "PositionY", "PositionZ"], recorded, time: false);

        List<Vec3> detour = Noise.Jitter(
            route.SampleWith(1, (s, position, heading) => Route.Detour(s, position, heading, 500, 600, 25)),
            sigma: 0.3,
            seed: 43);
        WriteDelimited(
            Path.Combine(folder, Detour),
            ";",
            ["Time", "WorldLocation.X", "WorldLocation.Y", "WorldLocation.Z"],
            detour,
            time: true);

        (Vec3 cutFrom, _) = route.At(1220);
        (Vec3 cutTo, _) = route.At(1385);
        List<Vec3> cut = Noise.Jitter(
            route.SampleWith(1, (s, position, _) => s is > 1220 and < 1385 ? Vec3.Lerp(cutFrom, cutTo, (s - 1220) / 165) : position),
            sigma: 0.3,
            seed: 44);
        WriteDelimited(Path.Combine(folder, CutCorner), "\t", ["pos_x", "pos_y", "pos_z"], cut, time: false);

        List<Vec3> moved = Noise.Move(recorded, PlantedTurnDegrees, PlantedOffset);
        object?[]?[] rows =
        [
            ["WorldLocation.X", "WorldLocation.Y", "WorldLocation.Z"],
            .. moved.Select(point => Frame.ToEcef(point)).Select(point => new object?[] { point.X, point.Y, point.Z }),
        ];
        ExcelFixture.Write(Path.Combine(folder, MovedAndTurned), ("Recording", rows));
    }

    /// <summary>Writes East-North-Up points as geocentric metres, optionally after a time column at 15 m/s.</summary>
    private static void WriteDelimited(string path, string delimiter, string[]? header, List<Vec3> enuPoints, bool time)
    {
        var text = new StringBuilder();
        if (header is not null)
        {
            text.AppendLine(string.Join(delimiter, header));
        }

        for (int i = 0; i < enuPoints.Count; i++)
        {
            Vec3 ecef = Frame.ToEcef(enuPoints[i]);
            var cells = new List<string>();
            if (time)
            {
                cells.Add((i / 15.0).ToString("0.###", CultureInfo.InvariantCulture));
            }

            cells.Add(Format(ecef.X));
            cells.Add(Format(ecef.Y));
            cells.Add(Format(ecef.Z));
            text.AppendLine(string.Join(delimiter, cells));
        }

        File.WriteAllText(path, text.ToString());
    }

    /// <summary>Millimetres are as fine as any simulation logs a position.</summary>
    private static string Format(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);
}
