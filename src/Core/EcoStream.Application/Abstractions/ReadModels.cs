namespace EcoStream.Application.Abstractions;

// ---- Read side (CQRS). Implemented in Infrastructure with no-tracking EF / PostGIS SQL. ----

public sealed record StationDto(string Code, string Name, double Latitude, double Longitude, string Provider, bool IsActive);

public sealed record ReadingDto(
    string StationCode,
    DateTimeOffset MeasuredAt,
    double RainfallMmPerHour,
    double WindGustKmh,
    double PressureHpa);

public sealed record NeighborhoodProximityDto(
    string Name,
    string City,
    int? Population,
    double DistanceMeters,
    bool InFloodZone);

public interface IStationQueries
{
    Task<IReadOnlyList<StationDto>> ListAsync(bool onlyActive, CancellationToken cancellationToken);
}

public interface ITelemetryQueries
{
    Task<IReadOnlyList<ReadingDto>> GetLatestPerStationAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<ReadingDto>> GetSeriesAsync(string stationCode, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken cancellationToken);
}

/// <summary>Geospatial queries — the same port the MCP server will expose as agent tools in Phase 3.</summary>
public interface IGeoQueries
{
    Task<IReadOnlyList<NeighborhoodProximityDto>> FindNeighborhoodsWithinAsync(
        double latitude, double longitude, double radiusKm, CancellationToken cancellationToken);
}
