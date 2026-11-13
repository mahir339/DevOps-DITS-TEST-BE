using Inventory.Domain.Entities;

namespace Inventory.Application.Stock;

public sealed record QuantityRequest(int Quantity);

public sealed record StockItemResponse(int ProductId, int Quantity, DateTimeOffset UpdatedAt)
{
    public static StockItemResponse FromEntity(StockItem item) => new(item.ProductId, item.Quantity, item.UpdatedAt);
}
