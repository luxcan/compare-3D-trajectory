using Compare3DTrajectory.Core.Geometry;

namespace Compare3DTrajectory.Core.Geo;

/// <summary>
/// A flat East-North-Up frame in metres, tangent to the WGS84 ellipsoid at an origin.
/// </summary>
/// <remarks>
/// Every comparison happens in this frame. Up is a fixed axis, so "turn only around the vertical" is
/// a rotation about Z. The numbers also stay small near the origin, which keeps millimetres when
/// they are handed to the GPU as floats.
/// </remarks>
public sealed class LocalFrame
{
    private readonly Vec3 _east;
    private readonly Vec3 _north;
    private readonly Vec3 _up;

    public LocalFrame(Vec3 originEcef)
    {
        if (!originEcef.IsFinite)
        {
            throw new ArgumentException("The origin must be a finite position.", nameof(originEcef));
        }

        OriginEcef = originEcef;
        Origin = Wgs84.ToGeodetic(originEcef);

        double latitude = Origin.LatitudeDegrees * Math.PI / 180;
        double longitude = Origin.LongitudeDegrees * Math.PI / 180;
        double sinLatitude = Math.Sin(latitude);
        double cosLatitude = Math.Cos(latitude);
        double sinLongitude = Math.Sin(longitude);
        double cosLongitude = Math.Cos(longitude);

        _east = new Vec3(-sinLongitude, cosLongitude, 0);
        _north = new Vec3(-sinLatitude * cosLongitude, -sinLatitude * sinLongitude, cosLatitude);
        _up = new Vec3(cosLatitude * cosLongitude, cosLatitude * sinLongitude, sinLatitude);
    }

    public Vec3 OriginEcef { get; }

    public GeodeticPosition Origin { get; }

    /// <summary>East, North and Up metres from the origin.</summary>
    public Vec3 ToLocal(Vec3 ecef)
    {
        Vec3 offset = ecef - OriginEcef;
        return new Vec3(offset.Dot(_east), offset.Dot(_north), offset.Dot(_up));
    }

    public Vec3 ToEcef(Vec3 local) => OriginEcef + (_east * local.X) + (_north * local.Y) + (_up * local.Z);
}
