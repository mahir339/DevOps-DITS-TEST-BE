using Inventory.Domain.Entities;

namespace Inventory.Infrastructure.Persistence;

/// <summary>Starting stock for products 1-6 (the products seeded by catalog-service).</summary>
internal static class SeedData
{
    public static IEnumerable<StockItem> Stock()
    {
        var now = DateTimeOffset.UtcNow;
        int[] quantities = [50, 20, 8, 35, 12, 3];
        return quantities.Select((quantity, index) => new StockItem(index + 1, quantity, now));
    }
}
