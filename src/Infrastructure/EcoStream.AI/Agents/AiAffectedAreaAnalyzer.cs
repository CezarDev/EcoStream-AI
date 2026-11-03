using EcoStream.AI.Mcp;
using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using Microsoft.SemanticKernel;

namespace EcoStream.AI.Agents;

/// <summary>
/// Agent 1 — Data Analyst. The neighborhood list stays grounded in PostGIS (never generated);
/// the agent investigates rivers and risk zones through MCP tools and writes the analyst notes.
/// </summary>
public sealed class AiAffectedAreaAnalyzer(
    PostGisAffectedAreaAnalyzer grounded,
    McpToolProvider mcp,
    Kernel kernel) : IAffectedAreaAnalyzer
{
    internal const string Instructions = """
        You are the Data Analyst of a Civil Defense climate emergency team in Brazil.
        Use the geo tools (find_rivers_near, get_risk_zones_near, find_neighborhoods_within_radius)
        to investigate the area around the anomaly. Only state facts returned by the tools or given in the prompt.
        Answer in Brazilian Portuguese, at most 120 words, plain text, focused on: rivers that may overflow,
        risk zones (type and level) affecting people, and which neighborhoods need attention first.
        """;

    public async Task<AffectedAreasIdentified> AnalyzeAsync(AnomalyDetectedIntegrationEvent anomaly, CancellationToken cancellationToken)
    {
        var baseline = await grounded.AnalyzeAsync(anomaly, cancellationToken);

        var agentKernel = await mcp.CreateKernelWithToolsAsync(kernel, cancellationToken);
        var agent = AgentRunner.Create("DataAnalyst", Instructions, agentKernel, useTools: true);

        var prompt = $"""
            Anomaly: {AgentRunner.ToJson(anomaly.Anomaly)}
            Neighborhoods already found by PostGIS (radius search): {AgentRunner.ToJson(baseline.Neighborhoods)}
            Estimated exposed population: {baseline.EstimatedPopulation}
            Investigate rivers and risk zones within 5 km and write the analyst notes.
            """;

        var notes = await AgentRunner.RunAsync(agent, prompt, cancellationToken);
        return baseline with { AnalystNotes = string.IsNullOrWhiteSpace(notes) ? null : notes };
    }
}
