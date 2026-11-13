using Notifications.Domain.Entities;

namespace Notifications.Application.Notifications;

public sealed record NotificationRequest(string Recipient, string Subject, string Message, string? Channel);

/// <summary>A notification triggered by a broker event.</summary>
public sealed record EventNotification(Guid MessageId, int OrderId, string Type, string Recipient, string Subject, string Message);

public sealed record NotificationResponse(int Id, string Recipient, string Subject, string Message, string Channel, string Type, int? OrderId, DateTimeOffset CreatedAt)
{
    public static NotificationResponse FromEntity(Notification n) =>
        new(n.Id, n.Recipient, n.Subject, n.Message, n.Channel, n.Type, n.OrderId, n.CreatedAt);
}
