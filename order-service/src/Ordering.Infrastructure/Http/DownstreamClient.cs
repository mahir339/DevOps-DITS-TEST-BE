using System.Net;
using System.Net.Http.Json;
using Ordering.Application.Common;

namespace Ordering.Infrastructure.Http;

/// <summary>Shared plumbing for the typed clients: turns network failures into <see cref="DownstreamUnavailableException"/>.</summary>
public abstract class DownstreamClient(IHttpClientFactory httpClientFactory, string serviceName)
{
    protected HttpClient CreateClient() => httpClientFactory.CreateClient(serviceName);

    protected async Task<T?> GetOrDefaultAsync<T>(string path, CancellationToken cancellationToken) where T : class
    {
        var client = CreateClient();
        using var response = await SendAsync(client, c => c.GetAsync(path, cancellationToken), cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        EnsureSuccess(client, response);
        return await response.Content.ReadFromJsonAsync<T>(cancellationToken);
    }

    protected async Task<HttpResponseMessage> SendAsync(
        HttpClient client,
        Func<HttpClient, Task<HttpResponseMessage>> send,
        CancellationToken cancellationToken)
    {
        try
        {
            return await send(client);
        }
        catch (HttpRequestException ex)
        {
            throw new DownstreamUnavailableException(serviceName, client.BaseAddress, ex.Message, ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new DownstreamUnavailableException(serviceName, client.BaseAddress, $"timed out after {client.Timeout.TotalSeconds:0}s", ex);
        }
    }

    protected void EnsureSuccess(HttpClient client, HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
            throw new DownstreamUnavailableException(serviceName, client.BaseAddress, $"returned HTTP {(int)response.StatusCode}");
    }
}
