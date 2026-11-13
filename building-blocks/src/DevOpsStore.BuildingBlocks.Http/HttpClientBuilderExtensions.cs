using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;

namespace DevOpsStore.BuildingBlocks.Http;

public static class HttpClientBuilderExtensions
{
    /// <summary>Forwards the incoming X-Correlation-Id header on every request made by this HttpClient.</summary>
    public static IHttpClientBuilder AddCorrelationIdPropagation(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddTransient<CorrelationIdPropagationHandler>();
        return builder.AddHttpMessageHandler<CorrelationIdPropagationHandler>();
    }

    /// <summary>Forwards the caller's bearer token on every request made by this HttpClient.</summary>
    public static IHttpClientBuilder AddBearerTokenPropagation(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddTransient<BearerTokenPropagationHandler>();
        return builder.AddHttpMessageHandler<BearerTokenPropagationHandler>();
    }

    /// <summary>
    /// Adds retries (GET/HEAD/OPTIONS only, so a POST is never sent twice), a circuit breaker and
    /// per-attempt timeouts. <paramref name="attemptTimeout"/> is how long one try may take.
    /// </summary>
    public static IHttpClientBuilder AddDownstreamResilience(this IHttpClientBuilder builder, TimeSpan attemptTimeout)
    {
        builder.AddStandardResilienceHandler(options =>
        {
            options.Retry.DisableForUnsafeHttpMethods();
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.FromMilliseconds(200);
            options.AttemptTimeout.Timeout = attemptTimeout;
            options.TotalRequestTimeout.Timeout = attemptTimeout * 3 + TimeSpan.FromSeconds(1);
            // The breaker's sampling window must be at least twice the attempt timeout.
            options.CircuitBreaker.SamplingDuration = attemptTimeout * 2 + TimeSpan.FromSeconds(1);
        });
        return builder;
    }

    /// <summary>Sets the base address (always ending in "/") and timeout of a downstream-service client.</summary>
    public static IHttpClientBuilder ConfigureDownstream(this IHttpClientBuilder builder, Func<IServiceProvider, string> baseUrl, Func<IServiceProvider, TimeSpan> timeout)
    {
        return builder.ConfigureHttpClient((serviceProvider, client) =>
        {
            client.BaseAddress = new Uri(baseUrl(serviceProvider).TrimEnd('/') + "/");
            // HttpClient.Timeout covers all retries; the resilience handler enforces the per-attempt limit.
            client.Timeout = timeout(serviceProvider) * 4;
        });
    }
}
