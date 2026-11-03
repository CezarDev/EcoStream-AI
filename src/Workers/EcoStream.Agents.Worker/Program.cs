using EcoStream.AI;
using EcoStream.Agents.Worker.Consumers;
using EcoStream.Application;
using EcoStream.Contracts;
using EcoStream.Infrastructure;
using EcoStream.Infrastructure.Messaging;
using EcoStream.Notifications;
using MassTransit;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddIncidentPipeline();
builder.Services.AddAiAgents(builder.Configuration); // no-op when Ai:Provider = Rules
builder.Services.AddNotifications();
builder.Services.AddInfrastructure(builder.Configuration,
    bus =>
    {
        bus.AddConsumer<AnomalyDetectedConsumer, ResilientConsumerDefinition<AnomalyDetectedConsumer>>();
        bus.AddConsumer<AffectedAreasConsumer, ResilientConsumerDefinition<AffectedAreasConsumer>>();
        bus.AddConsumer<RiskAssessedConsumer, ResilientConsumerDefinition<RiskAssessedConsumer>>();
        bus.AddConsumer<AlertDispatchConsumer, ResilientConsumerDefinition<AlertDispatchConsumer>>()
            .ExcludeFromConfigureEndpoints(); // bound manually below, per channel
    },
    (context, cfg) =>
    {
        foreach (var channel in AlertChannels.All)
        {
            cfg.AlertsEndpoint<AlertDispatchConsumer>(context, $"notifications-{channel}", $"alerts.*.{channel}");
        }
    });
builder.AddEcoStreamTelemetry("ecostream-agents", t => t
    .AddHttpClientInstrumentation()
    .AddSource("Microsoft.SemanticKernel*")   // agent invocations, tool calls, token usage
    .AddSource("Experimental.ModelContextProtocol*"));

builder.Build().Run();
