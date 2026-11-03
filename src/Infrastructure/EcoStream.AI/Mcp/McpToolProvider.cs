using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using ModelContextProtocol.Client;

namespace EcoStream.AI.Mcp;

/// <summary>
/// Lazily connects to the EcoStream MCP server and exposes its tools as Semantic Kernel functions.
/// The connection is shared; on failure it is dropped and re-created on the next call.
/// </summary>
public sealed partial class McpToolProvider(IOptions<AiOptions> options, ILoggerFactory loggerFactory) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ILogger _logger = loggerFactory.CreateLogger<McpToolProvider>();
    private McpClient? _client;
    private IReadOnlyList<KernelFunction>? _functions;

    public async Task<IReadOnlyList<KernelFunction>> GetFunctionsAsync(CancellationToken cancellationToken)
    {
        if (_functions is not null) return _functions;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_functions is not null) return _functions;

            var endpoint = options.Value.McpServerUrl;
            var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = endpoint, Name = "ecostream-geo" }, loggerFactory);
            _client = await McpClient.CreateAsync(transport, loggerFactory: loggerFactory, cancellationToken: cancellationToken);

            var tools = await _client.ListToolsAsync(cancellationToken: cancellationToken);
            _functions = tools.Select(t => t.AsKernelFunction()).ToList();
            LogConnected(endpoint, string.Join(", ", tools.Select(t => t.Name)));
            return _functions;
        }
        catch
        {
            await ResetAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Kernel clone with the MCP tools registered as the "geo" plugin.</summary>
    public async Task<Kernel> CreateKernelWithToolsAsync(Kernel kernel, CancellationToken cancellationToken)
    {
        var clone = kernel.Clone();
        clone.Plugins.AddFromFunctions("geo", await GetFunctionsAsync(cancellationToken));
        return clone;
    }

    private async Task ResetAsync()
    {
        _functions = null;
        if (_client is not null) await _client.DisposeAsync();
        _client = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetAsync();
        _gate.Dispose();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Connected to MCP server {Endpoint}; tools: {Tools}")]
    private partial void LogConnected(Uri endpoint, string tools);
}
