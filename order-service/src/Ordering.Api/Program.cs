using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Ordering.Api.Endpoints;
using Ordering.Application;
using Ordering.Infrastructure;
using Ordering.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging, health checks, CORS, problem details and correlation ids (DevOpsStore.BuildingBlocks.Web package).
builder.AddServiceDefaults("order-service");
// Validates JWTs issued by identity-service; the token is forwarded to customer/catalog/inventory.
builder.AddJwtAuthentication();

builder.Services.AddApplication();
// PostgreSQL, downstream HTTP clients, RabbitMQ and the outbox dispatcher.
builder.AddInfrastructure();

var app = builder.Build();

// "--migrate" applies EF Core migrations and exits; otherwise checks the schema is up to date.
if (await app.PrepareDatabaseAsync<OrderingDbContext>(args)) return;

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapServiceDefaults();
app.MapDependencyEndpoints();
app.MapOrderEndpoints();

app.Run();
