using Microsoft.Extensions.Logging;

namespace EcoStream.Notifications.Channels;

/// <summary>Phase 2 stand-in: proves routing end to end without real credentials. Replaced per channel in Phase 4.</summary>
internal sealed partial class LogOnlyNotificationChannel(string name, ILogger<LogOnlyNotificationChannel> logger) : INotificationChannel
{
    public string Name => name;

    public Task SendAsync(string title, string body, int severity, CancellationToken cancellationToken)
    {
        LogSend(name, severity, title, body);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "[{Channel}] severity {Severity}: {Title}\n{Body}")]
    private partial void LogSend(string channel, int severity, string title, string body);
}
