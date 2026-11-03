using System.Text;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace EcoStream.AI.Agents;

internal static class AgentRunner
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static ChatCompletionAgent Create(string name, string instructions, Kernel kernel, bool useTools, Type? responseFormat = null) =>
        new()
        {
            Name = name,
            Instructions = instructions,
            Kernel = kernel,
            Arguments = new KernelArguments(new OpenAIPromptExecutionSettings
            {
                Temperature = 0.1,
                FunctionChoiceBehavior = useTools ? FunctionChoiceBehavior.Auto() : null,
                ResponseFormat = responseFormat,
            }),
        };

    public static async Task<string> RunAsync(ChatCompletionAgent agent, string prompt, CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        await foreach (var item in agent.InvokeAsync(prompt, thread: null, options: null, cancellationToken))
        {
            sb.Append(item.Message.Content);
        }
        return sb.ToString().Trim();
    }

    /// <summary>Parses the model's JSON, tolerating ```json fences some local models add.</summary>
    public static T ParseJson<T>(string raw)
    {
        var text = raw.Trim();
        if (text.StartsWith("```", StringComparison.Ordinal))
        {
            var firstNewLine = text.IndexOf('\n', StringComparison.Ordinal);
            var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
            if (firstNewLine > 0 && lastFence > firstNewLine) text = text[(firstNewLine + 1)..lastFence].Trim();
        }

        return JsonSerializer.Deserialize<T>(text, Json)
            ?? throw new InvalidDataException($"Model returned null for {typeof(T).Name}.");
    }

    public static string ToJson(object value) => JsonSerializer.Serialize(value, Json);
}
