using System.Globalization;
using System.Text.Json;
using EcoStream.Domain.Geo;
using EcoStream.Domain.Stations;
using EcoStream.Infrastructure.Persistence.Configurations;
using EcoStream.Infrastructure.Persistence.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.Converters;

namespace EcoStream.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotent loader for a city folder (infra/seed/&lt;city&gt;/*.geojson). Rows are matched by natural key
/// (station code, neighborhood city+name, river/zone name) and only missing ones are inserted.
/// </summary>
public sealed partial class GeoJsonSeeder(EcoStreamDbContext db, ILogger<GeoJsonSeeder> logger)
{
    private static readonly GeometryFactory Factory =
        NtsGeometryServices.Instance.CreateGeometryFactory(GeoPointConversion.Wgs84);

    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task SeedAsync(string directory, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException(directory);

        var stations = await SeedStationsAsync(Path.Combine(directory, "stations.geojson"), cancellationToken);
        var neighborhoods = await SeedNeighborhoodsAsync(Path.Combine(directory, "neighborhoods.geojson"), cancellationToken);
        var rivers = await SeedRiversAsync(Path.Combine(directory, "rivers.geojson"), cancellationToken);
        var zones = await SeedRiskZonesAsync(Path.Combine(directory, "risk-zones.geojson"), cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        LogSeeded(directory, stations, neighborhoods, rivers, zones);
    }

    private async Task<int> SeedStationsAsync(string file, CancellationToken ct)
    {
        var existing = (await db.Stations.Select(s => s.Code).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = 0;
        foreach (var f in await ReadAsync(file, ct))
        {
            var code = Str(f, "code");
            if (!existing.Add(code)) continue;
            if (f.Geometry is not Point p) throw new InvalidDataException($"Station {code} must be a Point.");

            var provider = Enum.Parse<TelemetryProvider>(Str(f, "provider"), ignoreCase: true);
            db.Stations.Add(Station.Register(code, Str(f, "name"), new GeoPoint(p.Y, p.X), provider));
            added++;
        }
        return added;
    }

    private async Task<int> SeedNeighborhoodsAsync(string file, CancellationToken ct)
    {
        var existing = (await db.Neighborhoods.Select(n => n.City + "|" + n.Name).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var f in await ReadAsync(file, ct))
        {
            var (name, city) = (Str(f, "name"), Str(f, "city"));
            if (!existing.Add(city + "|" + name)) continue;
            db.Neighborhoods.Add(new Neighborhood
            {
                Name = name,
                City = city,
                Population = OptInt(f, "population"),
                Boundary = ToMultiPolygon(f.Geometry, name),
            });
            added++;
        }
        return added;
    }

    private async Task<int> SeedRiversAsync(string file, CancellationToken ct)
    {
        var existing = (await db.Rivers.Select(r => r.Name).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var f in await ReadAsync(file, ct))
        {
            var name = Str(f, "name");
            if (!existing.Add(name)) continue;
            var course = f.Geometry switch
            {
                MultiLineString m => m,
                LineString l => Factory.CreateMultiLineString([l]),
                _ => throw new InvalidDataException($"River {name} must be a (Multi)LineString."),
            };
            course.SRID = GeoPointConversion.Wgs84;
            db.Rivers.Add(new River { Name = name, Course = course });
            added++;
        }
        return added;
    }

    private async Task<int> SeedRiskZonesAsync(string file, CancellationToken ct)
    {
        var existing = (await db.RiskZones.Select(z => z.Name).ToListAsync(ct)).ToHashSet();
        var added = 0;
        foreach (var f in await ReadAsync(file, ct))
        {
            var name = Str(f, "name");
            if (!existing.Add(name)) continue;
            db.RiskZones.Add(new RiskZone
            {
                Name = name,
                RiskType = Enum.Parse<RiskType>(Str(f, "riskType"), ignoreCase: true),
                Level = OptInt(f, "level") ?? throw new InvalidDataException($"Risk zone {name} has no level."),
                Boundary = ToMultiPolygon(f.Geometry, name),
            });
            added++;
        }
        return added;
    }

    // ---- helpers ----

    internal static async Task<IReadOnlyList<IFeature>> ReadAsync(string file, CancellationToken ct)
    {
        if (!File.Exists(file)) return [];
        await using var stream = File.OpenRead(file);
        var collection = await JsonSerializer.DeserializeAsync<FeatureCollection>(stream, JsonOptions, ct)
            ?? throw new InvalidDataException($"{file} is not a FeatureCollection.");
        return collection;
    }

    private static MultiPolygon ToMultiPolygon(Geometry geometry, string name)
    {
        var result = geometry switch
        {
            MultiPolygon m => m,
            Polygon p => Factory.CreateMultiPolygon([p]),
            _ => throw new InvalidDataException($"{name} must be a (Multi)Polygon."),
        };
        result.SRID = GeoPointConversion.Wgs84;
        return result;
    }

    private static string Str(IFeature f, string key) =>
        Value(f, key) switch
        {
            string s when !string.IsNullOrWhiteSpace(s) => s,
            JsonElement { ValueKind: JsonValueKind.String } e => e.GetString()!,
            _ => throw new InvalidDataException($"Feature is missing string property '{key}'."),
        };

    private static int? OptInt(IFeature f, string key) =>
        Value(f, key) switch
        {
            null => null,
            JsonElement { ValueKind: JsonValueKind.Null } => null,
            JsonElement { ValueKind: JsonValueKind.Number } e => e.GetInt32(),
            IConvertible c => c.ToInt32(CultureInfo.InvariantCulture),
            var other => throw new InvalidDataException($"Property '{key}' is not a number: {other}"),
        };

    private static object? Value(IFeature f, string key) =>
        f.Attributes is { } a && a.Exists(key) ? a[key] : null;

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new GeoJsonConverterFactory(Factory));
        return options;
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Seeded {Directory}: +{Stations} stations, +{Neighborhoods} neighborhoods, +{Rivers} rivers, +{Zones} risk zones")]
    private partial void LogSeeded(string directory, int stations, int neighborhoods, int rivers, int zones);
}
