using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Inventory.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools. Adding a migration or generating a script/bundle does not
/// connect to a database; "dotnet ef database update" uses --connection or INVENTORY_DB_CONNECTION.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<InventoryDbContext>
{
    public InventoryDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("INVENTORY_DB_CONNECTION")
                               ?? "Host=localhost;Database=inventory_db;Username=design_time_only";
        return new InventoryDbContext(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(connectionString).Options);
    }
}
