namespace EcoStream.Domain.Telemetry;

/// <summary>Limits above/below which a reading counts as a severe anomaly. Defaults follow common INMET alert levels.</summary>
public sealed record AnomalyThresholds(
    double RainfallMmPerHour = 50,
    double WindGustKmh = 90,
    double MinPressureHpa = 980)
{
    public static AnomalyThresholds Default { get; } = new();
}
