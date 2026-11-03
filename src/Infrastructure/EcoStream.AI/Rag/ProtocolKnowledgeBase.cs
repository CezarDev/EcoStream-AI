using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace EcoStream.AI.Rag;

public sealed record ProtocolSnippet(string Id, string Title, string Section, string Text, double Score);

public interface IProtocolRetriever
{
    Task<IReadOnlyList<ProtocolSnippet>> SearchAsync(string query, CancellationToken cancellationToken);
}

/// <summary>
/// RAG & Protocols agent's knowledge base: Civil Defense protocols embedded into Qdrant.
/// Retrieval is deterministic (vector search), only the embeddings come from the AI provider.
/// </summary>
public sealed partial class ProtocolKnowledgeBase(
    QdrantClient qdrant,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IOptions<AiOptions> options,
    ILogger<ProtocolKnowledgeBase> logger) : IProtocolRetriever
{
    private QdrantOptions Q => options.Value.Qdrant;

    public async Task<IReadOnlyList<ProtocolSnippet>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var vector = await embeddings.GenerateVectorAsync(query, cancellationToken: cancellationToken);
        var hits = await qdrant.QueryAsync(Q.Collection, query: vector.ToArray(), limit: (ulong)Q.TopK,
            payloadSelector: true, cancellationToken: cancellationToken);

        // Short ids (P1, P2…) are what the LLM cites; anything else it cites is discarded.
        return hits.Select((h, i) => new ProtocolSnippet(
            $"P{i + 1}",
            h.Payload["title"].StringValue,
            h.Payload["section"].StringValue,
            h.Payload["text"].StringValue,
            h.Score)).ToList();
    }

    public async Task<int> IngestAsync(IEnumerable<DocumentChunk> chunks, bool recreate, CancellationToken cancellationToken)
    {
        var list = chunks.ToList();
        if (list.Count == 0) return 0;

        var vectors = await embeddings.GenerateAsync(list.Select(c => c.Text), cancellationToken: cancellationToken);
        var dimensions = (ulong)vectors[0].Vector.Length;

        await EnsureCollectionAsync(dimensions, recreate, cancellationToken);

        var points = list.Select((c, i) => new PointStruct
        {
            Id = c.Id,
            Vectors = vectors[i].Vector.ToArray(),
            Payload =
            {
                ["source"] = c.Source,
                ["title"] = c.Title,
                ["section"] = c.Section,
                ["text"] = c.Text,
            },
        }).ToList();

        await qdrant.UpsertAsync(Q.Collection, points, cancellationToken: cancellationToken);
        LogIngested(list.Count, Q.Collection, dimensions);
        return list.Count;
    }

    private async Task EnsureCollectionAsync(ulong dimensions, bool recreate, CancellationToken ct)
    {
        var exists = await qdrant.CollectionExistsAsync(Q.Collection, ct);
        if (exists && recreate)
        {
            await qdrant.DeleteCollectionAsync(Q.Collection, cancellationToken: ct);
            exists = false;
        }

        if (exists)
        {
            var info = await qdrant.GetCollectionInfoAsync(Q.Collection, ct);
            var current = info.Config.Params.VectorsConfig.Params.Size;
            if (current != dimensions)
                throw new InvalidOperationException(
                    $"Collection '{Q.Collection}' has {current} dimensions but the embedding model returns {dimensions}. Re-run with --recreate.");
            return;
        }

        await qdrant.CreateCollectionAsync(Q.Collection, new VectorParams { Size = dimensions, Distance = Distance.Cosine }, cancellationToken: ct);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Ingested {Count} chunks into Qdrant collection {Collection} ({Dimensions} dims)")]
    private partial void LogIngested(int count, string collection, ulong dimensions);
}
