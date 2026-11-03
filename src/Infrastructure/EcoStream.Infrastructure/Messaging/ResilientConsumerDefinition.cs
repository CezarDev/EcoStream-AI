using EcoStream.Domain.Common;
using EcoStream.Infrastructure.Persistence;
using FluentValidation;
using MassTransit;

namespace EcoStream.Infrastructure.Messaging;

/// <summary>
/// Consumer policy shared by every pipeline step, applied wherever the consumer is configured:
/// <list type="number">
///   <item>Delayed redelivery (1, 5, 15 min) for longer outages — via RabbitMQ delayed-exchange plugin.</item>
///   <item>Immediate exponential retry for transient faults.</item>
///   <item>EF Core inbox/outbox: duplicates are dropped by MessageId and messages published by the consumer
///         are committed atomically with its DB work.</item>
/// </list>
/// After that the message lands in <c>&lt;queue&gt;_error</c>.
/// </summary>
public sealed class ResilientConsumerDefinition<TConsumer> : ConsumerDefinition<TConsumer>
    where TConsumer : class, IConsumer
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<TConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseDelayedRedelivery(r =>
        {
            r.Intervals(TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15));
            IgnorePermanentFailures(r);
        });

        endpointConfigurator.UseMessageRetry(r =>
        {
            r.Exponential(5, TimeSpan.FromMilliseconds(200), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500));
            IgnorePermanentFailures(r);
        });

        endpointConfigurator.UseEntityFrameworkOutbox<EcoStreamDbContext>(context);
    }

    // Bad data will not get better by retrying: send straight to _error.
    private static void IgnorePermanentFailures(IRetryConfigurator r)
    {
        r.Ignore<ValidationException>();
        r.Ignore<DomainException>();
        r.Ignore<ArgumentException>();
    }
}
