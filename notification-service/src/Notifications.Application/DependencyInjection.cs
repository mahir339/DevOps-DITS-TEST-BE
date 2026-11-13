using Microsoft.Extensions.DependencyInjection;
using Notifications.Application.Notifications;

namespace Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<INotificationService, NotificationService>();
        return services;
    }
}
