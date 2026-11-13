using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace DevOpsStore.BuildingBlocks.Messaging.Outbox;

/// <summary>
/// An event waiting to be published. It is saved in the SAME transaction as the business change,
/// so an order can never be stored without its event (or the other way round).
/// <see cref="OutboxDispatcher{TContext}"/> sends it to RabbitMQ afterwards.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; init; }

    public required string RoutingKey { get; init; }

    public required string Payload { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}

public static class OutboxExtensions
{
    public const string TableName = "outbox_messages";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Maps the outbox table. Call from OnModelCreating, then add a migration.</summary>
    public static ModelBuilder AddOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(entity =>
        {
            entity.ToTable(TableName);
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").ValueGeneratedNever();
            entity.Property(m => m.RoutingKey).HasColumnName("routing_key").HasMaxLength(200);
            entity.Property(m => m.Payload).HasColumnName("payload").HasColumnType("jsonb");
            entity.Property(m => m.CorrelationId).HasColumnName("correlation_id").HasMaxLength(64);
            entity.Property(m => m.OccurredAt).HasColumnName("occurred_at");
            entity.Property(m => m.ProcessedAt).HasColumnName("processed_at");
            entity.Property(m => m.Attempts).HasColumnName("attempts");
            entity.Property(m => m.LastError).HasColumnName("last_error").HasMaxLength(1000);
            entity.HasIndex(m => m.OccurredAt)
                .HasDatabaseName("ix_outbox_messages_unprocessed")
                .HasFilter("processed_at IS NULL");
        });
        return modelBuilder;
    }

    /// <summary>Queues an event in the outbox. It is written when you call SaveChanges.</summary>
    public static OutboxMessage AddOutboxMessage<TEvent>(
        this DbContext context,
        string routingKey,
        TEvent integrationEvent,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        var message = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            RoutingKey = routingKey,
            Payload = JsonSerializer.Serialize(integrationEvent, JsonOptions),
            CorrelationId = correlationId,
            OccurredAt = occurredAt,
        };
        context.Set<OutboxMessage>().Add(message);
        return message;
    }
}
