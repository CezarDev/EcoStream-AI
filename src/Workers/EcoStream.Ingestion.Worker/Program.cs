using EcoStream.Application;
using EcoStream.Infrastructure;
using EcoStream.Ingestion.Worker;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddTelemetryProviders(builder.Configuration);
builder.Services.Configure<IngestionOptions>(builder.Configuration.GetSection(IngestionOptions.Section));
builder.AddEcoStreamTelemetry("ecostream-ingestion", t => t.AddHttpClientInstrumentation().AddSource(TelemetryPollingWorker.ActivitySourceName));
builder.Services.AddHostedService<TelemetryPollingWorker>();

builder.Build().Run();
