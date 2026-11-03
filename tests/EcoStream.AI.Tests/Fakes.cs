using System.Runtime.CompilerServices;
using EcoStream.AI.Rag;
using EcoStream.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace EcoStream.AI.Tests;

/// <summary>Scripted chat model: lets agent tests run without any LLM provider.</summary>
internal sealed class FakeChatCompletion(Func<ChatHistory, string> respond) : IChatCompletionService
{
    public List<ChatHistory> Calls { get; } = [];

    public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(chatHistory);
        return Task.FromResult<IReadOnlyList<ChatMessageContent>>([new ChatMessageContent(AuthorRole.Assistant, respond(chatHistory))]);
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null, Kernel? kernel = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new StreamingChatMessageContent(AuthorRole.Assistant, respond(chatHistory));
    }

    public static Kernel KernelReturning(string json, out FakeChatCompletion fake)
    {
        fake = new FakeChatCompletion(_ => json);
        var builder = Kernel.CreateBuilder();
        builder.Services.AddSingleton<IChatCompletionService>(fake);
        return builder.Build();
    }
}

/// <summary>Deterministic retriever over the repo's protocol docs (keyword overlap) — isolates the LLM in evals.</summary>
internal sealed class DocsProtocolRetriever : IProtocolRetriever
{
    private readonly IReadOnlyList<DocumentChunk> _chunks = LoadDocs();

    public Task<IReadOnlyList<ProtocolSnippet>> SearchAsync(string query, CancellationToken cancellationToken)
    {
        var terms = query.ToLowerInvariant().Split([' ', ';', ','], StringSplitOptions.RemoveEmptyEntries).Where(t => t.Length > 3).ToHashSet();
        IReadOnlyList<ProtocolSnippet> top = _chunks
            .Select(c => (Chunk: c, Score: terms.Count(t => c.Text.Contains(t, StringComparison.OrdinalIgnoreCase))))
            .OrderByDescending(x => x.Score).Take(4)
            .Select((x, i) => new ProtocolSnippet($"P{i + 1}", x.Chunk.Title, x.Chunk.Section, x.Chunk.Text, x.Score))
            .ToList();
        return Task.FromResult(top);
    }

    internal static string DocsDir([CallerFilePath] string here = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(here)!, "..", "..", "docs", "protocols"));

    internal static IReadOnlyList<DocumentChunk> LoadDocs() =>
        Directory.GetFiles(DocsDir(), "0*.md").Order()
            .SelectMany(f => MarkdownChunker.Chunk(Path.GetFileName(f), File.ReadAllText(f)))
            .ToList();
}

internal static class Scenarios
{
    public static readonly AnomalySnapshot Storm =
        new("POA-CENTRO", -30.033, -51.23, 85, 60, 1000, ["rainfall 85 mm/h"], DateTimeOffset.UtcNow);

    public static AffectedAreasIdentified StormOverFloodZone() => new(
        Guid.NewGuid(), Storm,
        [new("Centro Histórico", 39_000, 0, true), new("Cidade Baixa", 16_000, 800, false)],
        55_000,
        "Lago Guaíba a 300 m; zona de inundação nível 4 na orla.");

    public static AffectedAreasIdentified LightRainNobodyAround() => new(
        Guid.NewGuid(), Storm with { RainfallMmPerHour = 52, Reasons = ["rainfall 52 mm/h"] }, [], 0);
}
