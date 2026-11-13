using Catalog.Application.Abstractions;
using Catalog.Application.Products;

namespace Catalog.UnitTests.Fakes;

/// <summary>In-process stand-in for the Redis cache.</summary>
public sealed class DictionaryProductCache : IProductCache
{
    private IReadOnlyList<ProductResponse>? _all;
    private readonly Dictionary<int, ProductResponse> _items = new();

    public int Invalidations { get; private set; }

    public Task<IReadOnlyList<ProductResponse>?> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult(_all);

    public Task SetAllAsync(IReadOnlyList<ProductResponse> products, CancellationToken cancellationToken = default)
    {
        _all = products;
        return Task.CompletedTask;
    }

    public Task<ProductResponse?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_items.GetValueOrDefault(id));

    public Task SetAsync(ProductResponse product, CancellationToken cancellationToken = default)
    {
        _items[product.Id] = product;
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(int? id, CancellationToken cancellationToken = default)
    {
        Invalidations++;
        _all = null;
        if (id is { } productId) _items.Remove(productId);
        return Task.CompletedTask;
    }
}
