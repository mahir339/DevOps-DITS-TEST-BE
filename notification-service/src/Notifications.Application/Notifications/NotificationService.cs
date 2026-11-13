using DevOpsStore.BuildingBlocks.Results;
using Notifications.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.Application.Notifications;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<NotificationResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<NotificationResponse>> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default);

    /// <summary>Idempotent: a message id that was already handled is ignored.</summary>
    Task SendForEventAsync(EventNotification draft, CancellationToken cancellationToken = default);
}

public sealed class NotificationService(
    INotificationRepository repository,
    INotificationSender sender,
    TimeProvider timeProvider) : INotificationService
{
    public async Task<IReadOnlyList<NotificationResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken)).Select(NotificationResponse.FromEntity).ToList();

    public async Task<Result<NotificationResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var notification = await repository.GetByIdAsync(id, cancellationToken);
        return notification is null
            ? Error.NotFound($"Notification {id} was not found.")
            : NotificationResponse.FromEntity(notification);
    }

    public async Task<Result<NotificationResponse>> SendAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        var error = new ValidationBuilder()
            .AddIf(string.IsNullOrWhiteSpace(request.Recipient), "recipient", "Recipient is required.")
            .AddIf(string.IsNullOrWhiteSpace(request.Subject), "subject", "Subject is required.")
            .AddIf(string.IsNullOrWhiteSpace(request.Message), "message", "Message is required.")
            .ToError();
        if (error is not null) return error;

        var notification = new Notification(
            request.Recipient.Trim(),
            request.Subject.Trim(),
            request.Message.Trim(),
            request.Channel,
            timeProvider.GetUtcNow());

        var stored = await repository.AddAsync(notification, cancellationToken);
        await sender.SendAsync(stored, cancellationToken);
        return NotificationResponse.FromEntity(stored);
    }

    public async Task SendForEventAsync(EventNotification draft, CancellationToken cancellationToken = default)
    {
        if (await repository.ExistsForMessageAsync(draft.MessageId, cancellationToken)) return;

        var notification = new Notification(draft.Recipient, draft.Subject, draft.Message, Notification.DefaultChannel,
            timeProvider.GetUtcNow(), draft.Type, draft.OrderId, draft.MessageId);
        var stored = await repository.AddAsync(notification, cancellationToken);
        await sender.SendAsync(stored, cancellationToken);
    }
}
