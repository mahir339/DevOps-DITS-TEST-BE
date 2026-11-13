using DevOpsStore.BuildingBlocks.Http;
using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Persistence;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Ordering.Application.Abstractions;
using Ordering.Application.Abstractions.ExternalServices;
using Ordering.Infrastructure.Configuration;
using Ordering.Infrastructure.Http;
using Ordering.Infrastructure.Persistence;

namespace Ordering.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "OrderingDb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        var services = builder.Services;
        var configuration = builder.Configuration;

        // PostgreSQL (ConnectionStrings:OrderingDb) + RabbitMQ (ConnectionStrings:RabbitMq) via the outbox.
        builder.AddPostgresDbContext<OrderingDbContext>(DatabaseConnectionName);
        builder.AddRabbitMqMessaging();
        builder.AddOutboxDispatcher<OrderingDbContext>();
        services.AddScoped<IOrderRepository, EfOrderRepository>();

        services.AddOptions<DownstreamServicesOptions>()
            .Bind(configuration.GetSection(DownstreamServicesOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        AddDownstreamHttpClient(services, DownstreamServiceNames.Catalog, o => o.CatalogUrl);
        AddDownstreamHttpClient(services, DownstreamServiceNames.Customer, o => o.CustomerUrl);
        AddDownstreamHttpClient(services, DownstreamServiceNames.Inventory, o => o.InventoryUrl);

        services.AddScoped<ICustomerServiceClient, CustomerServiceClient>();
        services.AddScoped<ICatalogServiceClient, CatalogServiceClient>();
        services.AddScoped<IInventoryServiceClient, InventoryServiceClient>();
        services.AddScoped<IDependencyHealthChecker, DependencyHealthChecker>();

        return builder;
    }

    private static void AddDownstreamHttpClient(
        IServiceCollection services,
        string name,
        Func<DownstreamServicesOptions, string> selectUrl)
    {
        static DownstreamServicesOptions Options(IServiceProvider sp) =>
            sp.GetRequiredService<IOptions<DownstreamServicesOptions>>().Value;

        // ConfigureDownstream, the propagation handlers and AddDownstreamResilience come from the DevOpsStore.BuildingBlocks.Http package.
        services.AddHttpClient(name)
            .ConfigureDownstream(
                sp => selectUrl(Options(sp)),
                sp => TimeSpan.FromSeconds(Options(sp).TimeoutSeconds))
            .AddCorrelationIdPropagation()
            .AddBearerTokenPropagation()
            .AddDownstreamResilience(TimeSpan.FromSeconds(5));
    }
}
