using EcoStream.Application.Abstractions;
using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;
using EcoStream.Infrastructure.Persistence.Geo;
using Microsoft.EntityFrameworkCore;

namespace EcoStream.Infrastructure.Persistence;

public sealed class EcoStreamDbContext(DbContextOptions<EcoStreamDbContext> options) : DbContext(options), IUnitOfWork
{
    public const string GeoSchema = "geo";

    // Aggregates (write side)
    public DbSet<Station> Stations => Set<Station>();
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();

    // Geospatial reference layers (read models, seeded from GeoJSON)
    public DbSet<Neighborhood> Neighborhoods => Set<Neighborhood>();
    public DbSet<River> Rivers => Set<River>();
    public DbSet<RiskZone> RiskZones => Set<RiskZone>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(EcoStreamDbContext).Assembly);
    }
}
