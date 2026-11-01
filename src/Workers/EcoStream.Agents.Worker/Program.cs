using EcoStream.Agents.Worker.Consumers;
using EcoStream.Infrastructure;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration, bus =>
{
    bus.AddConsumer<AnomalyDetectedConsumer>();
});
builder.AddEcoStreamTelemetry("ecostream-agents", t => t.AddHttpClientInstrumentation());

builder.Build().Run();
