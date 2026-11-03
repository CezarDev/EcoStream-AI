using EcoStream.Infrastructure;
using EcoStream.McpServer.Tools;
using OpenTelemetry.Trace;

// MCP server (streamable HTTP) exposing PostGIS tools for the agents.
// Inspect it with: npx @modelcontextprotocol/inspector  → http://localhost:5090/mcp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.AddEcoStreamTelemetry("ecostream-mcp", t => t.AddAspNetCoreInstrumentation());
builder.Services.AddHealthChecks();
builder.Services
    .AddMcpServer(o => o.ServerInfo = new() { Name = "ecostream-geo", Version = "0.3.0" })
    .WithHttpTransport(o => o.Stateless = true)
    .WithTools<GeoTools>();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapMcp("/mcp");
app.Run();

public partial class Program; // for WebApplicationFactory in tests
