using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Customers.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools. Adding a migration or generating a script/bundle does not
/// connect to a database; "dotnet ef database update" uses --connection or CUSTOMERS_DB_CONNECTION.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<CustomersDbContext>
{
    public CustomersDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("CUSTOMERS_DB_CONNECTION")
                               ?? "Host=localhost;Database=customers_db;Username=design_time_only";
        return new CustomersDbContext(new DbContextOptionsBuilder<CustomersDbContext>().UseNpgsql(connectionString).Options);
    }
}
