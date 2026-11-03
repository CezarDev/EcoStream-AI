using EcoStream.Application.Pipeline;
using EcoStream.Contracts;
using MassTransit;

namespace EcoStream.Agents.Worker.Consumers;

// Thin adapters: RabbitMQ message in → pipeline step (rules today, AI agent in Phase 3) → next message out.
// Publishes go through the consumer outbox, so they are only sent if the step completes.

/// <summary>Step 1 — Data Analyst.</summary>
public sealed partial class AnomalyDetectedConsumer(IAffectedAreaAnalyzer analyzer, ILogger<AnomalyDetectedConsumer> logger)
    : IConsumer<AnomalyDetectedIntegrationEvent>
{
    public async Task Consume(ConsumeContext<AnomalyDetectedIntegrationEvent> context)
    {
        var result = await analyzer.AnalyzeAsync(context.Message, context.CancellationToken);
        LogAnalyzed(context.Message.ReadingId, result.Neighborhoods.Count, result.EstimatedPopulation);
        await context.Publish(result, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Incident {ReadingId}: {Count} neighborhoods exposed (~{Population} people)")]
    private partial void LogAnalyzed(Guid readingId, int count, int population);
}

/// <summary>Step 2/3 — Risk Decider (RAG protocol lookup joins here in Phase 3).</summary>
public sealed partial class AffectedAreasConsumer(IRiskDecider decider, ILogger<AffectedAreasConsumer> logger)
    : IConsumer<AffectedAreasIdentified>
{
    public async Task Consume(ConsumeContext<AffectedAreasIdentified> context)
    {
        var risk = await decider.AssessAsync(context.Message, context.CancellationToken);
        LogAssessed(risk.ReadingId, risk.Severity, risk.AssessedBy);
        await context.Publish(risk, context.CancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Incident {ReadingId}: severity {Severity} (by {AssessedBy})")]
    private partial void LogAssessed(Guid readingId, int severity, string assessedBy);
}

/// <summary>Step 4 — Communicator: one AlertDispatchRequested per channel, routed by topic key.</summary>
public sealed class RiskAssessedConsumer(IAlertComposer composer) : IConsumer<RiskAssessed>
{
    public async Task Consume(ConsumeContext<RiskAssessed> context)
    {
        var messages = await composer.ComposeAsync(context.Message, context.CancellationToken);
        await context.PublishBatch(messages, context.CancellationToken);
    }
}
