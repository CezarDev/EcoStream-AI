namespace EcoStream.AI.Agents;

/// <summary>
/// The four specialised agents of the multi-agent pipeline (implemented in Phase 3 with Semantic Kernel).
/// Each one is triggered by a RabbitMQ message and publishes the next one, so the flow is asynchronous and retryable.
/// </summary>
public enum AgentRole
{
    /// <summary>Consumes AnomalyDetected; calls MCP tools over PostGIS; publishes AffectedAreasIdentified.</summary>
    DataAnalyst,

    /// <summary>Retrieves Civil Defense manuals/protocols from Qdrant (RAG) for the affected scenario.</summary>
    ProtocolsRag,

    /// <summary>Combines data + protocols into a 1–5 severity; publishes RiskAssessed.</summary>
    RiskDecider,

    /// <summary>Formats per-channel messages; publishes AlertDispatchRequested.</summary>
    Communicator,
}
