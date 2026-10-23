using System.Globalization;

namespace Compare3DTrajectory.App.Formatting;

/// <summary>Distances and angles as people read them: fewer decimals as the numbers grow.</summary>
public static class Measure
{
    public static string Metres(double metres)
    {
        double size = Math.Abs(metres);
        string format = size < 10 ? "0.00" : size < 1000 ? "0.0" : "#,0";
        return metres.ToString(format, CultureInfo.CurrentCulture) + " m";
    }

    public static string Degrees(double degrees) => degrees.ToString("0.0", CultureInfo.CurrentCulture) + "°";

    public static string Percent(double percent) => percent.ToString("0.#", CultureInfo.CurrentCulture) + "%";

    public static string Count(int count) => count.ToString("#,0", CultureInfo.CurrentCulture);

    /// <summary>"3 issues", "1 issue".</summary>
    public static string Plural(int count, string singular) =>
        $"{Count(count)} {singular}{(count == 1 ? string.Empty : "s")}";

    /// <summary>Parses what someone typed, in their own format first and the invariant one second.</summary>
    public static bool TryParseNumber(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
