using Inventory.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Inventory.IntegrationTests;

/// <summary>Recreates the schema once per test run in the database named by INVENTORY_TEST_DB.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string EnvironmentVariable = "INVENTORY_TEST_DB";

    public string ConnectionString { get; } = Environment.GetEnvironmentVariable(EnvironmentVariable)
        ?? throw new InvalidOperationException(
            $"{EnvironmentVariable} is not set. Integration tests need a disposable PostgreSQL database, e.g. " +
            $"{EnvironmentVariable}=\"Host=localhost;Port=5432;Database=inventory_test;Username=...;Password=...\". " +
            "Run only unit tests with: dotnet test --filter \"Category!=Integration\"");

    public InventoryDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<InventoryDbContext>().UseNpgsql(ConnectionString).Options);

    public async Task InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
