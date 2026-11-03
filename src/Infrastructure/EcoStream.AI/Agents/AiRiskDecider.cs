using System.Globalization;
using System.Text;
using EcoStream.AI.Rag;
using EcoStream.AI.Resilience;
using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace EcoStream.AI.Agents;

/// <summary>Structured output requested from the model (JSON schema).</summary>
public sealed record RiskDecision(int Severity, string Summary, IReadOnlyList<string> RecommendedActions, IReadOnlyList<string> CitedProtocolIds);

/// <summary>
/// Agents 2 + 3 — RAG & Protocols retrieves the relevant Civil Defense guidance from Qdrant;
/// the Risk Decider combines data + protocols into a severity, summary and actions with citations.
/// The rule-based baseline is computed first and enforced by <see cref="SeverityGuardrail"/>.
/// </summary>
public sealed class AiRiskDecider(
    RuleBasedRiskDecider rules,
    IProtocolRetriever protocols,
    Kernel kernel,
    IOptions<AiOptions> options) : IRiskDecider
{
    internal const string Instructions = """
        You are the Risk Decider of a Civil Defense climate emergency team in Brazil.
        Decide the severity on a 1-5 scale (1 observation, 2 attention, 3 alert, 4 high alert, 5 emergency)
        using ONLY the data and the protocol excerpts provided. Recommended actions must follow the protocols;
        cite the ids (P1, P2…) of the excerpts you used in citedProtocolIds. Never invent numbers or places.
        Write summary and actions in Brazilian Portuguese. Respond with JSON only.
        """;

    public async Task<RiskAssessed> AssessAsync(AffectedAreasIdentified areas, CancellationToken cancellationToken)
    {
        var baseline = await rules.AssessAsync(areas, cancellationToken);
        var snippets = await protocols.SearchAsync(BuildQuery(areas), cancellationToken);

        var agent = AgentRunner.Create("RiskDecider", Instructions, kernel, useTools: false, responseFormat: typeof(RiskDecision));
        var prompt = new StringBuilder()
            .AppendLine(CultureInfo.InvariantCulture, $"Anomaly: {AgentRunner.ToJson(areas.Anomaly)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Affected neighborhoods: {AgentRunner.ToJson(areas.Neighborhoods)}")
            .AppendLine(CultureInfo.InvariantCulture, $"Estimated exposed population: {areas.EstimatedPopulation}")
            .AppendLine(CultureInfo.InvariantCulture, $"Analyst notes: {areas.AnalystNotes ?? "(none)"}")
            .AppendLine(CultureInfo.InvariantCulture, $"Rule-based baseline: severity {baseline.Severity} — {baseline.Summary}")
            .AppendLine("Protocol excerpts:");
        foreach (var s in snippets) prompt.AppendLine(CultureInfo.InvariantCulture, $"[{s.Id}] {s.Text}");

        var decision = AgentRunner.ParseJson<RiskDecision>(await AgentRunner.RunAsync(agent, prompt.ToString(), cancellationToken));
        Validate(decision);

        var (severity, adjusted) = SeverityGuardrail.Reconcile(decision.Severity, baseline.Severity);
        var byId = snippets.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var cited = decision.CitedProtocolIds
            .Where(byId.ContainsKey) // drop hallucinated citations
            .Select(id => $"{byId[id].Title} — {byId[id].Section}")
            .Distinct()
            .ToList();

        return new RiskAssessed(
            areas.ReadingId,
            areas.Anomaly,
            severity,
            decision.Summary.Trim(),
            baseline.AffectedNeighborhoods,
            decision.RecommendedActions.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList(),
            $"ai:{options.Value.ChatModel}{(adjusted ? "+guardrail" : string.Empty)}",
            cited);
    }

    internal static string BuildQuery(AffectedAreasIdentified areas)
    {
        var a = areas.Anomaly;
        var hazards = new List<string>();
        if (a.RainfallMmPerHour >= 50) hazards.Add("chuva intensa inundação alagamento");
        if (areas.Neighborhoods.Any(n => n.InFloodZone)) hazards.Add("área de risco de inundação evacuação");
        if (a.WindGustKmh >= 90) hazards.Add("vendaval rajadas de vento");
        if (a.RainfallMmPerHour >= 80) hazards.Add("deslizamento encostas");
        hazards.Add("níveis de alerta comunicação à população");
        return string.Join("; ", hazards);
    }

    private static void Validate(RiskDecision d)
    {
        if (d.Severity is < 1 or > 5) throw new InvalidDataException($"Severity out of range: {d.Severity}");
        if (string.IsNullOrWhiteSpace(d.Summary)) throw new InvalidDataException("Empty summary.");
        if (d.RecommendedActions is null || d.RecommendedActions.Count == 0) throw new InvalidDataException("No recommended actions.");
    }
}
