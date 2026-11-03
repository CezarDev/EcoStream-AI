namespace EcoStream.AI;

public enum AiProvider
{
    /// <summary>AI disabled — the deterministic rule-based pipeline runs alone.</summary>
    Rules = 0,
    OpenAI = 1,
    AzureOpenAI = 2,

    /// <summary>Local models through Ollama's OpenAI-compatible endpoint (default http://localhost:11434/v1).</summary>
    Ollama = 3,
}

public sealed class AiOptions
{
    public const string Section = "Ai";

    public AiProvider Provider { get; set; } = AiProvider.Rules;

    /// <summary>Chat model id (OpenAI/Ollama) or deployment name (Azure). Must support tool calling.</summary>
    public string? ChatModel { get; set; }

    /// <summary>Embedding model id (OpenAI/Ollama) or deployment name (Azure).</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Required for Azure; optional override for OpenAI; defaults to localhost for Ollama.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>Use user-secrets or the Ai__ApiKey environment variable — never appsettings.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Per-step budget; on timeout the step falls back to the rules.</summary>
    public TimeSpan StepTimeout { get; set; } = TimeSpan.FromSeconds(45);

    public Uri McpServerUrl { get; set; } = new("http://localhost:5090/mcp");

    public QdrantOptions Qdrant { get; set; } = new();

    public bool Enabled => Provider != AiProvider.Rules;

    internal void Validate()
    {
        if (!Enabled) return;
        if (string.IsNullOrWhiteSpace(ChatModel)) throw new InvalidOperationException("Ai:ChatModel is required when Ai:Provider is not 'Rules'.");
        if (string.IsNullOrWhiteSpace(EmbeddingModel)) throw new InvalidOperationException("Ai:EmbeddingModel is required when Ai:Provider is not 'Rules'.");
        if (Provider is AiProvider.OpenAI or AiProvider.AzureOpenAI && string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException($"Ai:ApiKey is required for {Provider} (set Ai__ApiKey or use dotnet user-secrets).");
        if (Provider == AiProvider.AzureOpenAI && Endpoint is null) throw new InvalidOperationException("Ai:Endpoint is required for AzureOpenAI.");
    }
}

public sealed class QdrantOptions
{
    public string Host { get; set; } = "localhost";
    public int GrpcPort { get; set; } = 6334;
    public string? ApiKey { get; set; }
    public string Collection { get; set; } = "civil-defense-protocols";
    public int TopK { get; set; } = 4;
}
