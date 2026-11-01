using EcoStream.Application.Abstractions;
using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace EcoStream.Infrastructure.Persistence;

internal sealed class TelemetryRepository(EcoStreamDbContext db) : ITelemetryRepository
{
    public async Task AddAsync(TelemetryReading reading, CancellationToken cancellationToken) =>
        await db.TelemetryReadings.AddAsync(reading, cancellationToken);

    public Task<bool> ExistsAsync(string stationCode, DateTimeOffset measuredAt, CancellationToken cancellationToken) =>
        db.TelemetryReadings.AnyAsync(r => r.StationCode == stationCode && r.MeasuredAt == measuredAt, cancellationToken);
}

internal sealed class StationRepository(EcoStreamDbContext db) : IStationRepository
{
    public Task<Station?> GetByCodeAsync(string code, CancellationToken cancellationToken)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return db.Stations.FirstOrDefaultAsync(s => s.Code == normalized, cancellationToken);
    }
}
