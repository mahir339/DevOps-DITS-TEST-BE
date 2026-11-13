using Catalog.Api.Endpoints;
using Catalog.Application;
using Catalog.Infrastructure;
using Catalog.Infrastructure.Persistence;
using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;

var builder = WebApplication.CreateBuilder(args);

// Logging, health checks, CORS, problem details and correlation ids (DevOpsStore.BuildingBlocks.Web package).
builder.AddServiceDefaults("catalog-service");
// Validates JWTs issued by identity-service (DevOpsStore.BuildingBlocks.Security package).
builder.AddJwtAuthentication();

builder.Services.AddApplication();
builder.AddInfrastructure();

var app = builder.Build();

// "--migrate" applies EF Core migrations and exits; otherwise checks the schema is up to date.
if (await app.PrepareDatabaseAsync<CatalogDbContext>(args)) return;

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapServiceDefaults();
app.MapProductEndpoints();

app.Run();
