using Ordering.Domain.Entities;

namespace Ordering.Application.Abstractions;

/// <summary>An integration event to publish through the outbox: routing key + payload.</summary>
public sealed record OutboxEvent(string RoutingKey, object Payload);

public interface IOrderRepository
{
    Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a new order and the event built from it (it needs the generated id) in ONE transaction.
    /// </summary>
    Task AddAsync(Order order, Func<Order, OutboxEvent> createEvent, CancellationToken cancellationToken = default);

    /// <summary>Saves changes to an order loaded with <see cref="GetByIdAsync"/> together with an event.</summary>
    Task UpdateAsync(Order order, OutboxEvent outboxEvent, CancellationToken cancellationToken = default);
}
