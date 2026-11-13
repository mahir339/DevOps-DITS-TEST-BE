using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using Inventory.Application.Stock;
using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Inventory.Infrastructure.Messaging;

/// <summary>
/// Puts the stock of a cancelled order back. The message id is stored in the same transaction as
/// the stock changes, so a redelivered message (at-least-once delivery) is recognised and skipped.
/// </summary>
public sealed class OrderCancelledHandler(
    InventoryDbContext db,
    IStockService stock,
    TimeProvider timeProvider,
    ILogger<OrderCancelledHandler> logger) : IIntegrationEventHandler<OrderCancelledIntegrationEvent>
{
    public const string QueueName = "inventory.order-cancelled";

    public async Task HandleAsync(OrderCancelledIntegrationEvent integrationEvent, MessageContext context, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async ct =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            if (await db.ProcessedMessages.AnyAsync(m => m.MessageId == context.MessageId, ct))
            {
                logger.LogInformation("Message {MessageId} was already processed; skipping", context.MessageId);
                return;
            }

            foreach (var line in integrationEvent.Lines)
            {
                var result = await stock.ReleaseAsync(line.ProductId, new QuantityRequest(line.Quantity), ct);
                if (!result.IsSuccess)
                    logger.LogWarning("Order {OrderId}: could not return {Quantity} unit(s) of product {ProductId}: {Error}",
                        integrationEvent.OrderId, line.Quantity, line.ProductId, result.Error!.Detail);
            }

            db.ProcessedMessages.Add(new ProcessedMessage
            {
                MessageId = context.MessageId,
                RoutingKey = context.RoutingKey,
                ProcessedAt = timeProvider.GetUtcNow(),
            });
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            logger.LogInformation("Order {OrderId} cancelled: stock returned for {Lines} line(s)", integrationEvent.OrderId, integrationEvent.Lines.Count);
        }, cancellationToken);
    }
}
