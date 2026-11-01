using EcoStream.Domain.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

namespace EcoStream.Infrastructure.Persistence.Configurations;

internal static class GeoPointConversion
{
    public const int Wgs84 = 4326;

    /// <summary>Maps the domain <see cref="GeoPoint"/> to a PostGIS geography point (distances in meters).</summary>
    public static PropertyBuilder<GeoPoint> IsGeographyPoint(this PropertyBuilder<GeoPoint> property) =>
        property
            .HasColumnType("geography(Point,4326)")
            .HasConversion(
                g => new Point(g.Longitude, g.Latitude) { SRID = Wgs84 },
                p => new GeoPoint(p.Y, p.X));
}
