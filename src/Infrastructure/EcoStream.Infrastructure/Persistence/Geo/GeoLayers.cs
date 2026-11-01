using NetTopologySuite.Geometries;

namespace EcoStream.Infrastructure.Persistence.Geo;

// Reference layers. They carry no business rules, so they live here as read models
// (with NetTopologySuite types) instead of polluting the domain.

public sealed class Neighborhood
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string City { get; set; }
    public int? Population { get; set; }
    public required MultiPolygon Boundary { get; set; }
}

public sealed class River
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required MultiLineString Course { get; set; }
}

public enum RiskType
{
    Flood = 1,
    Landslide = 2,
}

public sealed class RiskZone
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public RiskType RiskType { get; set; }

    /// <summary>1 (low) to 5 (very high), as mapped by Civil Defense.</summary>
    public int Level { get; set; }

    public required MultiPolygon Boundary { get; set; }
}
