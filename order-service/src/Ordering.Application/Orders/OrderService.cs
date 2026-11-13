using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using DevOpsStore.BuildingBlocks.Results;
using Microsoft.Extensions.Logging;
using Ordering.Application.Abstractions;
using Ordering.Application.Abstractions.ExternalServices;
using Ordering.Application.Common;
using Ordering.Domain.Entities;
using Ordering.Domain.ValueObjects;

namespace Ordering.Application.Orders;

public interface IOrderService
{
    Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<OrderResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<OrderResponse>> PlaceOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default);

    Task<Result<OrderResponse>> CancelAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Places an order by calling the other services:
/// 1. customer-service     - check the customer exists
/// 2. catalog-service      - look up product names and prices
/// 3. inventory-service    - reserve stock (released again if a later item fails)
/// Then stores the order together with an OrderPlaced event (transactional outbox). The outbox
/// dispatcher publishes it to RabbitMQ; notification-service sends the confirmation email from it.
/// </summary>
public sealed class OrderService(
    IOrderRepository repository,
    ICustomerServiceClient customers,
    ICatalogServiceClient catalog,
    IInventoryServiceClient inventory,
    TimeProvider timeProvider,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<IReadOnlyList<OrderResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken)).Select(OrderResponse.FromEntity).ToList();

    public async Task<Result<OrderResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetByIdAsync(id, cancellationToken);
        return order is null ? Error.NotFound($"Order {id} was not found.") : OrderResponse.FromEntity(order);
    }

    public async Task<Result<OrderResponse>> PlaceOrderAsync(CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (OrderValidator.Validate(request) is { } validationError) return validationError;

        try
        {
            var customer = await customers.GetCustomerAsync(request.CustomerId, cancellationToken);
            if (customer is null)
                return Error.BadRequest("Invalid order", $"Customer {request.CustomerId} was not found.");

            var lines = new List<OrderLine>();
            foreach (var item in MergeDuplicateItems(request.Items!))
            {
                var product = await catalog.GetProductAsync(item.ProductId, cancellationToken);
                if (product is null)
                    return Error.BadRequest("Invalid order", $"Product {item.ProductId} was not found.");

                lines.Add(new OrderLine(product.Id, product.Name, item.Quantity, product.Price));
            }

            if (await ReserveStockAsync(lines, cancellationToken) is { } stockError) return stockError;

            var order = Order.Place(customer.Id, $"{customer.FirstName} {customer.LastName}", customer.Email, lines, timeProvider.GetUtcNow());
            try
            {
                await repository.AddAsync(order, o => new OutboxEvent(EventNames.OrderPlaced, new OrderPlacedIntegrationEvent(
                    o.Id, o.CustomerId, o.CustomerName, o.CustomerEmail, o.Total, ToEventLines(o), o.CreatedAt)), cancellationToken);
            }
            catch
            {
                await ReleaseAsync(lines); // the order was not stored, so give the stock back
                throw;
            }

            logger.LogInformation("Order {OrderId} placed for customer {CustomerId}, total {Total}", order.Id, order.CustomerId, order.Total);
            return OrderResponse.FromEntity(order);
        }
        catch (DownstreamUnavailableException ex)
        {
            logger.LogError(ex, "Order failed because {Service} is unavailable", ex.ServiceName);
            return Error.Unavailable(ex.Message);
        }
    }

    private static IEnumerable<OrderItemRequest> MergeDuplicateItems(IEnumerable<OrderItemRequest> items) =>
        items.GroupBy(i => i.ProductId).Select(g => new OrderItemRequest(g.Key, g.Sum(i => i.Quantity)));

    /// <summary>Reserves every line, or none: earlier reservations are released if a later one fails.</summary>
    private async Task<Error?> ReserveStockAsync(IReadOnlyList<OrderLine> lines, CancellationToken cancellationToken)
    {
        var reserved = new List<OrderLine>();
        try
        {
            foreach (var line in lines)
            {
                var outcome = await inventory.ReserveAsync(line.ProductId, line.Quantity, cancellationToken);
                if (outcome == ReservationOutcome.InsufficientStock)
                {
                    await ReleaseAsync(reserved);
                    return Error.Conflict("Insufficient stock", $"Not enough stock for '{line.ProductName}'.");
                }
                reserved.Add(line);
            }
            return null;
        }
        catch
        {
            await ReleaseAsync(reserved);
            throw;
        }
    }

    private async Task ReleaseAsync(IEnumerable<OrderLine> lines)
    {
        foreach (var line in lines)
        {
            try
            {
                await inventory.ReleaseAsync(line.ProductId, line.Quantity, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not release {Quantity} unit(s) of product {ProductId}", line.Quantity, line.ProductId);
            }
        }
    }

    public async Task<Result<OrderResponse>> CancelAsync(int id, CancellationToken cancellationToken = default)
    {
        var order = await repository.GetByIdAsync(id, cancellationToken);
        if (order is null) return Error.NotFound($"Order {id} was not found.");
        if (!order.Cancel(timeProvider.GetUtcNow()))
            return Error.Conflict("Order already cancelled", $"Order {id} was cancelled at {order.CancelledAt:u}.");

        // inventory-service puts the stock back and notification-service emails the customer, both from this event.
        await repository.UpdateAsync(order, new OutboxEvent(EventNames.OrderCancelled, new OrderCancelledIntegrationEvent(
            order.Id, order.CustomerId, order.CustomerName, order.CustomerEmail, order.Total, ToEventLines(order), order.CancelledAt!.Value)),
            cancellationToken);

        logger.LogInformation("Order {OrderId} cancelled", order.Id);
        return OrderResponse.FromEntity(order);
    }

    private static List<OrderLineEvent> ToEventLines(Order order) =>
        order.Lines.Select(l => new OrderLineEvent(l.ProductId, l.ProductName, l.Quantity, l.UnitPrice)).ToList();
}
