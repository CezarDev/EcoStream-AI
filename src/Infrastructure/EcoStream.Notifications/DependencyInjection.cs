using EcoStream.Contracts;
using EcoStream.Notifications.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EcoStream.Notifications;

public static class DependencyInjection
{
    /// <summary>Registers one keyed <see cref="INotificationChannel"/> per channel name in <see cref="AlertChannels"/>.</summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        foreach (var channel in AlertChannels.All)
        {
            services.AddKeyedSingleton<INotificationChannel>(channel, (sp, _) =>
                new LogOnlyNotificationChannel(channel, sp.GetRequiredService<ILogger<LogOnlyNotificationChannel>>()));
        }
        return services;
    }
}
