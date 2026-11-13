using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ordering.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools. Adding a migration or generating a script/bundle does not
/// connect to a database; "dotnet ef database update" uses --connection or ORDERING_DB_CONNECTION.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<OrderingDbContext>
{
    public OrderingDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ORDERING_DB_CONNECTION")
                               ?? "Host=localhost;Database=ordering_db;Username=design_time_only";
        return new OrderingDbContext(new DbContextOptionsBuilder<OrderingDbContext>().UseNpgsql(connectionString).Options);
    }
}
