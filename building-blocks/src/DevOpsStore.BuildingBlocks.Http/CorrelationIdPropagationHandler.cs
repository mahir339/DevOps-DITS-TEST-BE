using DevOpsStore.BuildingBlocks.Web.Correlation;
using Microsoft.AspNetCore.Http;

namespace DevOpsStore.BuildingBlocks.Http;

/// <summary>Copies the current request's correlation id onto outgoing HttpClient requests.</summary>
public sealed class CorrelationIdPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!request.Headers.Contains(CorrelationId.HeaderName)
            && CorrelationId.Get(httpContextAccessor.HttpContext) is { } correlationId)
        {
            request.Headers.Add(CorrelationId.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
