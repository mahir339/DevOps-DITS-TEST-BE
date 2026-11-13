using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace DevOpsStore.BuildingBlocks.Http;

/// <summary>
/// Copies the caller's "Authorization: Bearer ..." header onto outgoing HttpClient requests,
/// so a downstream service authorizes the call as the same user.
/// </summary>
public sealed class BearerTokenPropagationHandler(IHttpContextAccessor httpContextAccessor) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var incoming = httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.Authorization].FirstOrDefault();

        if (request.Headers.Authorization is null
            && incoming is not null
            && incoming.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.Authorization, incoming);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
