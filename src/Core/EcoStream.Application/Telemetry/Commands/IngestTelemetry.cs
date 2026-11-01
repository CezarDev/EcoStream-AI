using EcoStream.Application.Abstractions;
using EcoStream.Application.Common;
using EcoStream.Contracts;
using EcoStream.Domain.Geo;
using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;
using FluentValidation;
using MediatR;

namespace EcoStream.Application.Telemetry.Commands;

/// <summary>
/// Records one reading for a registered station. Coordinates come from the station, not the payload,
/// so a misconfigured sensor cannot "move" a station.
/// </summary>
public sealed record IngestTelemetryCommand(
    string StationCode,
    DateTimeOffset MeasuredAt,
    double RainfallMmPerHour,
    double WindGustKmh,
    double PressureHpa) : IRequest<IngestTelemetryResult>;

public enum IngestOutcome
{
    Recorded,
    Duplicate,
}

public sealed record IngestTelemetryResult(Guid? ReadingId, IngestOutcome Outcome, bool AnomalyDetected);

public sealed class IngestTelemetryValidator : AbstractValidator<IngestTelemetryCommand>
{
    public IngestTelemetryValidator()
    {
        RuleFor(x => x.StationCode).NotEmpty().MaximumLength(Station.CodeMaxLength);
        RuleFor(x => x.MeasuredAt).LessThanOrEqualTo(_ => DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("MeasuredAt cannot be in the future.");
        RuleFor(x => x.RainfallMmPerHour).InclusiveBetween(0, 500);
        RuleFor(x => x.WindGustKmh).InclusiveBetween(0, 400);
        RuleFor(x => x.PressureHpa).InclusiveBetween(800, 1100);
    }
}

internal sealed class IngestTelemetryHandler(
    IStationRepository stations,
    ITelemetryRepository readings,
    IUnitOfWork unitOfWork,
    IEventBus eventBus) : IRequestHandler<IngestTelemetryCommand, IngestTelemetryResult>
{
    public async Task<IngestTelemetryResult> Handle(IngestTelemetryCommand request, CancellationToken cancellationToken)
    {
        var station = await stations.GetByCodeAsync(request.StationCode, cancellationToken)
            ?? throw new NotFoundException("Station", request.StationCode);

        var measuredAt = request.MeasuredAt.ToUniversalTime();
        if (await readings.ExistsAsync(station.Code, measuredAt, cancellationToken))
        {
            return new IngestTelemetryResult(null, IngestOutcome.Duplicate, AnomalyDetected: false);
        }

        var reading = TelemetryReading.Record(
            station.Code,
            station.Location,
            measuredAt,
            request.RainfallMmPerHour,
            request.WindGustKmh,
            request.PressureHpa,
            AnomalyThresholds.Default);

        await readings.AddAsync(reading, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Phase 2 replaces this direct publish with the MassTransit transactional outbox.
        var anomaly = reading.DomainEvents.OfType<SevereAnomalyDetected>().SingleOrDefault();
        if (anomaly is not null)
        {
            await eventBus.PublishAsync(ToIntegrationEvent(anomaly, reading), cancellationToken);
        }

        reading.ClearDomainEvents();
        return new IngestTelemetryResult(reading.Id, IngestOutcome.Recorded, anomaly is not null);
    }

    private static AnomalyDetectedIntegrationEvent ToIntegrationEvent(SevereAnomalyDetected e, TelemetryReading r) =>
        new(e.ReadingId, e.StationCode,
            e.Location.Latitude, e.Location.Longitude,
            r.RainfallMmPerHour, r.WindGustKmh, r.PressureHpa,
            e.Reasons, e.OccurredAt);
}
