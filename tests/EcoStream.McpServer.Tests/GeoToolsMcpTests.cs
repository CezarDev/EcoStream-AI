using EcoStream.Application.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using NSubstitute;

namespace EcoStream.McpServer.Tests;

/// <summary>Real MCP protocol round-trip (streamable HTTP) against the in-process server; PostGIS is substituted.</summary>
public sealed class GeoToolsMcpTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly IGeoQueries _geo = Substitute.For<IGeoQueries>();
    private McpClient _client = null!;

    public GeoToolsMcpTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .ConfigureServices(s => s.Replace(ServiceDescriptor.Scoped(_ => _geo))));
    }

    public async Task InitializeAsync()
    {
        var http = _factory.CreateClient();
        var transport = new HttpClientTransport(
            new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http, NullLoggerFactory.Instance, ownsHttpClient: true);
        _client = await McpClient.CreateAsync(transport);
    }

    public async Task DisposeAsync() => await _client.DisposeAsync();

    [Fact]
    public async Task Server_advertises_the_three_geo_tools()
    {
        var tools = await _client.ListToolsAsync();

        tools.Select(t => t.Name).Should().BeEquivalentTo("find_neighborhoods_within_radius", "find_rivers_near", "get_risk_zones_near");
        tools.Should().OnlyContain(t => !string.IsNullOrWhiteSpace(t.Description));
    }

    [Fact]
    public async Task Calling_a_tool_runs_the_postgis_query_and_clamps_the_radius()
    {
        _geo.FindNeighborhoodsWithinAsync(-30.03, -51.23, 50, Arg.Any<CancellationToken>())
            .Returns([new NeighborhoodProximityDto("Centro Histórico", "Porto Alegre", 39_000, 0, true)]);

        var result = await _client.CallToolAsync("find_neighborhoods_within_radius",
            new Dictionary<string, object?> { ["latitude"] = -30.03, ["longitude"] = -51.23, ["radiusKm"] = 500 });

        result.IsError.Should().NotBe(true);
        result.Content.OfType<TextContentBlock>().Single().Text.Should().Contain("Centro Hist");
    }
}
