using EcoStream.Application.Abstractions;
using MassTransit;

namespace EcoStream.Infrastructure.Messaging;

internal sealed class MassTransitEventBus(IPublishEndpoint publishEndpoint) : IEventBus
{
    public Task PublishAsync<TMessage>(TMessage message, CancellationToken cancellationToken)
        where TMessage : class =>
        publishEndpoint.Publish(message, cancellationToken);
}
