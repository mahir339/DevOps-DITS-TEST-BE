using System.Text.Json;
using Catalog.Application.Abstractions;
using Catalog.Application.Products;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Catalog.Infrastructure.Caching;

/// <summary>
/// Caches product JSON in Redis. Any Redis error is logged and treated as a miss, and Redis is then
/// skipped for 30 seconds so a dead cache does not add a timeout to every request.
/// </summary>
public sealed class RedisProductCache(
    IDistributedCache cache,
    IOptions<CacheOptions> options,
    TimeProvider timeProvider,
    ILogger<RedisProductCache> logger) : IProductCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static long _skipUntilTicks;

    private string ListKey => $"{options.Value.KeyPrefix}products:all";

    private string ItemKey(int id) => $"{options.Value.KeyPrefix}products:{id}";

    public Task<IReadOnlyList<ProductResponse>?> GetAllAsync(CancellationToken cancellationToken = default) =>
        ReadAsync<IReadOnlyList<ProductResponse>>(ListKey, cancellationToken);

    public Task SetAllAsync(IReadOnlyList<ProductResponse> products, CancellationToken cancellationToken = default) =>
        WriteAsync(ListKey, products, cancellationToken);

    public Task<ProductResponse?> GetAsync(int id, CancellationToken cancellationToken = default) =>
        ReadAsync<ProductResponse>(ItemKey(id), cancellationToken);

    public Task SetAsync(ProductResponse product, CancellationToken cancellationToken = default) =>
        WriteAsync(ItemKey(product.Id), product, cancellationToken);

    public async Task InvalidateAsync(int? id, CancellationToken cancellationToken = default)
    {
        await TryAsync(() => cache.RemoveAsync(ListKey, cancellationToken), "remove", ListKey);
        if (id is { } productId)
            await TryAsync(() => cache.RemoveAsync(ItemKey(productId), cancellationToken), "remove", ItemKey(productId));
    }

    private async Task<T?> ReadAsync<T>(string key, CancellationToken cancellationToken) where T : class
    {
        byte[]? bytes = null;
        await TryAsync(async () => bytes = await cache.GetAsync(key, cancellationToken), "read", key);
        return bytes is null ? null : JsonSerializer.Deserialize<T>(bytes, JsonOptions);
    }

    private Task WriteAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        var entry = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(options.Value.ProductTtlSeconds),
        };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, JsonOptions);
        return TryAsync(() => cache.SetAsync(key, bytes, entry, cancellationToken), "write", key);
    }

    private async Task TryAsync(Func<Task> action, string operation, string key)
    {
        if (timeProvider.GetUtcNow().UtcTicks < Interlocked.Read(ref _skipUntilTicks)) return;
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Interlocked.Exchange(ref _skipUntilTicks, timeProvider.GetUtcNow().AddSeconds(30).UtcTicks);
            logger.LogWarning("Redis {Operation} of {Key} failed ({Error}); skipping the cache for 30 s", operation, key, ex.Message);
        }
    }
}

/// <summary>Used when Cache:Enabled is false.</summary>
public sealed class NoProductCache : IProductCache
{
    public Task<IReadOnlyList<ProductResponse>?> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ProductResponse>?>(null);

    public Task SetAllAsync(IReadOnlyList<ProductResponse> products, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<ProductResponse?> GetAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult<ProductResponse?>(null);

    public Task SetAsync(ProductResponse product, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InvalidateAsync(int? id, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
