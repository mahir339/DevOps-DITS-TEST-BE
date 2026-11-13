using Inventory.Domain.Entities;

namespace Inventory.Application.Abstractions;

public enum ReserveOutcome
{
    Reserved,
    InsufficientStock,
    NotFound,
}

public interface IStockRepository
{
    Task<IReadOnlyList<StockItem>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<StockItem?> GetAsync(int productId, CancellationToken cancellationToken = default);

    /// <summary>Sets the stock level, creating the record if the product has none yet.</summary>
    Task<StockItem> SetQuantityAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Atomically takes <paramref name="quantity"/> units, only if that many are available.</summary>
    Task<(ReserveOutcome Outcome, StockItem? Item)> TryReserveAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Atomically puts units back. Returns null when the product has no stock record.</summary>
    Task<StockItem?> ReleaseAsync(int productId, int quantity, DateTimeOffset now, CancellationToken cancellationToken = default);
}
