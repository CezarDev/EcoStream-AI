using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoStream.Infrastructure.Persistence.Configurations;

internal sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> b)
    {
        b.ToTable("telemetry_readings");
        b.HasKey(x => x.Id);
        b.Property(x => x.StationCode).HasMaxLength(Station.CodeMaxLength);
        b.Property(x => x.Location).IsGeographyPoint();

        b.HasOne<Station>()
            .WithMany()
            .HasForeignKey(x => x.StationCode)
            .HasPrincipalKey(s => s.Code)
            .OnDelete(DeleteBehavior.Restrict);

        // Idempotency + the "series by station" access path in one index.
        b.HasIndex(x => new { x.StationCode, x.MeasuredAt }).IsUnique();
        b.HasIndex(x => x.Location).HasMethod("gist");
        b.Ignore(x => x.DomainEvents);
    }
}
