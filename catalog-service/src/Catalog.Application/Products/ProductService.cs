using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;
using DevOpsStore.BuildingBlocks.Results;

namespace Catalog.Application.Products;

public interface IProductService
{
    Task<IReadOnlyList<ProductResponse>> GetAllAsync(string? category, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default);

    Task<Result<ProductResponse>> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

/// <summary>Product use cases. Reads go through the cache first; every write invalidates it.</summary>
public sealed class ProductService(IProductRepository repository, IProductCache cache) : IProductService
{
    public async Task<IReadOnlyList<ProductResponse>> GetAllAsync(string? category, CancellationToken cancellationToken = default)
    {
        var products = await cache.GetAllAsync(cancellationToken);
        if (products is null)
        {
            products = (await repository.GetAllAsync(cancellationToken)).Select(ProductResponse.FromEntity).ToList();
            await cache.SetAllAsync(products, cancellationToken);
        }

        return products
            .Where(p => string.IsNullOrWhiteSpace(category)
                        || p.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    public async Task<Result<ProductResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        if (await cache.GetAsync(id, cancellationToken) is { } cached) return cached;

        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null) return NotFound(id);

        var response = ProductResponse.FromEntity(product);
        await cache.SetAsync(response, cancellationToken);
        return response;
    }

    public async Task<Result<ProductResponse>> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        if (ProductValidator.Validate(request) is { } error) return error;

        var product = new Product(request.Name.Trim(), request.Category.Trim(), request.Price, request.Description?.Trim() ?? string.Empty);
        var created = await repository.AddAsync(product, cancellationToken);
        await cache.InvalidateAsync(null, cancellationToken);
        return ProductResponse.FromEntity(created);
    }

    public async Task<Result<ProductResponse>> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        if (ProductValidator.Validate(request) is { } error) return error;

        var product = await repository.GetByIdAsync(id, cancellationToken);
        if (product is null) return NotFound(id);

        product.Update(request.Name.Trim(), request.Category.Trim(), request.Price, request.Description?.Trim() ?? string.Empty);
        await repository.UpdateAsync(product, cancellationToken);
        await cache.InvalidateAsync(id, cancellationToken);
        return ProductResponse.FromEntity(product);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (!await repository.DeleteAsync(id, cancellationToken)) return NotFound(id);

        await cache.InvalidateAsync(id, cancellationToken);
        return Result.Success();
    }

    private static Error NotFound(int id) => Error.NotFound($"Product {id} was not found.");
}
