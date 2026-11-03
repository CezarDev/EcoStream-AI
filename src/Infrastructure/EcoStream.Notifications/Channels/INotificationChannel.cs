namespace EcoStream.Notifications.Channels;

/// <summary>One implementation per channel (Telegram, Twilio SMS/Voice, Slack/Teams webhook).</summary>
public interface INotificationChannel
{
    string Name { get; }

    Task SendAsync(string title, string body, int severity, CancellationToken cancellationToken);
}
