using Microsoft.EntityFrameworkCore;
using Notifications.Application.Abstractions;
using Notifications.Domain.Entities;

namespace Notifications.Infrastructure.Persistence;

public sealed class EfNotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Notifications.AsNoTracking().OrderByDescending(n => n.Id).Take(500).ToListAsync(cancellationToken);

    public Task<Notification?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Notifications.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id, cancellationToken);

    public Task<bool> ExistsForMessageAsync(Guid messageId, CancellationToken cancellationToken = default) =>
        db.Notifications.AnyAsync(n => n.SourceMessageId == messageId, cancellationToken);

    public async Task<Notification> AddAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        db.Notifications.Add(notification);
        await db.SaveChangesAsync(cancellationToken);
        return notification;
    }
}
