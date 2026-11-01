using System.Diagnostics;
using EcoStream.Application.Abstractions;
using EcoStream.Application.Telemetry.Commands;
using EcoStream.Infrastructure.Telemetry.OpenMeteo;
using MediatR;
using Microsoft.Extensions.Options;

namespace EcoStream.Ingestion.Worker;

public sealed class IngestionOptions
{
    public const string Section = "Ingestion";

    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxParallelism { get; set; } = 4;
}

/// <summary>
/// Polls Open-Meteo for every active OpenMeteo station and dispatches <see cref="IngestTelemetryCommand"/>.
/// Each station runs in its own DI scope (DbContext is not thread-safe) and failures are isolated per station.
/// </summary>
internal sealed partial class TelemetryPollingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<IngestionOptions> options,
    ILogger<TelemetryPollingWorker> logger) : BackgroundService
{
    public const string ActivitySourceName = "EcoStream.Ingestion";
    private static readonly ActivitySource Activity = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.Interval);
        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogCycleFailed(ex);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        using var activity = Activity.StartActivity("ingestion.poll");

        IReadOnlyList<StationDto> stations;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            stations = await scope.ServiceProvider.GetRequiredService<IStationQueries>().ListAsync(onlyActive: true, ct);
        }

        var targets = stations.Where(s => s.Provider == "OpenMeteo").ToList();
        var recorded = 0;
        var anomalies = 0;

        await Parallel.ForEachAsync(targets, new ParallelOptions { MaxDegreeOfParallelism = options.Value.MaxParallelism, CancellationToken = ct },
            async (station, token) =>
            {
                try
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var client = scope.ServiceProvider.GetRequiredService<IOpenMeteoClient>();
                    var sender = scope.ServiceProvider.GetRequiredService<ISender>();

                    var r = await client.GetCurrentAsync(station.Latitude, station.Longitude, token);
                    var result = await sender.Send(
                        new IngestTelemetryCommand(station.Code, r.MeasuredAt, r.RainfallMmPerHour, r.WindGustKmh, r.PressureHpa), token);

                    if (result.Outcome == IngestOutcome.Recorded) Interlocked.Increment(ref recorded);
                    if (result.AnomalyDetected) Interlocked.Increment(ref anomalies);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    LogStationFailed(ex, station.Code);
                }
            });

        activity?.SetTag("stations", targets.Count).SetTag("recorded", recorded).SetTag("anomalies", anomalies);
        LogCycle(targets.Count, recorded, anomalies);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Polled {Stations} stations: {Recorded} new readings, {Anomalies} anomalies")]
    private partial void LogCycle(int stations, int recorded, int anomalies);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to ingest station {StationCode}")]
    private partial void LogStationFailed(Exception ex, string stationCode);

    [LoggerMessage(Level = LogLevel.Error, Message = "Polling cycle failed")]
    private partial void LogCycleFailed(Exception ex);
}
