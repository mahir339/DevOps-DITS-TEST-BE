using System.Collections.Concurrent;
using Notifications.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.UnitTests.Fakes;

/// <summary>Test double for the EF Core repository.</summary>
public sealed class InMemoryNotificationRepository : INotificationRepository
{
    private readonly ConcurrentDictionary<int, Notification> _notifications = new();
    private int _nextId;

    public Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Notification>>(_notifications.Values.OrderByDescending(n => n.Id).ToList());

    public Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_notifications.GetValueOrDefault(id));

    public Task<bool> ExistsForMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_notifications.Values.Any(n => n.SourceMessageId == messageId));

    public Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        notification.Id = Interlocked.Increment(ref _nextId);
        _notifications[notification.Id] = notification;
        return Task.FromResult(notification);
    }
}
