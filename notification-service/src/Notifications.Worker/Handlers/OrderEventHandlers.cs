using System.Globalization;
using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using Notifications.Application.Notifications;
using Notifications.Domain.Entities;

namespace Notifications.Worker.Handlers;

public sealed class OrderPlacedHandler(INotificationService notifications) : IIntegrationEventHandler<OrderPlacedIntegrationEvent>
{
    public Task HandleAsync(OrderPlacedIntegrationEvent e, MessageContext context, CancellationToken cancellationToken) =>
        notifications.SendForEventAsync(new EventNotification(
            context.MessageId, e.OrderId, NotificationTypes.OrderConfirmed, e.CustomerEmail,
            $"Order #{e.OrderId} confirmed",
            $"Hi {e.CustomerName}, your order #{e.OrderId} totaling ${e.Total.ToString("0.00", CultureInfo.InvariantCulture)} has been confirmed."),
            cancellationToken);
}

public sealed class OrderCancelledHandler(INotificationService notifications) : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    public Task HandleAsync(OrderCancelledIntegrationEvent e, MessageContext context, CancellationToken cancellationToken) =>
        notifications.SendForEventAsync(new EventNotification(
            context.MessageId, e.OrderId, NotificationTypes.OrderCancelled, e.CustomerEmail,
            $"Order #{e.OrderId} cancelled",
            $"Hi {e.CustomerName}, your order #{e.OrderId} has been cancelled."),
            cancellationToken);
}
