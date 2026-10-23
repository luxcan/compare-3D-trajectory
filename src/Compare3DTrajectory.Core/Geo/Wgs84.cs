using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Geo;

/// <summary>
/// Conversions between geocentric (ECEF) metres and latitude, longitude and height on the WGS84
/// ellipsoid. HLA's RPR FOM carries an entity's WorldLocation as geocentric metres.
/// </summary>
public static class Wgs84
{
    public const double SemiMajorAxis = 6_378_137.0;
    public const double Flattening = 1 / 298.257223563;
    public const double EccentricitySquared = Flattening * (2 - Flattening);

    private const double DegreesPerRadian = 180 / Math.PI;

    public static Vec3 ToEcef(GeodeticPosition position)
    {
        double latitude = position.LatitudeDegrees / DegreesPerRadian;
        double longitude = position.LongitudeDegrees / DegreesPerRadian;
        double sinLatitude = Math.Sin(latitude);
        double primeVerticalRadius = SemiMajorAxis / Math.Sqrt(1 - (EccentricitySquared * sinLatitude * sinLatitude));
        double horizontal = (primeVerticalRadius + position.Height) * Math.Cos(latitude);

        return new Vec3(
            horizontal * Math.Cos(longitude),
            horizontal * Math.Sin(longitude),
            ((primeVerticalRadius * (1 - EccentricitySquared)) + position.Height) * sinLatitude);
    }

    /// <summary>
    /// The latitude, longitude and height of a geocentric point, by fixed-point iteration on the
    /// latitude. It converges to well below a millimetre in a few steps anywhere near the surface.
    /// </summary>
    public static GeodeticPosition ToGeodetic(Vec3 ecef)
    {
        double longitude = Math.Atan2(ecef.Y, ecef.X);
        double horizontal = Math.Sqrt((ecef.X * ecef.X) + (ecef.Y * ecef.Y));
        double semiMinorAxis = SemiMajorAxis * (1 - Flattening);

        if (horizontal < 1e-9)
        {
            double pole = ecef.Z >= 0 ? 90 : -90;
            return new GeodeticPosition(pole, 0, Math.Abs(ecef.Z) - semiMinorAxis);
        }

        double latitude = Math.Atan2(ecef.Z, horizontal * (1 - EccentricitySquared));
        double height = 0;
        for (int iteration = 0; iteration < 20; iteration++)
        {
            double sinLatitude = Math.Sin(latitude);
            double primeVerticalRadius = SemiMajorAxis / Math.Sqrt(1 - (EccentricitySquared * sinLatitude * sinLatitude));
            height = HeightAt(latitude, horizontal, ecef.Z, primeVerticalRadius);
            double next = Math.Atan2(
                ecef.Z,
                horizontal * (1 - (EccentricitySquared * primeVerticalRadius / (primeVerticalRadius + height))));
            bool converged = Math.Abs(next - latitude) < 1e-14;
            latitude = next;
            if (converged)
            {
                break;
            }
        }

        double finalSin = Math.Sin(latitude);
        double finalRadius = SemiMajorAxis / Math.Sqrt(1 - (EccentricitySquared * finalSin * finalSin));
        height = HeightAt(latitude, horizontal, ecef.Z, finalRadius);

        return new GeodeticPosition(latitude * DegreesPerRadian, longitude * DegreesPerRadian, height);
    }

    /// <summary>
    /// Height from whichever of cos and sin is larger, so it stays accurate near the poles where
    /// cos(latitude) runs to zero.
    /// </summary>
    private static double HeightAt(double latitude, double horizontal, double z, double primeVerticalRadius)
    {
        double cosLatitude = Math.Cos(latitude);
        double sinLatitude = Math.Sin(latitude);
        return Math.Abs(cosLatitude) > Math.Abs(sinLatitude)
            ? (horizontal / cosLatitude) - primeVerticalRadius
            : (z / sinLatitude) - (primeVerticalRadius * (1 - EccentricitySquared));
    }
}
