using DevOpsStore.BuildingBlocks.Results;
using Inventory.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Inventory.Application.Stock;

public interface IStockService
{
    Task<IReadOnlyList<StockItemResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<StockItemResponse>> GetAsync(int productId, CancellationToken cancellationToken = default);

    Task<Result<StockItemResponse>> SetQuantityAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default);

    Task<Result<StockItemResponse>> ReserveAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default);

    Task<Result<StockItemResponse>> ReleaseAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default);
}

public sealed class StockService(IStockRepository repository, TimeProvider timeProvider, ILogger<StockService> logger) : IStockService
{
    public async Task<IReadOnlyList<StockItemResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken)).Select(StockItemResponse.FromEntity).ToList();

    public async Task<Result<StockItemResponse>> GetAsync(int productId, CancellationToken cancellationToken = default)
    {
        var item = await repository.GetAsync(productId, cancellationToken);
        return item is null ? NotFound(productId) : StockItemResponse.FromEntity(item);
    }

    /// <summary>Sets the stock level, creating the record if the product has none yet.</summary>
    public async Task<Result<StockItemResponse>> SetQuantityAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default)
    {
        if (Validate(request, allowZero: true) is { } error) return error;

        var item = await repository.SetQuantityAsync(productId, request.Quantity, timeProvider.GetUtcNow(), cancellationToken);
        logger.LogInformation("Stock for product {ProductId} set to {Quantity}", productId, request.Quantity);
        return StockItemResponse.FromEntity(item);
    }

    public async Task<Result<StockItemResponse>> ReserveAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default)
    {
        if (Validate(request, allowZero: false) is { } error) return error;

        var (outcome, item) = await repository.TryReserveAsync(productId, request.Quantity, timeProvider.GetUtcNow(), cancellationToken);
        switch (outcome)
        {
            case ReserveOutcome.NotFound:
                return NotFound(productId);
            case ReserveOutcome.InsufficientStock:
                return Error.Conflict("Insufficient stock", $"Only {item?.Quantity ?? 0} unit(s) of product {productId} available.");
            default:
                logger.LogInformation("Reserved {Quantity} unit(s) of product {ProductId}; {Remaining} left", request.Quantity, productId, item!.Quantity);
                return StockItemResponse.FromEntity(item);
        }
    }

    public async Task<Result<StockItemResponse>> ReleaseAsync(int productId, QuantityRequest request, CancellationToken cancellationToken = default)
    {
        if (Validate(request, allowZero: false) is { } error) return error;

        var item = await repository.ReleaseAsync(productId, request.Quantity, timeProvider.GetUtcNow(), cancellationToken);
        if (item is null) return NotFound(productId);

        logger.LogInformation("Released {Quantity} unit(s) of product {ProductId}", request.Quantity, productId);
        return StockItemResponse.FromEntity(item);
    }

    private static Error? Validate(QuantityRequest request, bool allowZero) => new ValidationBuilder()
        .AddIf(allowZero ? request.Quantity < 0 : request.Quantity <= 0, "quantity",
            allowZero ? "Quantity cannot be negative." : "Quantity must be greater than zero.")
        .ToError();

    private static Error NotFound(int productId) => Error.NotFound($"No stock record for product {productId}.");
}
