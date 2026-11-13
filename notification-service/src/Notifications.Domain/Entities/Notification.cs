namespace Notifications.Domain.Entities;

public static class NotificationTypes
{
    public const string Manual = "Manual";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderCancelled = "OrderCancelled";
}

public sealed class Notification
{
    public const string DefaultChannel = "email";

    public Notification(string recipient, string subject, string message, string? channel, DateTimeOffset createdAt,
        string type = NotificationTypes.Manual, int? orderId = null, Guid? sourceMessageId = null)
    {
        Recipient = recipient;
        Subject = subject;
        Message = message;
        Channel = string.IsNullOrWhiteSpace(channel) ? DefaultChannel : channel.Trim().ToLowerInvariant();
        CreatedAt = createdAt;
        Type = type;
        OrderId = orderId;
        SourceMessageId = sourceMessageId;
    }

    /// <summary>Assigned by the database when the notification is stored.</summary>
    public int Id { get; set; }

    public string Recipient { get; private set; }

    public string Subject { get; private set; }

    public string Message { get; private set; }

    public string Channel { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Manual, OrderConfirmed or OrderCancelled.</summary>
    public string Type { get; private set; }

    public int? OrderId { get; private set; }

    /// <summary>Id of the broker message that created it (null for manual ones). Unique, so redeliveries are ignored.</summary>
    public Guid? SourceMessageId { get; private set; }
}
