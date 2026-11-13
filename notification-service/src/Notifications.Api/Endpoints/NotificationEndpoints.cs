using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Notifications.Application.Notifications;

namespace Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        // Only admins can read the notification log or send a manual notification.
        var group = app.MapGroup("/api/notifications").WithTags("Notifications").RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("/", async (INotificationService service, CancellationToken ct) =>
            Results.Ok(await service.GetAllAsync(ct)));

        group.MapGet("/{id:int}", async (int id, INotificationService service, CancellationToken ct) =>
            (await service.GetByIdAsync(id, ct)).ToOk());

        group.MapPost("/", async (NotificationRequest request, INotificationService service, CancellationToken ct) =>
            (await service.SendAsync(request, ct)).ToCreated(n => $"/api/notifications/{n.Id}"));

        return app;
    }
}
