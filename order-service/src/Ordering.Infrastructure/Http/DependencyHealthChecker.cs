using Ordering.Application.Abstractions.ExternalServices;

namespace Ordering.Infrastructure.Http;

/// <summary>Calls GET /health on every downstream service using the same HttpClients the order flow uses.</summary>
public sealed class DependencyHealthChecker(IHttpClientFactory httpClientFactory) : IDependencyHealthChecker
{
    public async Task<IReadOnlyList<DependencyStatus>> CheckAllAsync(CancellationToken cancellationToken = default) =>
        await Task.WhenAll(DownstreamServiceNames.All.Select(name => CheckAsync(name, cancellationToken)));

    private async Task<DependencyStatus> CheckAsync(string serviceName, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(serviceName);
        var url = client.BaseAddress?.ToString();
        try
        {
            using var response = await client.GetAsync("health", cancellationToken);
            return new DependencyStatus(serviceName, url, response.IsSuccessStatusCode, (int)response.StatusCode, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new DependencyStatus(serviceName, url, false, null, ex.Message);
        }
    }
}
