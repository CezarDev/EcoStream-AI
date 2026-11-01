using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;

namespace EcoStream.Application.Abstractions;

// ---- Write side (aggregates) ----

public interface ITelemetryRepository
{
    Task AddAsync(TelemetryReading reading, CancellationToken cancellationToken);

    /// <summary>Idempotency guard: providers are polled more often than they refresh.</summary>
    Task<bool> ExistsAsync(string stationCode, DateTimeOffset measuredAt, CancellationToken cancellationToken);
}

public interface IStationRepository
{
    Task<Station?> GetByCodeAsync(string code, CancellationToken cancellationToken);
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

/// <summary>Port for publishing integration events (implemented with MassTransit/RabbitMQ).</summary>
public interface IEventBus
{
    Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class;
}
