using EcoStream.Contracts;
using MassTransit;

namespace EcoStream.Agents.Worker.Consumers;

/// <summary>
/// Entry point of the multi-agent pipeline. Phase 3 hands the event to the Data Analyst agent
/// (Semantic Kernel + MCP tools) and publishes AffectedAreasIdentified.
/// </summary>
internal sealed partial class AnomalyDetectedConsumer(ILogger<AnomalyDetectedConsumer> logger)
    : IConsumer<AnomalyDetectedIntegrationEvent>
{
    public Task Consume(ConsumeContext<AnomalyDetectedIntegrationEvent> context)
    {
        var e = context.Message;
        LogReceived(e.ReadingId, e.StationCode, string.Join(", ", e.Reasons));
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anomaly {ReadingId} at station {StationCode}: {Reasons}")]
    private partial void LogReceived(Guid readingId, string stationCode, string reasons);
}
