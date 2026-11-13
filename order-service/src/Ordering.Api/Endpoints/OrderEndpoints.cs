using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Ordering.Application.Orders;

namespace Ordering.Api.Endpoints;

public static class OrderEndpoints
{
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/orders").WithTags("Orders").RequireAuthorization(AuthPolicies.Staff);

        group.MapGet("/", async (IOrderService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, IOrderService service, CancellationToken ct) =>
            (await service.GetByIdAsync(id, ct)).ToOk());

        group.MapPost("/", async (CreateOrderRequest request, IOrderService service, CancellationToken ct) =>
            (await service.PlaceOrderAsync(request, ct)).ToCreated(o => $"/api/orders/{o.Id}"));

        group.MapPost("/{id:int}/cancel", async (int id, IOrderService service, CancellationToken ct) =>
            (await service.CancelAsync(id, ct)).ToOk())
            .RequireAuthorization(AuthPolicies.Admin);

        return app;
    }
}
