using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Identity.Api.Endpoints;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Logging, health checks, CORS, problem details and correlation ids (DevOpsStore.BuildingBlocks.Web package).
builder.AddServiceDefaults("identity-service");
// The same "Jwt" settings are used to SIGN tokens here and to VALIDATE them in every other service.
builder.AddJwtAuthentication();

builder.Services.AddApplication();
// PostgreSQL, password hashing, token signing and the seed users from "Identity:SeedUsers".
builder.AddInfrastructure();

var app = builder.Build();

// "--migrate" applies EF Core migrations and exits; otherwise checks the schema is up to date.
if (await app.PrepareDatabaseAsync<IdentityDbContext>(args)) return;

app.UseServiceDefaults();
app.UseAuthentication();
app.UseAuthorization();

app.MapServiceDefaults();
app.MapAuthEndpoints();

app.Run();
