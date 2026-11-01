using EcoStream.Domain.Common;

namespace EcoStream.Domain.Geo;

/// <summary>WGS84 coordinate (SRID 4326). Kept free of NetTopologySuite so the domain stays persistence-agnostic.</summary>
public sealed record GeoPoint
{
    public GeoPoint(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90) throw new DomainException($"Invalid latitude: {latitude}");
        if (longitude is < -180 or > 180) throw new DomainException($"Invalid longitude: {longitude}");
        Latitude = latitude;
        Longitude = longitude;
    }

    public double Latitude { get; }
    public double Longitude { get; }
}
