using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Notifications.Api.Endpoints;
using Notifications.Application;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging, health checks, CORS, problem details and correlation ids (DevOpsStore.BuildingBlocks.Web package).
builder.AddServiceDefaults("notification-service");
// Validates JWTs issued by identity-service (DevOpsStore.BuildingBlocks.Security package).
builder.AddJwtAuthentication();

builder.Services.AddApplication();
builder.AddInfrastructure();

var app = builder.Build();

// This API owns the schema: "--migrate" applies EF Core migrations and exits.
// Notifications.Worker uses the same database and refuses to start until they are applied.
if (await app.PrepareDatabaseAsync<NotificationsDbContext>(args)) return;

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapServiceDefaults();
app.MapNotificationEndpoints();

app.Run();
