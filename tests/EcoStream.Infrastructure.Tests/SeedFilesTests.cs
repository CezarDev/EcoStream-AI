using EcoStream.Infrastructure.Persistence.Seeding;
using FluentAssertions;
using NetTopologySuite.Geometries;

namespace EcoStream.Infrastructure.Tests;

/// <summary>Guards the checked-in seed data: valid GeoJSON, expected geometry types, SRID 4326.</summary>
public class SeedFilesTests
{
    private static string SeedDir([System.Runtime.CompilerServices.CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "infra", "seed", "porto-alegre"));

    [Theory]
    [InlineData("stations.geojson", typeof(Point))]
    [InlineData("neighborhoods.geojson", typeof(Polygon))]
    [InlineData("rivers.geojson", typeof(LineString))]
    [InlineData("risk-zones.geojson", typeof(Polygon))]
    public async Task Seed_file_is_valid(string file, Type geometryType)
    {
        var features = await GeoJsonSeeder.ReadAsync(Path.Combine(SeedDir(), file), default);

        features.Should().NotBeEmpty();
        features.Should().AllSatisfy(f =>
        {
            f.Geometry.Should().BeAssignableTo(geometryType);
            f.Geometry.SRID.Should().Be(4326);
            f.Geometry.IsValid.Should().BeTrue();
            f.Attributes.Exists("name").Should().BeTrue();
        });
    }
}
