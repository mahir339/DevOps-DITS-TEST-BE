using DevOpsStore.BuildingBlocks.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Notifications.Application.Abstractions;
using Notifications.Infrastructure.Persistence;
using Notifications.Infrastructure.Senders;

namespace Notifications.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "NotificationsDb";

    /// <summary>Shared by Notifications.Api and Notifications.Worker: both use the same database.</summary>
    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // PostgreSQL: ConnectionStrings:NotificationsDb (DevOpsStore.BuildingBlocks.Persistence package).
        builder.AddPostgresDbContext<NotificationsDbContext>(DatabaseConnectionName);
        builder.Services.AddScoped<INotificationRepository, EfNotificationRepository>();
        builder.Services.AddSingleton<INotificationSender, LoggingNotificationSender>();
        return builder;
    }
}
