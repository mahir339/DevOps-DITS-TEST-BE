using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace DevOpsStore.BuildingBlocks.Web.Correlation;

/// <summary>
/// Reads the X-Correlation-Id request header (or creates a new id), echoes it on the response
/// and adds it to the logging scope of everything logged during the request.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[CorrelationId.HeaderName].FirstOrDefault();
        var correlationId = CorrelationId.IsValid(incoming) ? incoming! : CorrelationId.NewId();

        context.Items[CorrelationId.ItemKey] = correlationId;
        // Write the (possibly new) id back onto the request so proxies such as the gateway forward it downstream.
        context.Request.Headers[CorrelationId.HeaderName] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationId.HeaderName] = correlationId;
            return Task.CompletedTask;
        });

        using (logger.BeginScope("CorrelationId:{CorrelationId}", correlationId))
        {
            await next(context);
        }
    }
}
