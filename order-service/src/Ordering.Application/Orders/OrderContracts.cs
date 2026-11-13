using Ordering.Domain.Entities;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders;

public sealed record CreateOrderRequest(int CustomerId, List<OrderItemRequest>? Items);

public sealed record OrderItemRequest(int ProductId, int Quantity);

public sealed record OrderLineResponse(int ProductId, string ProductName, int Quantity, decimal UnitPrice, decimal LineTotal)
{
    public static OrderLineResponse FromValueObject(OrderLine line) =>
        new(line.ProductId, line.ProductName, line.Quantity, line.UnitPrice, line.LineTotal);
}

public sealed record OrderResponse(
    int Id,
    int CustomerId,
    string CustomerName,
    IReadOnlyList<OrderLineResponse> Items,
    decimal Total,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CancelledAt)
{
    public static OrderResponse FromEntity(Order order) => new(
        order.Id,
        order.CustomerId,
        order.CustomerName,
        order.Lines.Select(OrderLineResponse.FromValueObject).ToList(),
        order.Total,
        order.Status,
        order.CreatedAt,
        order.CancelledAt);
}
