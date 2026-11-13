using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsStore.BuildingBlocks.Messaging.Outbox;

/// <summary>Settings from the "Outbox" configuration section.</summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    [Range(100, 60000)]
    public int PollIntervalMilliseconds { get; init; } = 1000;

    [Range(1, 500)]
    public int BatchSize { get; init; } = 20;

    /// <summary>After this many failed publishes a message is left alone and reported as stuck.</summary>
    [Range(1, 1000)]
    public int MaxAttempts { get; init; } = 20;

    /// <summary>Published messages older than this are deleted.</summary>
    [Range(1, 24 * 90)]
    public int RetentionHours { get; init; } = 72;

    /// <summary>The readiness check reports Degraded when an unsent message is older than this.</summary>
    [Range(10, 86400)]
    public int StaleAfterSeconds { get; init; } = 300;
}

/// <summary>
/// Publishes outbox rows to RabbitMQ. Safe to run in several replicas at once: each batch is
/// claimed with SELECT ... FOR UPDATE SKIP LOCKED, so two replicas never send the same row
/// concurrently. Delivery is at-least-once (a crash between publish and commit re-sends).
/// </summary>
public sealed class OutboxDispatcher<TContext>(
    IServiceScopeFactory scopeFactory,
    IMessagePublisher publisher,
    IOptions<OutboxOptions> options,
    TimeProvider timeProvider,
    ILogger<OutboxDispatcher<TContext>> logger) : BackgroundService
    where TContext : DbContext
{
    private DateTimeOffset _nextCleanup = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        while (!stoppingToken.IsCancellationRequested)
        {
            var sent = 0;
            try
            {
                sent = await DispatchBatchAsync(settings, stoppingToken);
                await CleanupAsync(settings, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Outbox dispatch failed: {Error}", ex.Message);
            }

            // A full batch means more may be waiting: go again immediately.
            if (sent < settings.BatchSize)
            {
                try
                {
                    await Task.Delay(settings.PollIntervalMilliseconds, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private async Task<int> DispatchBatchAsync(OutboxOptions settings, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(ct);

            var batch = await context.Set<OutboxMessage>()
                .FromSql($"""
                    SELECT * FROM outbox_messages
                    WHERE processed_at IS NULL AND attempts < {settings.MaxAttempts}
                    ORDER BY occurred_at
                    LIMIT {settings.BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(ct);

            var published = 0;
            foreach (var message in batch)
            {
                try
                {
                    await publisher.PublishAsync(
                        new OutgoingMessage(message.Id, message.RoutingKey, message.Payload, message.CorrelationId, message.OccurredAt), ct);
                    message.ProcessedAt = timeProvider.GetUtcNow();
                    published++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.Attempts++;
                    message.LastError = Truncate(ex.Message, 1000);
                    if (message.Attempts >= settings.MaxAttempts)
                        logger.LogError("Outbox message {MessageId} ({RoutingKey}) gave up after {Attempts} attempts: {Error}",
                            message.Id, message.RoutingKey, message.Attempts, ex.Message);
                    else
                        logger.LogWarning("Could not publish outbox message {MessageId} ({RoutingKey}): {Error}",
                            message.Id, message.RoutingKey, ex.Message);
                    break; // the broker is probably down: stop this batch and try again later
                }
            }

            await context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            if (published > 0)
                logger.LogInformation("Published {Count} outbox message(s)", published);
            return batch.Count;
        }, cancellationToken);
    }

    private async Task CleanupAsync(OutboxOptions settings, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (now < _nextCleanup) return;
        _nextCleanup = now.AddMinutes(10);

        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var cutoff = now.AddHours(-settings.RetentionHours);
        var deleted = await context.Set<OutboxMessage>()
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        if (deleted > 0)
            logger.LogInformation("Deleted {Count} published outbox message(s) older than {Hours} h", deleted, settings.RetentionHours);
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}
