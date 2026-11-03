using EcoStream.Contracts;
using EcoStream.Notifications.Channels;
using MassTransit;

namespace EcoStream.Agents.Worker.Consumers;

/// <summary>
/// Bound to one queue per channel on the alerts topic exchange (notifications-slack ← alerts.*.slack, …).
/// The inbox guarantees a redelivered message does not send the same SMS twice.
/// </summary>
public sealed class AlertDispatchConsumer(IServiceProvider services) : IConsumer<AlertDispatchRequested>
{
    public Task Consume(ConsumeContext<AlertDispatchRequested> context)
    {
        var m = context.Message;
        var channel = services.GetKeyedService<INotificationChannel>(m.Channel)
            ?? throw new ArgumentException($"No notification channel registered for '{m.Channel}'.");

        return channel.SendAsync(m.Title, m.Body, m.Severity, context.CancellationToken);
    }
}
