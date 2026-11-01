using EcoStream.Application.Abstractions;
using EcoStream.Infrastructure.Messaging;
using EcoStream.Infrastructure.Persistence;
using EcoStream.Infrastructure.Persistence.Queries;
using EcoStream.Infrastructure.Persistence.Seeding;
using EcoStream.Infrastructure.Telemetry.OpenMeteo;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EcoStream.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Persistence + messaging. Most hosts want both.</summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null) =>
        services.AddPersistence(configuration).AddMessaging(configuration, configureConsumers);

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");

        services.AddDbContext<EcoStreamDbContext>(o => ConfigureNpgsql(o, connectionString));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<EcoStreamDbContext>());
        services.AddScoped<ITelemetryRepository, TelemetryRepository>();
        services.AddScoped<IStationRepository, StationRepository>();
        services.AddScoped<IStationQueries, StationQueries>();
        services.AddScoped<ITelemetryQueries, TelemetryQueries>();
        services.AddScoped<IGeoQueries, GeoQueries>();
        services.AddScoped<GeoJsonSeeder>();
        return services;
    }

    internal static void ConfigureNpgsql(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.UseNetTopologySuite();
                npgsql.EnableRetryOnFailure(maxRetryCount: 5);
                npgsql.MigrationsHistoryTable("__ef_migrations_history");
            })
            .UseSnakeCaseNamingConvention();

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configureConsumers = null)
    {
        services.AddScoped<IEventBus, MassTransitEventBus>();

        services.AddMassTransit(x =>
        {
            x.SetKebabCaseEndpointNameFormatter();
            configureConsumers?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                var rabbit = configuration.GetSection("RabbitMq");
                cfg.Host(rabbit["Host"] ?? "localhost", rabbit["VirtualHost"] ?? "/", h =>
                {
                    h.Username(rabbit["Username"] ?? "guest");
                    h.Password(rabbit["Password"] ?? "guest");
                });

                // Consumer-side resilience: exponential retries, then the message goes to the _error queue.
                cfg.UseMessageRetry(r => r.Exponential(5, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(2)));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>External telemetry providers, each behind a Polly resilience pipeline.</summary>
    public static IServiceCollection AddTelemetryProviders(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenMeteoOptions>(configuration.GetSection(OpenMeteoOptions.Section));

        services.AddHttpClient<IOpenMeteoClient, OpenMeteoClient>((sp, http) =>
            {
                http.BaseAddress = sp.GetRequiredService<IOptions<OpenMeteoOptions>>().Value.BaseUrl;
                http.DefaultRequestHeaders.UserAgent.ParseAdd("EcoStream-AI/0.1 (+https://github.com)");
            })
            .AddStandardResilienceHandler(o =>
            {
                o.Retry.MaxRetryAttempts = 4;
                o.Retry.UseJitter = true;
                o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                o.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30); // must be >= 2x attempt timeout
                o.CircuitBreaker.BreakDuration = TimeSpan.FromMinutes(1);
            });

        return services;
    }

    /// <summary>Shared OpenTelemetry setup; hosts add their own instrumentations (AspNetCore, Http...).</summary>
    public static IHostApplicationBuilder AddEcoStreamTelemetry(
        this IHostApplicationBuilder builder,
        string serviceName,
        Action<TracerProviderBuilder>? configureTracing = null)
    {
        builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t =>
            {
                t.AddSource("MassTransit")
                 .AddSource("Npgsql")
                 .AddOtlpExporter(); // honors OTEL_EXPORTER_OTLP_ENDPOINT (Jaeger: http://localhost:4317)
                configureTracing?.Invoke(t);
            });

        return builder;
    }
}
