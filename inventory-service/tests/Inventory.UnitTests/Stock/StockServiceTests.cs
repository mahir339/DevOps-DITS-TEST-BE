using DevOpsStore.BuildingBlocks.Results;
using Inventory.Application.Stock;
using Inventory.UnitTests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.UnitTests.Stock;

public class StockServiceTests
{
    private readonly StockService _service = new(new InMemoryStockRepository(), TimeProvider.System, NullLogger<StockService>.Instance);

    [Fact]
    public async Task Reserve_WithEnoughStock_DecreasesQuantity()
    {
        var result = await _service.ReserveAsync(1, new QuantityRequest(5));

        Assert.True(result.IsSuccess);
        Assert.Equal(45, result.Value.Quantity);
    }

    [Fact]
    public async Task Reserve_MoreThanAvailable_ReturnsConflictAndKeepsStock()
    {
        var result = await _service.ReserveAsync(6, new QuantityRequest(10));

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
        Assert.Equal(3, (await _service.GetAsync(6)).Value.Quantity);
    }

    [Fact]
    public async Task Reserve_UnknownProduct_ReturnsNotFound()
    {
        var result = await _service.ReserveAsync(999, new QuantityRequest(1));

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Reserve_NonPositiveQuantity_ReturnsValidationError(int quantity)
    {
        var result = await _service.ReserveAsync(1, new QuantityRequest(quantity));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Release_IncreasesQuantity()
    {
        var result = await _service.ReleaseAsync(2, new QuantityRequest(5));

        Assert.Equal(25, result.Value.Quantity);
    }

    [Fact]
    public async Task SetQuantity_NewProduct_CreatesRecord()
    {
        var result = await _service.SetQuantityAsync(42, new QuantityRequest(7));

        Assert.True(result.IsSuccess);
        Assert.Equal(7, (await _service.GetAsync(42)).Value.Quantity);
    }

    [Fact]
    public async Task SetQuantity_Negative_ReturnsValidationError()
    {
        var result = await _service.SetQuantityAsync(1, new QuantityRequest(-5));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    [Fact]
    public async Task Reserve_ConcurrentRequests_NeverOversells()
    {
        // Product 6 starts with 3 units; 20 parallel requests for 1 unit each.
        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(_ => Task.Run(() => _service.ReserveAsync(6, new QuantityRequest(1)))));

        Assert.Equal(3, results.Count(r => r.IsSuccess));
        Assert.Equal(0, (await _service.GetAsync(6)).Value.Quantity);
    }
}
