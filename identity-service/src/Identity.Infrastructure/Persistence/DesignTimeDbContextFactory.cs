using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Identity.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools. Adding a migration or generating a script/bundle does not
/// connect to a database; "dotnet ef database update" uses --connection or IDENTITY_DB_CONNECTION.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("IDENTITY_DB_CONNECTION")
                               ?? "Host=localhost;Database=identity_db;Username=design_time_only";
        return new IdentityDbContext(new DbContextOptionsBuilder<IdentityDbContext>().UseNpgsql(connectionString).Options);
    }
}
