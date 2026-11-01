namespace EcoStream.Contracts;

// Integration events exchanged over RabbitMQ. Keep them flat, versionable and free of domain types.

public sealed record AnomalyDetectedIntegrationEvent(
    Guid ReadingId,
    string StationCode,
    double Latitude,
    double Longitude,
    double RainfallMmPerHour,
    double WindGustKmh,
    double PressureHpa,
    IReadOnlyList<string> Reasons,
    DateTimeOffset DetectedAt);

public sealed record AffectedAreasIdentified(
    Guid ReadingId,
    IReadOnlyList<string> Neighborhoods,
    IReadOnlyList<string> NearbyRivers,
    int EstimatedPopulation);

public sealed record RiskAssessed(
    Guid ReadingId,
    int Severity,
    string Summary,
    IReadOnlyList<string> RecommendedActions);

public sealed record AlertDispatchRequested(
    Guid ReadingId,
    int Severity,
    string Title,
    string Body,
    IReadOnlyList<string> Channels);
