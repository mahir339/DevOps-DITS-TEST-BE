using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using DevOpsStore.BuildingBlocks.Persistence;
using Inventory.Application.Abstractions;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Inventory.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "InventoryDb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // PostgreSQL: ConnectionStrings:InventoryDb (DevOpsStore.BuildingBlocks.Persistence package).
        builder.AddPostgresDbContext<InventoryDbContext>(DatabaseConnectionName);
        builder.Services.AddScoped<IStockRepository, EfStockRepository>();

        // RabbitMQ: ConnectionStrings:RabbitMq. Cancelled orders put their stock back.
        builder.AddRabbitMqMessaging();
        builder.AddEventConsumer<OrderCancelledIntegrationEvent, OrderCancelledHandler>(
            OrderCancelledHandler.QueueName, EventNames.OrderCancelled);

        return builder;
    }
}
