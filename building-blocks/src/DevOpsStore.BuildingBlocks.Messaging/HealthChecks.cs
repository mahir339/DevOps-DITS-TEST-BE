using DevOpsStore.BuildingBlocks.Messaging.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace DevOpsStore.BuildingBlocks.Messaging;

/// <summary>Healthy when a RabbitMQ connection is open or can be opened within 3 seconds.</summary>
public sealed class RabbitMqHealthCheck(RabbitMqConnectionProvider connections) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (connections.IsConnected) return HealthCheckResult.Healthy("Connected.");

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        try
        {
            await connections.GetConnectionAsync(timeout.Token);
            return HealthCheckResult.Healthy("Connected.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ is unreachable.", ex);
        }
    }
}

/// <summary>Degraded when events are piling up in the outbox (usually: the broker is down).</summary>
public sealed class OutboxHealthCheck<TContext>(TContext dbContext, IOptions<OutboxOptions> options, TimeProvider timeProvider) : IHealthCheck
    where TContext : DbContext
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        var staleBefore = timeProvider.GetUtcNow().AddSeconds(-settings.StaleAfterSeconds);
        var pending = dbContext.Set<OutboxMessage>().Where(m => m.ProcessedAt == null);

        var stale = await pending.CountAsync(m => m.OccurredAt < staleBefore, cancellationToken);
        var stuck = await pending.CountAsync(m => m.Attempts >= settings.MaxAttempts, cancellationToken);

        if (stuck > 0)
            return HealthCheckResult.Degraded($"{stuck} outbox message(s) gave up after {settings.MaxAttempts} attempts.");
        if (stale > 0)
            return HealthCheckResult.Degraded($"{stale} outbox message(s) waiting longer than {settings.StaleAfterSeconds} s.");
        return HealthCheckResult.Healthy("Outbox is draining.");
    }
}
