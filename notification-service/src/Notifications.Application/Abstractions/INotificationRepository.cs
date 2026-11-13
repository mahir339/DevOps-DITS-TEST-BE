using Notifications.Domain.Entities;

namespace Notifications.Application.Abstractions;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForMessageAsync(Guid messageId, CancellationToken cancellationToken = default);

    Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default);
}
