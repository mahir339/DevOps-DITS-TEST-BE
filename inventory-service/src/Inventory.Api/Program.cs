using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Inventory.Api.Endpoints;
using Inventory.Application;
using Inventory.Infrastructure;
using Inventory.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging, health checks, CORS, problem details and correlation ids (DevOpsStore.BuildingBlocks.Web package).
builder.AddServiceDefaults("inventory-service");
// Validates JWTs issued by identity-service (DevOpsStore.BuildingBlocks.Security package).
builder.AddJwtAuthentication();

builder.Services.AddApplication();
// PostgreSQL + the RabbitMQ consumer for cancelled orders.
builder.AddInfrastructure();

var app = builder.Build();

// "--migrate" applies EF Core migrations and exits; otherwise checks the schema is up to date.
if (await app.PrepareDatabaseAsync<InventoryDbContext>(args)) return;

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapServiceDefaults();
app.MapInventoryEndpoints();

app.Run();
