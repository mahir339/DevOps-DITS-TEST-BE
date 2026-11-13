using Microsoft.Extensions.DependencyInjection;
using Ordering.Application.Orders;

namespace Ordering.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IOrderService, OrderService>();
        return services;
    }
}
