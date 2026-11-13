namespace DevOpsStore.BuildingBlocks.Messaging.Contracts;

/// <summary>
/// Routing keys of the events exchanged between services. Changing a name or a payload is a
/// breaking change for every consumer: publish a new package version and update all services.
/// </summary>
public static class EventNames
{
    public const string OrderPlaced = "ordering.order-placed.v1";
    public const string OrderCancelled = "ordering.order-cancelled.v1";
}

public sealed record OrderLineEvent(int ProductId, string ProductName, int Quantity, decimal UnitPrice);

/// <summary>Published by order-service after an order is stored (via its outbox).</summary>
public sealed record OrderPlacedIntegrationEvent(
    int OrderId,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderLineEvent> Lines,
    DateTimeOffset PlacedAt);

/// <summary>Published by order-service when an admin cancels an order. Inventory puts the stock back.</summary>
public sealed record OrderCancelledIntegrationEvent(
    int OrderId,
    int CustomerId,
    string CustomerName,
    string CustomerEmail,
    decimal Total,
    IReadOnlyList<OrderLineEvent> Lines,
    DateTimeOffset CancelledAt);
