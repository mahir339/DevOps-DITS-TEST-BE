using Ordering.Application.Abstractions.ExternalServices;
using Ordering.Application.Abstractions;
using Ordering.Application.Common;
using Ordering.Domain.Entities;

namespace Ordering.UnitTests.Fakes;

internal sealed class FakeCustomerClient : ICustomerServiceClient
{
    public Dictionary<int, CustomerSnapshot> Customers { get; } = new()
    {
        [1] = new CustomerSnapshot(1, "Alex", "Rivera", "alex.rivera@example.com"),
    };

    public Task<CustomerSnapshot?> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Customers.GetValueOrDefault(customerId));
}

internal sealed class FakeCatalogClient : ICatalogServiceClient
{
    public Dictionary<int, ProductSnapshot> Products { get; } = new()
    {
        [1] = new ProductSnapshot(1, "Wireless Mouse", 24.99m),
        [3] = new ProductSnapshot(3, "27-inch Monitor", 279.99m),
        [6] = new ProductSnapshot(6, "1080p Webcam", 59.99m),
    };

    public Task<ProductSnapshot?> GetProductAsync(int productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Products.GetValueOrDefault(productId));
}

internal sealed class FakeInventoryClient : IInventoryServiceClient
{
    public Dictionary<int, int> Stock { get; } = new() { [1] = 50, [3] = 8, [6] = 3 };

    public bool Unavailable { get; set; }

    public List<(int ProductId, int Quantity)> Released { get; } = [];

    public Task<ReservationOutcome> ReserveAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        if (Unavailable) throw new DownstreamUnavailableException("inventory-service", new Uri("http://inventory/"), "connection refused");
        if (Stock.GetValueOrDefault(productId) < quantity) return Task.FromResult(ReservationOutcome.InsufficientStock);
        Stock[productId] -= quantity;
        return Task.FromResult(ReservationOutcome.Reserved);
    }

    public Task ReleaseAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        Stock[productId] += quantity;
        Released.Add((productId, quantity));
        return Task.CompletedTask;
    }
}

internal sealed class FakeOrderRepository : IOrderRepository
{
    private readonly List<Order> _orders = [];

    public List<OutboxEvent> Events { get; } = [];

    public Task<IReadOnlyList<Order>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Order>>(_orders.OrderByDescending(o => o.Id).ToList());

    public Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_orders.FirstOrDefault(o => o.Id == id));

    public Task AddAsync(Order order, Func<Order, OutboxEvent> createEvent, CancellationToken cancellationToken = default)
    {
        order.Id = _orders.Count + 1;
        _orders.Add(order);
        Events.Add(createEvent(order));
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Order order, OutboxEvent outboxEvent, CancellationToken cancellationToken = default)
    {
        Events.Add(outboxEvent);
        return Task.CompletedTask;
    }
}
