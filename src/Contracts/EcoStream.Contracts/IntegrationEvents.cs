namespace EcoStream.Contracts;

// Integration events exchanged over RabbitMQ. Flat, versionable, no domain types.
// Pipeline: AnomalyDetected → AffectedAreasIdentified → RiskAssessed → AlertDispatchRequested (one per channel).
// Every message carries ReadingId as the correlation key of the incident.

/// <summary>Snapshot of the triggering reading, carried along the whole pipeline.</summary>
public sealed record AnomalySnapshot(
    string StationCode,
    double Latitude,
    double Longitude,
    double RainfallMmPerHour,
    double WindGustKmh,
    double PressureHpa,
    IReadOnlyList<string> Reasons,
    DateTimeOffset DetectedAt);

/// <summary>Published to the fanout exchange <c>ecostream.anomaly-detected</c>.</summary>
public sealed record AnomalyDetectedIntegrationEvent(Guid ReadingId, AnomalySnapshot Anomaly);

public sealed record AffectedNeighborhood(string Name, int? Population, double DistanceMeters, bool InFloodZone);

/// <summary>Output of the Data Analyst step (PostGIS).</summary>
public sealed record AffectedAreasIdentified(
    Guid ReadingId,
    AnomalySnapshot Anomaly,
    IReadOnlyList<AffectedNeighborhood> Neighborhoods,
    int EstimatedPopulation,
    string? AnalystNotes = null);

/// <summary>Output of the Risk Decider step.</summary>
public sealed record RiskAssessed(
    Guid ReadingId,
    AnomalySnapshot Anomaly,
    int Severity,
    string Summary,
    IReadOnlyList<string> AffectedNeighborhoods,
    IReadOnlyList<string> RecommendedActions,
    string AssessedBy,
    IReadOnlyList<string>? CitedProtocols = null);

/// <summary>
/// Published to the topic exchange <c>ecostream.alerts</c> with routing key
/// <c>alerts.{severity}.{channel}</c>, so each channel binds only what it handles.
/// </summary>
public sealed record AlertDispatchRequested(
    Guid ReadingId,
    int Severity,
    string Channel,
    string Title,
    string Body);

public static class AlertChannels
{
    public const string Slack = "slack";
    public const string Telegram = "telegram";
    public const string Sms = "sms";

    public static readonly IReadOnlyList<string> All = [Slack, Telegram, Sms];
}
