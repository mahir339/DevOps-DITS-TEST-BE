using Catalog.Application.Products;

namespace Catalog.Application.Abstractions;

/// <summary>
/// Read-through cache for product responses. Implementations must never throw: when the cache is
/// unavailable they behave like a cache miss, so the service keeps working (just slower).
/// </summary>
public interface IProductCache
{
    Task<IReadOnlyList<ProductResponse>?> GetAllAsync(CancellationToken cancellationToken = default);

    Task SetAllAsync(IReadOnlyList<ProductResponse> products, CancellationToken cancellationToken = default);

    Task<ProductResponse?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task SetAsync(ProductResponse product, CancellationToken cancellationToken = default);

    /// <summary>Removes the product (if given) and the cached product list.</summary>
    Task InvalidateAsync(int? id, CancellationToken cancellationToken = default);
}
