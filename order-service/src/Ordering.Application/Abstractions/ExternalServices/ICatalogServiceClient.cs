namespace Ordering.Application.Abstractions.ExternalServices;

/// <summary>Calls catalog-service. Throws DownstreamUnavailableException when it cannot be reached.</summary>
public interface ICatalogServiceClient
{
    /// <returns>The product, or null if catalog-service says it does not exist.</returns>
    Task<ProductSnapshot?> GetProductAsync(int productId, CancellationToken cancellationToken = default);
}

public sealed record ProductSnapshot(int Id, string Name, decimal Price);
