using Inventory.Application.Abstractions;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Inventory.Infrastructure.Persistence;

public sealed class EfStockRepository(InventoryDbContext db) : IStockRepository
{
    public async Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.StockItems.AsNoTracking().OrderBy(s => s.ProductId).ToListAsync(cancellationToken);

    public Task<StockItem?> GetAsync(int productId, CancellationToken cancellationToken = default) =>
        db.StockItems.AsNoTracking().FirstOrDefaultAsync(s => s.ProductId == productId, cancellationToken);

    public async Task<StockItem> SetQuantityAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // INSERT ... ON CONFLICT DO UPDATE: safe when two requests create the same record at once.
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO stock_items (product_id, quantity, updated_at)
            VALUES ({productId}, {quantity}, {now})
            ON CONFLICT (product_id) DO UPDATE SET quantity = EXCLUDED.quantity, updated_at = EXCLUDED.updated_at
            """, cancellationToken);
        return (await GetAsync(productId, cancellationToken))!;
    }

    public async Task<(ReserveOutcome Outcome, StockItem? Item)> TryReserveAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // One conditional UPDATE: the database checks and decrements in a single step, so concurrent
        // reservations (even from several replicas) can never take the quantity below zero.
        var updated = await db.StockItems
            .Where(s => s.ProductId == productId && s.Quantity >= quantity)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Quantity, x => x.Quantity - quantity)
                .SetProperty(x => x.UpdatedAt, now), cancellationToken);

        var item = await GetAsync(productId, cancellationToken);
        if (item is null) return (ReserveOutcome.NotFound, null);
        return (updated == 1 ? ReserveOutcome.Reserved : ReserveOutcome.InsufficientStock, item);
    }

    public async Task<StockItem?> ReleaseAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var updated = await db.StockItems
            .Where(s => s.ProductId == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Quantity, x => x.Quantity + quantity)
                .SetProperty(x => x.UpdatedAt, now), cancellationToken);

        return updated == 0 ? null : await GetAsync(productId, cancellationToken);
    }
}
