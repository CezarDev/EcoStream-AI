using EcoStream.Infrastructure;
using OpenTelemetry.Trace;

// Phase 3: add the ModelContextProtocol.AspNetCore package and expose PostGIS tools, e.g.
//   find_neighborhoods_within_radius(lat, lon, km)
//   find_rivers_near(lat, lon, km)
//   get_risk_zones_intersecting(lat, lon)
// The Semantic Kernel agents consume these tools over HTTP (MCP streamable transport).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddPersistence(builder.Configuration);
builder.AddEcoStreamTelemetry("ecostream-mcp", t => t.AddAspNetCoreInstrumentation());
builder.Services.AddHealthChecks();

var app = builder.Build();
app.MapHealthChecks("/health");
app.Run();
