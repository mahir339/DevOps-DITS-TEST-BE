using System.Net;
using System.Net.Http.Json;
using Ordering.Application.Abstractions.ExternalServices;

namespace Ordering.Infrastructure.Http;

public sealed class CustomerServiceClient(IHttpClientFactory factory)
    : DownstreamClient(factory, DownstreamServiceNames.Customer), ICustomerServiceClient
{
    public Task<CustomerSnapshot?> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<CustomerSnapshot>($"api/customers/{customerId}", cancellationToken);
}

public sealed class CatalogServiceClient(IHttpClientFactory factory)
    : DownstreamClient(factory, DownstreamServiceNames.Catalog), ICatalogServiceClient
{
    public Task<ProductSnapshot?> GetProductAsync(int productId, CancellationToken cancellationToken = default) =>
        GetOrDefaultAsync<ProductSnapshot>($"api/products/{productId}", cancellationToken);
}

public sealed class InventoryServiceClient(IHttpClientFactory factory)
    : DownstreamClient(factory, DownstreamServiceNames.Inventory), IInventoryServiceClient
{
    public async Task<ReservationOutcome> ReserveAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await SendAsync(client,
            c => c.PostAsJsonAsync($"api/inventory/{productId}/reserve", new { quantity }, cancellationToken),
            cancellationToken);

        // 409 = not enough stock, 404 = product has no stock record at all.
        if (response.StatusCode is HttpStatusCode.Conflict or HttpStatusCode.NotFound)
            return ReservationOutcome.InsufficientStock;

        EnsureSuccess(client, response);
        return ReservationOutcome.Reserved;
    }

    public async Task ReleaseAsync(int productId, int quantity, CancellationToken cancellationToken = default)
    {
        var client = CreateClient();
        using var response = await SendAsync(client,
            c => c.PostAsJsonAsync($"api/inventory/{productId}/release", new { quantity }, cancellationToken),
            cancellationToken);
        EnsureSuccess(client, response);
    }
}
