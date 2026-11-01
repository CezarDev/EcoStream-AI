using EcoStream.Domain.Common;
using EcoStream.Domain.Geo;

namespace EcoStream.Domain.Telemetry;

public sealed class TelemetryReading : AggregateRoot<Guid>
{
    private TelemetryReading() { } // EF Core

    private TelemetryReading(string stationCode, GeoPoint location, DateTimeOffset measuredAt,
        double rainfallMmPerHour, double windGustKmh, double pressureHpa)
    {
        Id = Guid.CreateVersion7();
        StationCode = stationCode;
        Location = location;
        MeasuredAt = measuredAt;
        RainfallMmPerHour = rainfallMmPerHour;
        WindGustKmh = windGustKmh;
        PressureHpa = pressureHpa;
    }

    public string StationCode { get; private set; } = string.Empty;
    public GeoPoint Location { get; private set; } = null!;
    public DateTimeOffset MeasuredAt { get; private set; }
    public double RainfallMmPerHour { get; private set; }
    public double WindGustKmh { get; private set; }
    public double PressureHpa { get; private set; }

    public static TelemetryReading Record(string stationCode, GeoPoint location, DateTimeOffset measuredAt,
        double rainfallMmPerHour, double windGustKmh, double pressureHpa, AnomalyThresholds thresholds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(stationCode);
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(thresholds);
        if (rainfallMmPerHour < 0) throw new DomainException("Rainfall cannot be negative.");
        if (windGustKmh < 0) throw new DomainException("Wind gust cannot be negative.");

        var reading = new TelemetryReading(stationCode, location, measuredAt, rainfallMmPerHour, windGustKmh, pressureHpa);

        var reasons = reading.EvaluateAnomalies(thresholds);
        if (reasons.Count > 0)
        {
            reading.Raise(new SevereAnomalyDetected(reading.Id, stationCode, location, reasons, DateTimeOffset.UtcNow));
        }

        return reading;
    }

    private List<string> EvaluateAnomalies(AnomalyThresholds t)
    {
        var reasons = new List<string>();
        if (RainfallMmPerHour >= t.RainfallMmPerHour) reasons.Add($"rainfall {RainfallMmPerHour} mm/h");
        if (WindGustKmh >= t.WindGustKmh) reasons.Add($"wind gust {WindGustKmh} km/h");
        if (PressureHpa <= t.MinPressureHpa) reasons.Add($"pressure {PressureHpa} hPa");
        return reasons;
    }
}

public sealed record SevereAnomalyDetected(
    Guid ReadingId,
    string StationCode,
    GeoPoint Location,
    IReadOnlyList<string> Reasons,
    DateTimeOffset OccurredAt) : IDomainEvent;
