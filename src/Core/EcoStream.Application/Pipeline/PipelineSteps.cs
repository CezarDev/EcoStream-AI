using EcoStream.Contracts;

namespace EcoStream.Application.Pipeline;

// The four agent roles as ports. Phase 2 ships deterministic implementations;
// Phase 3 registers Semantic Kernel agents for the same interfaces (rules remain the fallback).

/// <summary>Data Analyst: which neighborhoods are exposed to this anomaly?</summary>
public interface IAffectedAreaAnalyzer
{
    Task<AffectedAreasIdentified> AnalyzeAsync(AnomalyDetectedIntegrationEvent anomaly, CancellationToken cancellationToken);
}

/// <summary>Risk Decider: severity 1–5 plus recommended actions.</summary>
public interface IRiskDecider
{
    Task<RiskAssessed> AssessAsync(AffectedAreasIdentified areas, CancellationToken cancellationToken);
}

/// <summary>Communicator: one message per channel, already formatted for it.</summary>
public interface IAlertComposer
{
    Task<IReadOnlyList<AlertDispatchRequested>> ComposeAsync(RiskAssessed risk, CancellationToken cancellationToken);
}

public sealed class PipelineOptions
{
    public const string Section = "Pipeline";

    /// <summary>Radius around the station used to find exposed neighborhoods.</summary>
    public double ImpactRadiusKm { get; set; } = 5;
}
