namespace Inventory.Domain.Entities;

/// <summary>
/// Stock level for one product. Reservations against the database are done with a single
/// conditional UPDATE (see the repository), so two replicas can never oversell.
/// </summary>
public sealed class StockItem
{
    public StockItem(int productId, int quantity, DateTimeOffset updatedAt)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        ProductId = productId;
        Quantity = quantity;
        UpdatedAt = updatedAt;
    }

    public int ProductId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>Removes <paramref name="quantity"/> units if enough are available.</summary>
    public bool TryReserve(int quantity, DateTimeOffset now)
    {
        if (Quantity < quantity) return false;
        Quantity -= quantity;
        UpdatedAt = now;
        return true;
    }

    public void Release(int quantity, DateTimeOffset now)
    {
        Quantity += quantity;
        UpdatedAt = now;
    }

    public void SetQuantity(int quantity, DateTimeOffset now)
    {
        if (quantity < 0) throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity cannot be negative.");
        Quantity = quantity;
        UpdatedAt = now;
    }
}
