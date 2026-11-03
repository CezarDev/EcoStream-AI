// Embeds the Civil Defense protocol documents into Qdrant (RAG knowledge base).
//   dotnet run --project src/Tools/EcoStream.KnowledgeIngestor                   -> docs/protocols/*.md
//   dotnet run --project src/Tools/EcoStream.KnowledgeIngestor -- --dir <folder>
//   dotnet run --project src/Tools/EcoStream.KnowledgeIngestor -- --recreate     -> drop + recreate collection
// Requires Ai:Provider != Rules (embeddings) and Qdrant running.
using EcoStream.AI;
using EcoStream.AI.Rag;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddAiCore(builder.Configuration);
using var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("KnowledgeIngestor");
if (!host.Services.GetRequiredService<IOptions<AiOptions>>().Value.Enabled)
{
    logger.LogError("Ai:Provider is 'Rules'. Configure OpenAI, AzureOpenAI or Ollama to generate embeddings.");
    return 1;
}

var dirIndex = Array.IndexOf(args, "--dir");
var folder = Path.GetFullPath(dirIndex >= 0 && dirIndex + 1 < args.Length ? args[dirIndex + 1] : FindRepoFolder(Path.Combine("docs", "protocols")));
var files = Directory.GetFiles(folder, "*.md").Where(f => !Path.GetFileName(f).Equals("README.md", StringComparison.OrdinalIgnoreCase)).Order().ToList();

var chunks = new List<DocumentChunk>();
foreach (var file in files)
{
    chunks.AddRange(MarkdownChunker.Chunk(Path.GetFileName(file), await File.ReadAllTextAsync(file)));
}
logger.LogInformation("Read {Files} documents from {Folder} → {Chunks} chunks", files.Count, folder, chunks.Count);

var kb = host.Services.GetRequiredService<ProtocolKnowledgeBase>();
await kb.IngestAsync(chunks, recreate: args.Contains("--recreate"), CancellationToken.None);
return 0;

static string FindRepoFolder(string relative)
{
    for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
    {
        var candidate = Path.Combine(dir.FullName, relative);
        if (Directory.Exists(candidate)) return candidate;
    }
    return relative;
}
