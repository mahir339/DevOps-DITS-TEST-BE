using System.Security.Claims;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Identity.Application.Auth;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var auth = app.MapGroup("/api/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest request, IAuthService service, CancellationToken ct) =>
            (await service.LoginAsync(request, ct)).ToOk()).AllowAnonymous();

        auth.MapGet("/me", async (ClaimsPrincipal user, IAuthService service, CancellationToken ct) =>
            int.TryParse(user.FindFirstValue(AppClaims.Subject), out var id)
                ? (await service.GetUserAsync(id, ct)).ToOk()
                : Results.Unauthorized()).RequireAuthorization();

        app.MapGet("/api/users", async (IAuthService service, CancellationToken ct) =>
            Results.Ok(await service.GetUsersAsync(ct))).WithTags("Users").RequireAuthorization(AuthPolicies.Admin);

        return app;
    }
}
