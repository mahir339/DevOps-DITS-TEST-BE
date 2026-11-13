using Catalog.Domain.Entities;

namespace Catalog.Application.Products;

public sealed record ProductRequest(string Name, string Category, decimal Price, string? Description);

public sealed record ProductResponse(int Id, string Name, string Category, decimal Price, string Description)
{
    public static ProductResponse FromEntity(Product product) =>
        new(product.Id, product.Name, product.Category, product.Price, product.Description);
}
