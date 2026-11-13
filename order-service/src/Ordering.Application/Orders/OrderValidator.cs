using DevOpsStore.BuildingBlocks.Results;

namespace Ordering.Application.Orders;

public static class OrderValidator
{
    public const int MaxQuantityPerItem = 100;

    public static Error? Validate(CreateOrderRequest request) => new ValidationBuilder()
        .AddIf(request.CustomerId <= 0, "customerId", "Customer is required.")
        .AddIf(request.Items is null || request.Items.Count == 0, "items", "At least one item is required.")
        .AddIf(request.Items?.Any(i => i.ProductId <= 0 || i.Quantity is <= 0 or > MaxQuantityPerItem) == true,
            "items", $"Each item needs a valid product and a quantity between 1 and {MaxQuantityPerItem}.")
        .ToError();
}
