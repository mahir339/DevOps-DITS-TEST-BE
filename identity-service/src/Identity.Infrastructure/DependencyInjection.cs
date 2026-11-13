using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Security;
using Identity.Infrastructure.Seeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Identity.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "IdentityDb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // PostgreSQL: ConnectionStrings:IdentityDb (DevOpsStore.BuildingBlocks.Persistence package).
        builder.AddPostgresDbContext<IdentityDbContext>(DatabaseConnectionName);
        builder.Services.AddScoped<IUserRepository, EfUserRepository>();
        builder.Services.AddSingleton<IPasswordHasher, AspNetPasswordHasher>();
        builder.Services.AddSingleton<JwtTokenIssuer>();
        builder.Services.AddSingleton<ITokenService, JwtTokenService>();

        builder.Services.AddOptions<IdentitySeedOptions>().Bind(builder.Configuration.GetSection(IdentitySeedOptions.SectionName));
        builder.Services.AddHostedService<UserSeeder>();
        return builder;
    }
}
