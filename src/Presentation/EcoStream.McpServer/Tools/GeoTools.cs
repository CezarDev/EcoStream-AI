using System.ComponentModel;
using EcoStream.Application.Abstractions;
using ModelContextProtocol.Server;

namespace EcoStream.McpServer.Tools;

/// <summary>
/// PostGIS queries exposed as MCP tools. Any MCP client (our Semantic Kernel agents, Claude Desktop,
/// VS Code…) can discover and call them. Distances are in meters over WGS84 geography.
/// </summary>
[McpServerToolType]
public sealed class GeoTools(IGeoQueries geo)
{
    public const double MaxRadiusKm = 50;

    [McpServerTool(Name = "find_neighborhoods_within_radius", ReadOnly = true, Idempotent = true)]
    [Description("Neighborhoods whose boundary is within the radius of a point, nearest first, with population and whether they intersect a mapped flood zone.")]
    public Task<IReadOnlyList<NeighborhoodProximityDto>> FindNeighborhoods(
        [Description("Latitude (WGS84), e.g. -30.033")] double latitude,
        [Description("Longitude (WGS84), e.g. -51.23")] double longitude,
        [Description("Search radius in kilometers (max 50)")] double radiusKm,
        CancellationToken cancellationToken) =>
        geo.FindNeighborhoodsWithinAsync(latitude, longitude, Clamp(radiusKm), cancellationToken);

    [McpServerTool(Name = "find_rivers_near", ReadOnly = true, Idempotent = true)]
    [Description("Rivers, lakes and streams within the radius of a point, nearest first. Use to judge overflow risk.")]
    public Task<IReadOnlyList<RiverProximityDto>> FindRivers(
        [Description("Latitude (WGS84)")] double latitude,
        [Description("Longitude (WGS84)")] double longitude,
        [Description("Search radius in kilometers (max 50)")] double radiusKm,
        CancellationToken cancellationToken) =>
        geo.FindRiversWithinAsync(latitude, longitude, Clamp(radiusKm), cancellationToken);

    [McpServerTool(Name = "get_risk_zones_near", ReadOnly = true, Idempotent = true)]
    [Description("Civil Defense risk zones (Flood, Landslide; level 1-5) within the radius of a point, highest level first; ContainsPoint=true when the point is inside the zone.")]
    public Task<IReadOnlyList<RiskZoneProximityDto>> GetRiskZones(
        [Description("Latitude (WGS84)")] double latitude,
        [Description("Longitude (WGS84)")] double longitude,
        [Description("Search radius in kilometers (max 50)")] double radiusKm,
        CancellationToken cancellationToken) =>
        geo.FindRiskZonesWithinAsync(latitude, longitude, Clamp(radiusKm), cancellationToken);

    private static double Clamp(double radiusKm) => Math.Clamp(radiusKm, 0.1, MaxRadiusKm);
}
