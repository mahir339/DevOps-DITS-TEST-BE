using System.Collections.Concurrent;
using Catalog.Application.Abstractions;
using Catalog.Domain.Entities;

namespace Catalog.UnitTests.Fakes;

/// <summary>Test double for the EF Core repository, pre-filled with the same six products as the real seed.</summary>
public sealed class InMemoryProductRepository : IProductRepository
{
    private readonly ConcurrentDictionary<int, Product> _products = new();
    private int _nextId;

    public InMemoryProductRepository()
    {
        foreach (var (name, category, price) in new[]
                 {
                     ("Wireless Mouse", "Accessories", 24.99m), ("Mechanical Keyboard", "Accessories", 89.00m),
                     ("27-inch Monitor", "Displays", 279.99m), ("USB-C Hub", "Accessories", 39.50m),
                     ("Noise-Cancelling Headphones", "Audio", 199.00m), ("1080p Webcam", "Video", 59.99m),
                 })
        {
            Store(new Product(name, category, price, string.Empty));
        }
    }

    public int GetAllCalls { get; private set; }

    public Task<IReadOnlyList<Product>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        GetAllCalls++;
        return Task.FromResult<IReadOnlyList<Product>>(_products.Values.OrderBy(p => p.Id).ToList());
    }

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.GetValueOrDefault(id));

    public Task<Product> AddAsync(Product product, CancellationToken cancellationToken = default) =>
        Task.FromResult(Store(product));

    public Task UpdateAsync(Product product, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_products.TryRemove(id, out _));

    private Product Store(Product product)
    {
        product.Id = Interlocked.Increment(ref _nextId);
        _products[product.Id] = product;
        return product;
    }
}
