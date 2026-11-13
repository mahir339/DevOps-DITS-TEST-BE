using Customers.Application.Customers;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;

namespace Customers.Api.Endpoints;

public static class CustomerEndpoints
{
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        // Staff (and admins) manage customers; only admins may delete them.
        var group = app.MapGroup("/api/customers").WithTags("Customers").RequireAuthorization(AuthPolicies.Staff);

        group.MapGet("/", async (ICustomerService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, ICustomerService service, CancellationToken ct) =>
            (await service.GetByIdAsync(id, ct)).ToOk());

        group.MapPost("/", async (CustomerRequest request, ICustomerService service, CancellationToken ct) =>
            (await service.CreateAsync(request, ct)).ToCreated(c => $"/api/customers/{c.Id}"));

        group.MapPut("/{id:int}", async (int id, CustomerRequest request, ICustomerService service, CancellationToken ct) =>
            (await service.UpdateAsync(id, request, ct)).ToOk());

        group.MapDelete("/{id:int}", async (int id, ICustomerService service, CancellationToken ct) =>
            (await service.DeleteAsync(id, ct)).ToNoContent())
            .RequireAuthorization(AuthPolicies.Admin);

        return app;
    }
}
