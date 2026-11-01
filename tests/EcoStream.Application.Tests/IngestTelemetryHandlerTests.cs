using EcoStream.Application.Abstractions;
using EcoStream.Application.Common;
using EcoStream.Application.Telemetry.Commands;
using EcoStream.Contracts;
using EcoStream.Domain.Geo;
using EcoStream.Domain.Stations;
using EcoStream.Domain.Telemetry;
using FluentAssertions;
using NSubstitute;

namespace EcoStream.Application.Tests;

public class IngestTelemetryHandlerTests
{
    private readonly IStationRepository _stations = Substitute.For<IStationRepository>();
    private readonly ITelemetryRepository _readings = Substitute.For<ITelemetryRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IEventBus _bus = Substitute.For<IEventBus>();
    private readonly IngestTelemetryHandler _handler;

    private static readonly DateTimeOffset At = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    public IngestTelemetryHandlerTests()
    {
        _handler = new IngestTelemetryHandler(_stations, _readings, _uow, _bus);
        _stations.GetByCodeAsync("A801", Arg.Any<CancellationToken>())
            .Returns(Station.Register("A801", "Jardim Botânico", new GeoPoint(-30.05, -51.17), TelemetryProvider.OpenMeteo));
    }

    [Fact]
    public async Task Unknown_station_throws_not_found()
    {
        var act = () => _handler.Handle(new IngestTelemetryCommand("NOPE", At, 1, 1, 1010), default);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Duplicate_reading_is_skipped_without_saving()
    {
        _readings.ExistsAsync("A801", At, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(new IngestTelemetryCommand("A801", At, 80, 1, 1010), default);

        result.Outcome.Should().Be(IngestOutcome.Duplicate);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _bus.DidNotReceiveWithAnyArgs().PublishAsync<AnomalyDetectedIntegrationEvent>(default!, default);
    }

    [Fact]
    public async Task Severe_reading_is_saved_and_published_with_station_coordinates()
    {
        var result = await _handler.Handle(new IngestTelemetryCommand("A801", At, 72.5, 104, 1000), default);

        result.Should().Match<IngestTelemetryResult>(r => r.Outcome == IngestOutcome.Recorded && r.AnomalyDetected);
        await _readings.Received(1).AddAsync(Arg.Any<TelemetryReading>(), Arg.Any<CancellationToken>());
        await _bus.Received(1).PublishAsync(
            Arg.Is<AnomalyDetectedIntegrationEvent>(e => e.StationCode == "A801" && e.Latitude == -30.05 && e.Reasons.Count == 2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Normal_reading_is_saved_but_not_published()
    {
        var result = await _handler.Handle(new IngestTelemetryCommand("A801", At, 2, 20, 1012), default);

        result.AnomalyDetected.Should().BeFalse();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _bus.DidNotReceiveWithAnyArgs().PublishAsync<AnomalyDetectedIntegrationEvent>(default!, default);
    }
}
