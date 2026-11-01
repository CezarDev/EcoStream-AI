using EcoStream.Domain.Common;
using EcoStream.Domain.Geo;

namespace EcoStream.Domain.Stations;

public enum TelemetryProvider
{
    /// <summary>Open-Meteo model data at the station coordinates (no API key needed).</summary>
    OpenMeteo = 1,

    /// <summary>INMET automatic station (requires API token; adapter planned).</summary>
    Inmet = 2,

    /// <summary>Readings pushed through POST /api/telemetry (IoT, partners, tests).</summary>
    Manual = 3,
}

/// <summary>A monitoring point. <see cref="Code"/> is the natural key used across messages and APIs.</summary>
public sealed class Station : AggregateRoot<Guid>
{
    public const int CodeMaxLength = 32;

    private Station() { } // EF Core

    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public GeoPoint Location { get; private set; } = null!;
    public TelemetryProvider Provider { get; private set; }
    public bool IsActive { get; private set; }

    public static Station Register(string code, string name, GeoPoint location, TelemetryProvider provider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(location);
        if (code.Length > CodeMaxLength) throw new DomainException($"Station code longer than {CodeMaxLength} chars.");

        return new Station
        {
            Id = Guid.CreateVersion7(),
            Code = code.Trim().ToUpperInvariant(),
            Name = name.Trim(),
            Location = location,
            Provider = provider,
            IsActive = true,
        };
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
