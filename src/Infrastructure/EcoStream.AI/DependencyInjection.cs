using System.ClientModel;
using Azure.AI.OpenAI;
using EcoStream.AI.Agents;
using EcoStream.AI.Mcp;
using EcoStream.AI.Rag;
using EcoStream.AI.Resilience;
using EcoStream.Application.Pipeline;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using OpenAI;
using Qdrant.Client;

namespace EcoStream.AI;

public static class DependencyInjection
{
    public const string OllamaDefaultEndpoint = "http://localhost:11434/v1";

    /// <summary>Semantic Kernel (chat + embeddings) for the configured provider, plus Qdrant.</summary>
    public static IServiceCollection AddAiCore(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(AiOptions.Section);
        var options = section.Get<AiOptions>() ?? new AiOptions();
        services.Configure<AiOptions>(section);
        if (!options.Enabled) return services;
        options.Validate();

        switch (options.Provider)
        {
            case AiProvider.AzureOpenAI:
                var azure = new AzureOpenAIClient(options.Endpoint!, new ApiKeyCredential(options.ApiKey!));
                services.AddAzureOpenAIChatCompletion(options.ChatModel!, azure);
                services.AddAzureOpenAIEmbeddingGenerator(options.EmbeddingModel!, azure);
                break;

            default: // OpenAI and Ollama share the OpenAI wire protocol
                var endpoint = options.Endpoint ?? (options.Provider == AiProvider.Ollama ? new Uri(OllamaDefaultEndpoint) : null);
                var client = new OpenAIClient(
                    new ApiKeyCredential(options.ApiKey ?? "ollama"),
                    new OpenAIClientOptions { Endpoint = endpoint, NetworkTimeout = options.StepTimeout });
                services.AddOpenAIChatCompletion(options.ChatModel!, client);
                services.AddOpenAIEmbeddingGenerator(options.EmbeddingModel!, client);
                break;
        }

        services.AddKernel();
        services.TryAddSingleton(sp =>
        {
            var q = sp.GetRequiredService<IOptions<AiOptions>>().Value.Qdrant;
            return new QdrantClient(q.Host, q.GrpcPort, https: false, apiKey: q.ApiKey);
        });
        services.TryAddSingleton<ProtocolKnowledgeBase>();
        services.TryAddSingleton<IProtocolRetriever>(sp => sp.GetRequiredService<ProtocolKnowledgeBase>());
        return services;
    }

    /// <summary>
    /// Replaces the rule-based pipeline steps with Semantic Kernel agents wrapped in rule-based fallbacks.
    /// With Ai:Provider = Rules (default) nothing changes. Call after AddIncidentPipeline().
    /// </summary>
    public static IServiceCollection AddAiAgents(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAiCore(configuration);
        if (!(configuration.GetSection(AiOptions.Section).Get<AiOptions>()?.Enabled ?? false)) return services;

        services.TryAddSingleton<McpToolProvider>();
        services.AddScoped<AiAffectedAreaAnalyzer>();
        services.AddScoped<AiRiskDecider>();
        services.AddScoped<AiAlertComposer>();

        services.Replace(ServiceDescriptor.Scoped<IAffectedAreaAnalyzer>(sp => new FallbackAffectedAreaAnalyzer(
            sp.GetRequiredService<AiAffectedAreaAnalyzer>(), sp.GetRequiredService<PostGisAffectedAreaAnalyzer>(),
            sp.GetRequiredService<IOptions<AiOptions>>(), sp.GetRequiredService<ILogger<FallbackAffectedAreaAnalyzer>>())));
        services.Replace(ServiceDescriptor.Scoped<IRiskDecider>(sp => new FallbackRiskDecider(
            sp.GetRequiredService<AiRiskDecider>(), sp.GetRequiredService<RuleBasedRiskDecider>(),
            sp.GetRequiredService<IOptions<AiOptions>>(), sp.GetRequiredService<ILogger<FallbackRiskDecider>>())));
        services.Replace(ServiceDescriptor.Scoped<IAlertComposer>(sp => new FallbackAlertComposer(
            sp.GetRequiredService<AiAlertComposer>(), sp.GetRequiredService<RuleBasedAlertComposer>(),
            sp.GetRequiredService<IOptions<AiOptions>>(), sp.GetRequiredService<ILogger<FallbackAlertComposer>>())));

        return services;
    }
}
