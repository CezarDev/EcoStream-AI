using EcoStream.Contracts;
using MassTransit;
using RabbitMQ.Client;

namespace EcoStream.Infrastructure.Messaging;

/// <summary>
/// Exchange names and types. Applied in every host so publishers and consumers agree on the topology.
/// </summary>
public static class MessagingTopology
{
    public const string AnomalyDetectedExchange = "ecostream.anomaly-detected";
    public const string AffectedAreasExchange = "ecostream.affected-areas";
    public const string RiskAssessedExchange = "ecostream.risk-assessed";
    public const string AlertsExchange = "ecostream.alerts";

    public static string AlertRoutingKey(int severity, string channel) => $"alerts.{severity}.{channel}";

    internal static void Configure(IRabbitMqBusFactoryConfigurator cfg)
    {
        // Pipeline events: fanout (MassTransit default) — every interested service gets a copy.
        cfg.Message<AnomalyDetectedIntegrationEvent>(m => m.SetEntityName(AnomalyDetectedExchange));
        cfg.Message<AffectedAreasIdentified>(m => m.SetEntityName(AffectedAreasExchange));
        cfg.Message<RiskAssessed>(m => m.SetEntityName(RiskAssessedExchange));

        // Alerts: topic exchange, routing key alerts.{severity}.{channel}.
        cfg.Message<AlertDispatchRequested>(m => m.SetEntityName(AlertsExchange));
        cfg.Publish<AlertDispatchRequested>(p => p.ExchangeType = ExchangeType.Topic);
        cfg.Send<AlertDispatchRequested>(s => s.UseRoutingKeyFormatter(c => AlertRoutingKey(c.Message.Severity, c.Message.Channel)));
    }

    /// <summary>
    /// Declares a queue bound to the alerts topic exchange with the given pattern
    /// (e.g. <c>alerts.*.sms</c>) instead of MassTransit's default fanout binding.
    /// </summary>
    public static void AlertsEndpoint<TConsumer>(
        this IRabbitMqBusFactoryConfigurator cfg, IBusRegistrationContext context, string queueName, string routingKeyPattern)
        where TConsumer : class, IConsumer
    {
        cfg.ReceiveEndpoint(queueName, e =>
        {
            e.ConfigureConsumeTopology = false;
            e.Bind(AlertsExchange, b =>
            {
                b.ExchangeType = ExchangeType.Topic;
                b.RoutingKey = routingKeyPattern;
            });
            e.ConfigureConsumer<TConsumer>(context);
        });
    }
}
