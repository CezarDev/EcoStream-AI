using EcoStream.Agents.Worker.Consumers;
using EcoStream.Application;
using EcoStream.Application.Abstractions;
using EcoStream.Contracts;
using EcoStream.Notifications;
using EcoStream.Notifications.Channels;
using FluentAssertions;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace EcoStream.Agents.Worker.Tests;

/// <summary>
/// Runs the whole rule-based chain on MassTransit's in-memory transport:
/// AnomalyDetected → AffectedAreasIdentified → RiskAssessed → AlertDispatchRequested → channel.
/// (Outbox/inbox and RabbitMQ topology are infrastructure concerns, exercised against the real broker.)
/// </summary>
public sealed class IncidentPipelineTests : IAsyncLifetime
{
    private readonly IGeoQueries _geo = Substitute.For<IGeoQueries>();
    private readonly INotificationChannel _sms = Substitute.For<INotificationChannel>();
    private ServiceProvider _provider = null!;
    private ITestHarness _harness = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddLogging()
            .AddIncidentPipeline()
            .AddNotifications()
            .AddSingleton(_geo)
            .AddKeyedSingleton(AlertChannels.Sms, _sms) // last registration wins for the key
            .AddMassTransitTestHarness(x =>
            {
                x.AddConsumer<AnomalyDetectedConsumer>();
                x.AddConsumer<AffectedAreasConsumer>();
                x.AddConsumer<RiskAssessedConsumer>();
                x.AddConsumer<AlertDispatchConsumer>();
            });

        _provider = services.BuildServiceProvider(validateScopes: true);
        _harness = _provider.GetRequiredService<ITestHarness>();
        await _harness.Start();
    }

    public async Task DisposeAsync()
    {
        await _harness.Stop();
        await _provider.DisposeAsync();
    }

    private static AnomalyDetectedIntegrationEvent Storm(Guid id) => new(id, new AnomalySnapshot(
        "POA-CENTRO", -30.033, -51.23, 85, 60, 1000, ["rainfall 85 mm/h"], DateTimeOffset.UtcNow));

    [Fact]
    public async Task Severe_storm_over_flood_zone_reaches_every_channel_including_sms()
    {
        _geo.FindNeighborhoodsWithinAsync(default, default, default, default).ReturnsForAnyArgs(
        [
            new NeighborhoodProximityDto("Centro Histórico", "Porto Alegre", 39_000, 0, InFloodZone: true),
            new NeighborhoodProximityDto("Cidade Baixa", "Porto Alegre", 16_000, 800, InFloodZone: false),
        ]);
        var id = Guid.NewGuid();

        await _harness.Bus.Publish(Storm(id));

        (await _harness.Published.Any<RiskAssessed>(m => m.Context.Message.ReadingId == id && m.Context.Message.Severity == 5))
            .Should().BeTrue();
        (await _harness.Consumed.Any<AlertDispatchRequested>(m => m.Context.Message.Channel == AlertChannels.Sms))
            .Should().BeTrue();
        _harness.Published.Select<AlertDispatchRequested>().Select(m => m.Context.Message.Channel)
            .Should().BeEquivalentTo(AlertChannels.All);
        await _sms.Received(1).SendAsync(Arg.Is<string>(t => t.Contains("5/5")), Arg.Any<string>(), 5, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Mild_anomaly_far_from_people_only_notifies_ops()
    {
        _geo.FindNeighborhoodsWithinAsync(default, default, default, default).ReturnsForAnyArgs([]);
        var id = Guid.NewGuid();
        var mild = Storm(id) with { Anomaly = Storm(id).Anomaly with { RainfallMmPerHour = 55 } };

        await _harness.Bus.Publish(mild);

        (await _harness.Consumed.Any<AlertDispatchRequested>(m => m.Context.Message.ReadingId == id)).Should().BeTrue();
        _harness.Published.Select<AlertDispatchRequested>(m => m.Context.Message.ReadingId == id)
            .Select(m => m.Context.Message.Channel).Should().Equal(AlertChannels.Slack);
        await _sms.DidNotReceiveWithAnyArgs().SendAsync(default!, default!, default, default);
    }
}
