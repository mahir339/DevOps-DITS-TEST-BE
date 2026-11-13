using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using Inventory.Application.Abstractions;
using Inventory.Application.Stock;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;

namespace Inventory.IntegrationTests;

[Collection(PostgresCollection.Name)]
[Trait("Category", "Integration")]
public sealed class StockRepositoryTests(PostgresFixture database)
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public async Task Migration_SeedsSixProducts()
    {
        await using var db = database.CreateContext();

        var all = await new EfStockRepository(db).GetAllAsync();

        Assert.Equal([1, 2, 3, 4, 5, 6], all.Select(s => s.ProductId).Take(6));
    }

    [Fact]
    public async Task ConcurrentReservations_FromSeparateConnections_NeverOversell()
    {
        const int productId = 900;
        await using (var setup = database.CreateContext())
            await new EfStockRepository(setup).SetQuantityAsync(productId, 3, Now);

        // 10 parallel requests, each with its own DbContext/connection, like 10 replicas would.
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            await using var db = database.CreateContext();
            return (await new EfStockRepository(db).TryReserveAsync(productId, 1, Now)).Outcome;
        }));

        Assert.Equal(3, outcomes.Count(o => o == ReserveOutcome.Reserved));
        Assert.Equal(7, outcomes.Count(o => o == ReserveOutcome.InsufficientStock));
        await using var check = database.CreateContext();
        Assert.Equal(0, (await new EfStockRepository(check).GetAsync(productId))!.Quantity);
    }

    [Fact]
    public async Task Reserve_UnknownProduct_ReturnsNotFound()
    {
        await using var db = database.CreateContext();

        var (outcome, _) = await new EfStockRepository(db).TryReserveAsync(123456, 1, Now);

        Assert.Equal(ReserveOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task OrderCancelledHandler_SameMessageTwice_ReturnsStockOnlyOnce()
    {
        const int productId = 901;
        await using (var setup = database.CreateContext())
            await new EfStockRepository(setup).SetQuantityAsync(productId, 10, Now);

        var evt = new OrderCancelledIntegrationEvent(1, 1, "Alex Rivera", "alex@example.com", 20m,
            [new OrderLineEvent(productId, "Test product", 4, 5m)], Now);
        var context = new MessageContext(Guid.NewGuid(), EventNames.OrderCancelled, "corr-test", 0);

        for (var delivery = 0; delivery < 2; delivery++)
        {
            await using var db = database.CreateContext();
            var stock = new StockService(new EfStockRepository(db), TimeProvider.System, NullLogger<StockService>.Instance);
            await new OrderCancelledHandler(db, stock, TimeProvider.System, NullLogger<OrderCancelledHandler>.Instance)
                .HandleAsync(evt, context, CancellationToken.None);
        }

        await using var check = database.CreateContext();
        Assert.Equal(14, (await new EfStockRepository(check).GetAsync(productId))!.Quantity);
    }
}
