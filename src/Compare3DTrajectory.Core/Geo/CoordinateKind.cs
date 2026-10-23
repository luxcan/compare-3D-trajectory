namespace Compare3DTrajectory.Core.Geo;

/// <summary>How a file's x, y, z are to be read.</summary>
public enum CoordinateKind
{
    /// <summary>Plain metres with Z up, used as they are.</summary>
    Local,

    /// <summary>Earth-centred, Earth-fixed metres (HLA WorldLocation), converted to East-North-Up.</summary>
    Geocentric,
}
