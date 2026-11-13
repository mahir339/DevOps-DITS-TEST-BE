using Ordering.Application.Abstractions.ExternalServices;

namespace Ordering.Api.Endpoints;

public static class DependencyEndpoints
{
    /// <summary>GET /health/dependencies: can order-service reach every service it calls? (503 if not)</summary>
    public static IEndpointRouteBuilder MapDependencyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/dependencies", async (IDependencyHealthChecker checker, CancellationToken ct) =>
        {
            var dependencies = await checker.CheckAllAsync(ct);
            var healthy = dependencies.All(d => d.Healthy);
            return Results.Json(
                new { status = healthy ? "Healthy" : "Degraded", dependencies },
                statusCode: healthy ? StatusCodes.Status200OK : StatusCodes.Status503ServiceUnavailable);
        }).WithTags("System");

        return app;
    }
}
