using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Web;
using Notifications.Application;
using Notifications.Infrastructure;
using Notifications.Infrastructure.Persistence;
using Notifications.Worker.Handlers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("notification-worker");
builder.Services.AddApplication();
builder.AddInfrastructure();

// RabbitMQ: ConnectionStrings:RabbitMq. One durable queue per event type.
builder.AddRabbitMqMessaging();
builder.AddEventConsumer<OrderPlacedIntegrationEvent, OrderPlacedHandler>("notifications.order-placed", EventNames.OrderPlaced);
builder.AddEventConsumer<OrderCancelledIntegrationEvent, OrderCancelledHandler>("notifications.order-cancelled", EventNames.OrderCancelled);

var app = builder.Build();

// The worker never migrates (the notification API owns the schema); it only checks the schema is current.
if (await app.PrepareDatabaseAsync<NotificationsDbContext>([])) return;

app.UseServiceDefaults();
app.MapServiceDefaults();

app.Run();
