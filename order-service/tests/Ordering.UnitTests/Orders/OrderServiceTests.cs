using DevOpsStore.BuildingBlocks.Results;
using Microsoft.Extensions.Logging.Abstractions;
using Ordering.Application.Orders;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using Ordering.UnitTests.Fakes;

namespace Ordering.UnitTests.Orders;

public class OrderServiceTests
{
    private readonly FakeCustomerClient _customers = new();
    private readonly FakeCatalogClient _catalog = new();
    private readonly FakeInventoryClient _inventory = new();
    private readonly FakeOrderRepository _orders = new();
    private readonly OrderService _service;

    public OrderServiceTests()
    {
        _service = new OrderService(
            _orders,
            _customers,
            _catalog,
            _inventory,
            TimeProvider.System,
            NullLogger<OrderService>.Instance);
    }

    private static CreateOrderRequest Request(int customerId, params (int ProductId, int Quantity)[] items) =>
        new(customerId, items.Select(i => new OrderItemRequest(i.ProductId, i.Quantity)).ToList());

    [Fact]
    public async Task PlaceOrder_Valid_CreatesOrderReservesStockAndQueuesEvent()
    {
        var result = await _service.PlaceOrderAsync(Request(1, (1, 2), (3, 1)));

        Assert.True(result.IsSuccess);
        var order = result.Value;
        Assert.Equal(1, order.Id);
        Assert.Equal("Alex Rivera", order.CustomerName);
        Assert.Equal(329.97m, order.Total);
        Assert.Equal(48, _inventory.Stock[1]);
        Assert.Equal(7, _inventory.Stock[3]);
        var evt = Assert.Single(_orders.Events);
        Assert.Equal(EventNames.OrderPlaced, evt.RoutingKey);
        var placed = Assert.IsType<OrderPlacedIntegrationEvent>(evt.Payload);
        Assert.Equal(("alex.rivera@example.com", 329.97m), (placed.CustomerEmail, placed.Total));
    }

    [Fact]
    public async Task PlaceOrder_DuplicateProductLines_AreMerged()
    {
        var result = await _service.PlaceOrderAsync(Request(1, (1, 1), (1, 2)));

        var line = Assert.Single(result.Value.Items);
        Assert.Equal(3, line.Quantity);
    }

    [Fact]
    public async Task PlaceOrder_UnknownCustomer_ReturnsBadRequest()
    {
        var result = await _service.PlaceOrderAsync(Request(999, (1, 1)));

        Assert.Equal(ErrorType.BadRequest, result.Error!.Type);
        Assert.Contains("Customer 999", result.Error.Detail);
    }

    [Fact]
    public async Task PlaceOrder_UnknownProduct_ReturnsBadRequest()
    {
        var result = await _service.PlaceOrderAsync(Request(1, (999, 1)));

        Assert.Equal(ErrorType.BadRequest, result.Error!.Type);
    }

    [Fact]
    public async Task PlaceOrder_InsufficientStock_ReleasesEarlierReservations()
    {
        var result = await _service.PlaceOrderAsync(Request(1, (1, 1), (6, 10)));

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(50, _inventory.Stock[1]);
        Assert.Equal((1, 1), Assert.Single(_inventory.Released));
        Assert.Empty(await _service.GetAllAsync());
    }

    [Fact]
    public async Task PlaceOrder_InventoryUnavailable_ReturnsUnavailable()
    {
        _inventory.Unavailable = true;

        var result = await _service.PlaceOrderAsync(Request(1, (1, 1)));

        Assert.Equal(ErrorType.Unavailable, result.Error!.Type);
        Assert.Contains("inventory-service", result.Error.Detail);
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(1, 0, 1)]
    [InlineData(1, 1, 0)]
    [InlineData(1, 1, 101)]
    public async Task PlaceOrder_InvalidRequest_ReturnsValidationError(int customerId, int productId, int quantity)
    {
        var result = await _service.PlaceOrderAsync(Request(customerId, (productId, quantity)));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task PlaceOrder_NoItems_ReturnsValidationError()
    {
        var result = await _service.PlaceOrderAsync(new CreateOrderRequest(1, []));

        Assert.Contains("items", result.Error!.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Cancel_ConfirmedOrder_SetsStatusAndQueuesEvent()
    {
        var placed = await _service.PlaceOrderAsync(Request(1, (3, 2)));

        var result = await _service.CancelAsync(placed.Value.Id);

        Assert.Equal("Cancelled", result.Value.Status);
        Assert.NotNull(result.Value.CancelledAt);
        var cancelled = Assert.IsType<OrderCancelledIntegrationEvent>(_orders.Events[^1].Payload);
        Assert.Equal(2, Assert.Single(cancelled.Lines).Quantity);
    }

    [Fact]
    public async Task Cancel_Twice_ReturnsConflict()
    {
        var placed = await _service.PlaceOrderAsync(Request(1, (1, 1)));
        await _service.CancelAsync(placed.Value.Id);

        var second = await _service.CancelAsync(placed.Value.Id);

        Assert.Equal(ErrorType.Conflict, second.Error!.Type);
    }

    [Fact]
    public async Task Cancel_UnknownOrder_ReturnsNotFound()
    {
        Assert.Equal(ErrorType.NotFound, (await _service.CancelAsync(42)).Error!.Type);
    }
}
