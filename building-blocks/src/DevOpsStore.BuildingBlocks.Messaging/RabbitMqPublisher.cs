using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DevOpsStore.BuildingBlocks.Messaging;

/// <summary>A message ready to be sent: the JSON body plus the metadata that travels in AMQP properties.</summary>
public sealed record OutgoingMessage(Guid MessageId, string RoutingKey, string Payload, string? CorrelationId, DateTimeOffset OccurredAt);

public interface IMessagePublisher
{
    /// <summary>Publishes and waits for the broker to confirm. Throws if the broker rejects or cannot route the message.</summary>
    Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken);
}

/// <summary>Publishes to the events exchange with publisher confirms and mandatory routing.</summary>
public sealed class RabbitMqPublisher(RabbitMqConnectionProvider connections, IOptions<MessagingOptions> options)
    : IMessagePublisher, IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IChannel? _channel;

    public async Task PublishAsync(OutgoingMessage message, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var channel = await GetChannelAsync(cancellationToken);
            var properties = new BasicProperties
            {
                MessageId = message.MessageId.ToString(),
                CorrelationId = message.CorrelationId,
                Type = message.RoutingKey,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(message.OccurredAt.ToUnixTimeSeconds()),
            };

            // With confirmation tracking on, this completes only after the broker acks; a nack or an
            // unroutable message (no queue bound yet) throws, and the outbox keeps the row for a later retry.
            await channel.BasicPublishAsync(
                options.Value.Exchange,
                message.RoutingKey,
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(message.Payload),
                cancellationToken: cancellationToken);
        }
        catch
        {
            await ResetChannelAsync();
            throw;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IChannel> GetChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is { IsOpen: true }) return _channel;
        await ResetChannelAsync();

        var connection = await connections.GetConnectionAsync(cancellationToken);
        _channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);
        return _channel;
    }

    private async Task ResetChannelAsync()
    {
        if (_channel is null) return;
        try
        {
            await _channel.DisposeAsync();
        }
        catch
        {
            // The channel is already broken; nothing else to clean up.
        }
        _channel = null;
    }

    public async ValueTask DisposeAsync()
    {
        await ResetChannelAsync();
        _gate.Dispose();
    }
}
