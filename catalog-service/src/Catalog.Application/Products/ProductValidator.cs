using DevOpsStore.BuildingBlocks.Results;

namespace Catalog.Application.Products;

public static class ProductValidator
{
    public static Error? Validate(ProductRequest request) => new ValidationBuilder()
        .AddIf(string.IsNullOrWhiteSpace(request.Name), "name", "Name is required.")
        .AddIf(request.Name?.Length > 100, "name", "Name must be 100 characters or fewer.")
        .AddIf(string.IsNullOrWhiteSpace(request.Category), "category", "Category is required.")
        .AddIf(request.Price <= 0, "price", "Price must be greater than zero.")
        .ToError();
}
