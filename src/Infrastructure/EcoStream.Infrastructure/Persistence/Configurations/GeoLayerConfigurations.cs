using EcoStream.Infrastructure.Persistence.Geo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoStream.Infrastructure.Persistence.Configurations;

internal sealed class NeighborhoodConfiguration : IEntityTypeConfiguration<Neighborhood>
{
    public void Configure(EntityTypeBuilder<Neighborhood> b)
    {
        b.ToTable("neighborhoods", EcoStreamDbContext.GeoSchema);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.City).HasMaxLength(120);
        b.HasIndex(x => new { x.City, x.Name }).IsUnique();
        b.Property(x => x.Boundary).HasColumnType("geography(MultiPolygon,4326)");
        b.HasIndex(x => x.Boundary).HasMethod("gist");
    }
}

internal sealed class RiverConfiguration : IEntityTypeConfiguration<River>
{
    public void Configure(EntityTypeBuilder<River> b)
    {
        b.ToTable("rivers", EcoStreamDbContext.GeoSchema);
        b.Property(x => x.Name).HasMaxLength(120);
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.Course).HasColumnType("geography(MultiLineString,4326)");
        b.HasIndex(x => x.Course).HasMethod("gist");
    }
}

internal sealed class RiskZoneConfiguration : IEntityTypeConfiguration<RiskZone>
{
    public void Configure(EntityTypeBuilder<RiskZone> b)
    {
        b.ToTable("risk_zones", EcoStreamDbContext.GeoSchema, t =>
            t.HasCheckConstraint("ck_risk_zones_level", "level BETWEEN 1 AND 5"));
        b.Property(x => x.Name).HasMaxLength(160);
        b.HasIndex(x => x.Name).IsUnique();
        b.Property(x => x.RiskType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Boundary).HasColumnType("geography(MultiPolygon,4326)");
        b.HasIndex(x => x.Boundary).HasMethod("gist");
    }
}
