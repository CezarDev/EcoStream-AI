using System.Globalization;
using System.Text;
using EcoStream.Application.Abstractions;
using EcoStream.Contracts;
using EcoStream.Domain.Alerts;
using Microsoft.Extensions.Options;

namespace EcoStream.Application.Pipeline;

internal sealed class PostGisAffectedAreaAnalyzer(IGeoQueries geo, IOptions<PipelineOptions> options) : IAffectedAreaAnalyzer
{
    public async Task<AffectedAreasIdentified> AnalyzeAsync(AnomalyDetectedIntegrationEvent anomaly, CancellationToken cancellationToken)
    {
        var a = anomaly.Anomaly;
        var nearby = await geo.FindNeighborhoodsWithinAsync(a.Latitude, a.Longitude, options.Value.ImpactRadiusKm, cancellationToken);

        var neighborhoods = nearby
            .Select(n => new AffectedNeighborhood(n.Name, n.Population, Math.Round(n.DistanceMeters), n.InFloodZone))
            .ToList();

        return new AffectedAreasIdentified(anomaly.ReadingId, a, neighborhoods, neighborhoods.Sum(n => n.Population ?? 0));
    }
}

internal sealed class RuleBasedRiskDecider : IRiskDecider
{
    public const string Name = "rules-v1";

    public Task<RiskAssessed> AssessAsync(AffectedAreasIdentified areas, CancellationToken cancellationToken)
    {
        var a = areas.Anomaly;
        var flooded = areas.Neighborhoods.Where(n => n.InFloodZone).ToList();

        var severity = RiskAssessmentPolicy.Assess(
            new RiskInputs(a.RainfallMmPerHour, a.WindGustKmh, a.PressureHpa, flooded.Count, areas.EstimatedPopulation),
            out var factors);

        var summary = factors.Count == 0
            ? $"Anomaly at {a.StationCode}: {string.Join(", ", a.Reasons)}."
            : $"Anomaly at {a.StationCode}: {string.Join("; ", factors)}.";

        return Task.FromResult(new RiskAssessed(
            areas.ReadingId,
            a,
            severity.Value,
            summary,
            areas.Neighborhoods.Select(n => n.Name).ToList(),
            RecommendedActions(severity.Value, flooded.Select(n => n.Name).ToList()),
            Name));
    }

    private static List<string> RecommendedActions(int severity, List<string> floodedNeighborhoods)
    {
        var actions = new List<string> { "Keep monitoring station readings every 5 minutes." };
        if (severity >= 3) actions.Add("Notify Civil Defense duty officer and pre-position teams.");
        if (severity >= 3 && floodedNeighborhoods.Count > 0)
            actions.Add($"Prepare evacuation routes for: {string.Join(", ", floodedNeighborhoods)}.");
        if (severity >= 4) actions.Add("Open shelters and send SMS alert to registered residents.");
        if (severity >= 5) actions.Add("Activate municipal emergency operations center (COE).");
        return actions;
    }
}

internal sealed class RuleBasedAlertComposer : IAlertComposer
{
    /// <summary>Minimum severity for each channel: ops always, public channels only when it matters.</summary>
    internal static readonly IReadOnlyDictionary<string, int> MinSeverity = new Dictionary<string, int>
    {
        [AlertChannels.Slack] = 1,
        [AlertChannels.Telegram] = 3,
        [AlertChannels.Sms] = 4,
    };

    public Task<IReadOnlyList<AlertDispatchRequested>> ComposeAsync(RiskAssessed risk, CancellationToken cancellationToken)
    {
        var title = $"[Severity {risk.Severity}/5] Weather alert — {risk.Anomaly.StationCode}";

        IReadOnlyList<AlertDispatchRequested> messages = MinSeverity
            .Where(c => risk.Severity >= c.Value)
            .Select(c => new AlertDispatchRequested(risk.ReadingId, risk.Severity, c.Key, title, Body(c.Key, risk)))
            .ToList();

        return Task.FromResult(messages);
    }

    private static string Body(string channel, RiskAssessed risk)
    {
        var areas = risk.AffectedNeighborhoods.Count > 0 ? string.Join(", ", risk.AffectedNeighborhoods) : "area under analysis";

        if (channel == AlertChannels.Sms) // 160-char budget, plain text
        {
            var sms = $"ALERTA DEFESA CIVIL nivel {risk.Severity}/5: chuva forte em {areas}. Evite areas alagadas. Emergencia: 199";
            return sms.Length <= 160 ? sms : sms[..157] + "...";
        }

        var sb = new StringBuilder()
            .AppendLine(risk.Summary)
            .AppendLine(CultureInfo.InvariantCulture, $"Affected: {areas}")
            .AppendLine("Actions:");
        foreach (var action in risk.RecommendedActions) sb.AppendLine(CultureInfo.InvariantCulture, $"• {action}");
        return sb.ToString().TrimEnd();
    }
}
