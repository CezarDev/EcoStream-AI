using EcoStream.Application.Abstractions;
using EcoStream.Infrastructure.Persistence.Configurations;
using EcoStream.Infrastructure.Persistence.Geo;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace EcoStream.Infrastructure.Persistence.Queries;

internal sealed class StationQueries(EcoStreamDbContext db) : IStationQueries
{
    public async Task<IReadOnlyList<StationDto>> ListAsync(bool onlyActive, CancellationToken cancellationToken)
    {
        var stations = await db.Stations.AsNoTracking()
            .Where(s => !onlyActive || s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        return stations
            .Select(s => new StationDto(s.Code, s.Name, s.Location.Latitude, s.Location.Longitude, s.Provider.ToString(), s.IsActive))
            .ToList();
    }
}

internal sealed class TelemetryQueries(EcoStreamDbContext db) : ITelemetryQueries
{
    public async Task<IReadOnlyList<ReadingDto>> GetLatestPerStationAsync(CancellationToken cancellationToken) =>
        await db.TelemetryReadings.AsNoTracking()
            .GroupBy(r => r.StationCode)
            .Select(g => g.OrderByDescending(r => r.MeasuredAt)
                .Select(r => new ReadingDto(r.StationCode, r.MeasuredAt, r.RainfallMmPerHour, r.WindGustKmh, r.PressureHpa))
                .First())
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ReadingDto>> GetSeriesAsync(
        string stationCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken) =>
        await db.TelemetryReadings.AsNoTracking()
            .Where(r => r.StationCode == stationCode && r.MeasuredAt >= fromUtc && r.MeasuredAt < toUtc)
            .OrderBy(r => r.MeasuredAt)
            .Select(r => new ReadingDto(r.StationCode, r.MeasuredAt, r.RainfallMmPerHour, r.WindGustKmh, r.PressureHpa))
            .ToListAsync(cancellationToken);
}

internal sealed class GeoQueries(EcoStreamDbContext db) : IGeoQueries
{
    public async Task<IReadOnlyList<NeighborhoodProximityDto>> FindNeighborhoodsWithinAsync(
        double latitude, double longitude, double radiusKm, CancellationToken cancellationToken)
    {
        var origin = new Point(longitude, latitude) { SRID = GeoPointConversion.Wgs84 };
        var meters = radiusKm * 1000;

        // geography columns => ST_DWithin / ST_Distance in meters, GIST index used for the filter.
        return await db.Neighborhoods.AsNoTracking()
            .Where(n => n.Boundary.IsWithinDistance(origin, meters))
            .Select(n => new NeighborhoodProximityDto(
                n.Name,
                n.City,
                n.Population,
                n.Boundary.Distance(origin),
                db.RiskZones.Any(z => z.RiskType == RiskType.Flood && z.Boundary.Intersects(n.Boundary))))
            .OrderBy(x => x.DistanceMeters)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RiverProximityDto>> FindRiversWithinAsync(
        double latitude, double longitude, double radiusKm, CancellationToken cancellationToken)
    {
        var origin = new Point(longitude, latitude) { SRID = GeoPointConversion.Wgs84 };
        return await db.Rivers.AsNoTracking()
            .Where(r => r.Course.IsWithinDistance(origin, radiusKm * 1000))
            .Select(r => new RiverProximityDto(r.Name, r.Course.Distance(origin)))
            .OrderBy(x => x.DistanceMeters)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RiskZoneProximityDto>> FindRiskZonesWithinAsync(
        double latitude, double longitude, double radiusKm, CancellationToken cancellationToken)
    {
        var origin = new Point(longitude, latitude) { SRID = GeoPointConversion.Wgs84 };
        var zones = await db.RiskZones.AsNoTracking()
            .Where(z => z.Boundary.IsWithinDistance(origin, radiusKm * 1000))
            .Select(z => new { z.Name, z.RiskType, z.Level, Distance = z.Boundary.Distance(origin), Contains = z.Boundary.Intersects(origin) })
            .OrderByDescending(x => x.Level).ThenBy(x => x.Distance)
            .ToListAsync(cancellationToken);

        return zones.Select(z => new RiskZoneProximityDto(z.Name, z.RiskType.ToString(), z.Level, z.Distance, z.Contains)).ToList();
    }
}
