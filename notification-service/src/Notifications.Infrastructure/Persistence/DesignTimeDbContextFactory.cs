using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Notifications.Infrastructure.Persistence;

/// <summary>
/// Used only by the "dotnet ef" tools. Adding a migration or generating a script/bundle does not
/// connect to a database; "dotnet ef database update" uses --connection or NOTIFICATIONS_DB_CONNECTION.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<NotificationsDbContext>
{
    public NotificationsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("NOTIFICATIONS_DB_CONNECTION")
                               ?? "Host=localhost;Database=notifications_db;Username=design_time_only";
        return new NotificationsDbContext(new DbContextOptionsBuilder<NotificationsDbContext>().UseNpgsql(connectionString).Options);
    }
}
