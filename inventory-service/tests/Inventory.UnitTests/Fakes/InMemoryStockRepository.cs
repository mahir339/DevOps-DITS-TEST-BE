using System.Collections.Concurrent;
using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;

namespace Inventory.UnitTests.Fakes;

/// <summary>Thread-safe test double. Starting stock matches the real seed (products 1-6).</summary>
public sealed class InMemoryStockRepository : IStockRepository
{
    private readonly ConcurrentDictionary<int, StockItem> _stock = new();
    private readonly Lock _sync = new();

    public InMemoryStockRepository()
    {
        int[] quantities = [50, 20, 8, 35, 12, 3];
        for (var i = 0; i < quantities.Length; i++)
            _stock[i + 1] = new StockItem(i + 1, quantities[i], DateTimeOffset.UtcNow);
    }

    public Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<StockItem>>(_stock.Values.OrderBy(s => s.ProductId).ToList());

    public Task<StockItem?> GetAsync(int productId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_stock.GetValueOrDefault(productId));

    public Task<StockItem> SetQuantityAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var item = _stock.GetOrAdd(productId, id => new StockItem(id, quantity, now));
            item.SetQuantity(quantity, now);
            return Task.FromResult(item);
        }
    }

    public Task<(ReserveOutcome Outcome, StockItem? Item)> TryReserveAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_stock.TryGetValue(productId, out var item)) return Task.FromResult<(ReserveOutcome, StockItem?)>((ReserveOutcome.NotFound, null));
            var outcome = item.TryReserve(quantity, now) ? ReserveOutcome.Reserved : ReserveOutcome.InsufficientStock;
            return Task.FromResult<(ReserveOutcome, StockItem?)>((outcome, item));
        }
    }

    public Task<StockItem?> ReleaseAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (!_stock.TryGetValue(productId, out var item)) return Task.FromResult<StockItem?>(null);
            item.Release(quantity, now);
            return Task.FromResult<StockItem?>(item);
        }
    }
}
