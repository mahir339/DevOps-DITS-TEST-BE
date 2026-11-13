using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Inventory.Application.Stock;

namespace Inventory.Api.Endpoints;

public static class InventoryEndpoints
{
    public static IEndpointRouteBuilder MapInventoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization(AuthPolicies.Staff);

        group.MapGet("/", async (IStockService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{productId:int}", async (int productId, IStockService service, CancellationToken ct) =>
            (await service.GetAsync(productId, ct)).ToOk());

        // Sets the stock level (creates the record if it does not exist yet). Admins only.
        group.MapPut("/{productId:int}", async (int productId, QuantityRequest request, IStockService service, CancellationToken ct) =>
            (await service.SetQuantityAsync(productId, request, ct)).ToOk())
            .RequireAuthorization(AuthPolicies.Admin);

        // Internal: called by order-service (with the user's token) when an order is placed.
        // The gateway does not expose reserve/release to browsers.
        group.MapPost("/{productId:int}/reserve", async (int productId, QuantityRequest request, IStockService service, CancellationToken ct) =>
            (await service.ReserveAsync(productId, request, ct)).ToOk());

        // Internal: called by order-service to undo a reservation when an order fails part-way.
        group.MapPost("/{productId:int}/release", async (int productId, QuantityRequest request, IStockService service, CancellationToken ct) =>
            (await service.ReleaseAsync(productId, request, ct)).ToOk());

        return app;
    }
}
