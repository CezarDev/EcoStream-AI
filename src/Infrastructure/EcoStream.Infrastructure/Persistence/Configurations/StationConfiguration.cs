using EcoStream.Domain.Stations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EcoStream.Infrastructure.Persistence.Configurations;

internal sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> b)
    {
        b.ToTable("stations");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(Station.CodeMaxLength);
        b.HasAlternateKey(x => x.Code);
        b.Property(x => x.Name).HasMaxLength(120);
        b.Property(x => x.Provider).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Location).IsGeographyPoint();
        b.HasIndex(x => x.Location).HasMethod("gist");
        b.Ignore(x => x.DomainEvents);
    }
}
