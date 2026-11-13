using Notifications.Domain.Entities;

namespace Notifications.Application.Abstractions;

/// <summary>Delivers a notification (email, SMS, ...). Implemented in the Infrastructure layer.</summary>
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
