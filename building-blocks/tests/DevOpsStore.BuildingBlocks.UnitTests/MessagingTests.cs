using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using DevOpsStore.BuildingBlocks.Messaging;
using DevOpsStore.BuildingBlocks.Messaging.Contracts;
using DevOpsStore.BuildingBlocks.Messaging.Outbox;
using DevOpsStore.BuildingBlocks.Web.Health;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsStore.BuildingBlocks.UnitTests;

public class MessagingTests
{
    private sealed class TestEvent;
    private sealed class TestHandler : IIntegrationEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent integrationEvent, MessageContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private static int ReadAttempt(IDictionary<string, object?>? headers) =>
        RabbitMqConsumerService<TestEvent, TestHandler>.ReadAttempt(headers);

    [Fact]
    public void ReadAttempt_HandlesMissingAndTypedHeaders()
    {
        Assert.Equal(0, ReadAttempt(null));
        Assert.Equal(0, ReadAttempt(new Dictionary<string, object?>()));
        Assert.Equal(2, ReadAttempt(new Dictionary<string, object?> { ["x-attempt"] = 2 }));
        Assert.Equal(3, ReadAttempt(new Dictionary<string, object?> { ["x-attempt"] = 3L }));
        Assert.Equal(4, ReadAttempt(new Dictionary<string, object?> { ["x-attempt"] = Encoding.UTF8.GetBytes("4") }));
    }

    [Theory]
    [InlineData("", "is not configured")]
    [InlineData("http://localhost:5672", "amqp:// or amqps://")]
    [InlineData("not a uri", "amqp:// or amqps://")]
    public void MessagingOptions_RejectsBadConnectionStrings(string connectionString, string expected)
    {
        var options = new MessagingOptions { ConnectionString = connectionString };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);

        Assert.Contains(results, r => r.ErrorMessage!.Contains(expected));
    }

    [Fact]
    public void MessagingOptions_AcceptsAmqpUri()
    {
        var options = new MessagingOptions { ConnectionString = "amqp://guest:guest@localhost:5672/" };
        var results = new List<ValidationResult>();

        Assert.True(Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true));
    }

    private sealed class OutboxContext(DbContextOptions<OutboxContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutbox();
    }

    [Fact]
    public async Task AddOutboxMessage_StoresCamelCaseJsonWithMetadata()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<OutboxContext>().UseSqlite(connection).Options;
        await using var context = new OutboxContext(options);
        await context.Database.EnsureCreatedAsync();

        var occurred = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var evt = new OrderPlacedIntegrationEvent(7, 1, "Alex Rivera", "alex@example.com", 10m,
            [new OrderLineEvent(3, "Monitor", 1, 10m)], occurred);
        context.AddOutboxMessage(EventNames.OrderPlaced, evt, "corr-1", occurred);
        await context.SaveChangesAsync();

        var stored = await context.Set<OutboxMessage>().SingleAsync();
        Assert.Equal(EventNames.OrderPlaced, stored.RoutingKey);
        Assert.Equal("corr-1", stored.CorrelationId);
        Assert.Null(stored.ProcessedAt);

        using var json = JsonDocument.Parse(stored.Payload);
        Assert.Equal(7, json.RootElement.GetProperty("orderId").GetInt32());
        Assert.Equal("Monitor", json.RootElement.GetProperty("lines")[0].GetProperty("productName").GetString());
    }

    [Fact]
    public void HealthReportWriter_ExposesDescriptionButNotExceptionText()
    {
        var report = new HealthReport(new Dictionary<string, HealthReportEntry>
        {
            ["postgres"] = new(HealthStatus.Unhealthy, "Database is unreachable.", TimeSpan.FromMilliseconds(12.34),
                new InvalidOperationException("host=secret-db.internal password=x"), null),
        }, TimeSpan.FromMilliseconds(15));

        var response = HealthReportWriter.ToResponse(report);

        Assert.Equal("Unhealthy", response.Status);
        var check = Assert.Single(response.Checks);
        Assert.Equal("Database is unreachable.", check.Description);
        Assert.DoesNotContain("secret", JsonSerializer.Serialize(response));
    }
}
