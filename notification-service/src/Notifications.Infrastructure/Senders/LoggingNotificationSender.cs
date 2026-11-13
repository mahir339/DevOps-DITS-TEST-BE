using Microsoft.Extensions.Logging;
using Notifications.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Senders;

/// <summary>
/// Stand-in for a real email/SMS provider: writes the notification to the application log
/// so it shows up in the console / container logs.
/// </summary>
public sealed class LoggingNotificationSender(ILogger<LoggingNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        logger.LogInformation("Notification {Id} sent via {Channel} to {Recipient}: {Subject}",
            notification.Id, notification.Channel, notification.Recipient, notification.Subject);
        return Task.CompletedTask;
    }
}
