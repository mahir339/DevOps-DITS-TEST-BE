using DevOpsStore.BuildingBlocks.Messaging.Outbox;
using DevOpsStore.BuildingBlocks.Web.Correlation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Ordering.Application.Abstractions;
using Ordering.Domain.Entities;

namespace Ordering.Infrastructure.Persistence;

/// <summary>Stores orders and their outbox events in the same transaction.</summary>
public sealed class EfOrderRepository(OrderingDbContext db, IHttpContextAccessor http, TimeProvider timeProvider) : IOrderRepository
{
    public async Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Orders.AsNoTracking().OrderByDescending(o => o.Id).ToListAsync(cancellationToken);

    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Orders.FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task AddAsync(Order order, Func<Order, OutboxEvent> createEvent, CancellationToken cancellationToken = default) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.Orders.Add(order);
            await db.SaveChangesAsync(ct); // assigns order.Id, which the event needs
            AddToOutbox(createEvent(order));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }, cancellationToken);

    public async Task UpdateAsync(Order order, OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        AddToOutbox(outboxEvent);
        await db.SaveChangesAsync(cancellationToken); // one SaveChanges = one transaction
    }

    private void AddToOutbox(OutboxEvent outboxEvent) =>
        db.AddOutboxMessage(outboxEvent.RoutingKey, outboxEvent.Payload, CorrelationId.Get(http.HttpContext), timeProvider.GetUtcNow());
}
