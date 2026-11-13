using Catalog.Application.Products;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;

namespace Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/products").WithTags("Products");

        // Browsing the catalog is public.
        group.MapGet("/", async (string? category, IProductService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(category, ct))).AllowAnonymous();

        group.MapGet("/{id:int}", async (int id, IProductService service, CancellationToken ct) =>
            (await service.GetByIdAsync(id, ct)).ToOk()).AllowAnonymous();

        // Changing it is admin-only.
        group.MapPost("/", async (ProductRequest request, IProductService service, CancellationToken ct) =>
            (await service.CreateAsync(request, ct)).ToCreated(p => $"/api/products/{p.Id}"))
            .RequireAuthorization(AuthPolicies.Admin);

        group.MapPut("/{id:int}", async (int id, ProductRequest request, IProductService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, request, ct)).ToOk())
            .RequireAuthorization(AuthPolicies.Admin);

        group.MapDelete("/{id:int}", async (int id, IProductService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.Admin);

        return app;
    }
}
