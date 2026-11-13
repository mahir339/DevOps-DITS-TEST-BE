using System.Text;
using System.Text.Json;
using DevOpsStore.BuildingBlocks.Web.Correlation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace DevOpsStore.BuildingBlocks.Messaging;

/// <summary>Metadata of the message being handled.</summary>
public sealed record MessageContext(Guid MessageId, string RoutingKey, string? CorrelationId, int Attempt);

/// <summary>
/// Handles one integration event type. Messages are delivered AT LEAST ONCE, so handlers must be
/// idempotent (use <see cref="MessageContext.MessageId"/> to detect duplicates).
/// Throwing makes the message retry, and after the last retry it goes to the dead-letter queue.
/// </summary>
public interface IIntegrationEventHandler<in TEvent>
{
    Task HandleAsync(TEvent integrationEvent, MessageContext context, CancellationToken cancellationToken);
}

/// <summary>
/// Consumes one durable queue bound to the events exchange. Reconnects every 5 seconds while the
/// broker is unreachable. Failed messages are re-queued with an "x-attempt" header and a growing delay,
/// then dead-lettered to "{queue}.dead-letter".
/// </summary>
public sealed class RabbitMqConsumerService<TEvent, THandler>(
    string queueName,
    string routingKey,
    RabbitMqConnectionProvider connections,
    IServiceScopeFactory scopeFactory,
    IOptions<MessagingOptions> options,
    ILogger<RabbitMqConsumerService<TEvent, THandler>> logger) : BackgroundService
    where THandler : class, IIntegrationEventHandler<TEvent>
{
    public const string AttemptHeader = "x-attempt";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await using var channel = await StartConsumingAsync(closed, stoppingToken);
                logger.LogInformation("Consuming {RoutingKey} from queue {Queue}", routingKey, queueName);
                await closed.Task.WaitAsync(stoppingToken);
                logger.LogWarning("Consumer channel for queue {Queue} closed; reconnecting", queueName);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning("Cannot consume queue {Queue} ({Error}); retrying in 5 s", queueName, ex.Message);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<IChannel> StartConsumingAsync(TaskCompletionSource closed, CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var connection = await connections.GetConnectionAsync(stoppingToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
        channel.ChannelShutdownAsync += (_, _) =>
        {
            closed.TrySetResult();
            return Task.CompletedTask;
        };

        var deadLetterQueue = $"{queueName}.dead-letter";
        await channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: stoppingToken);
        await channel.QueueBindAsync(deadLetterQueue, settings.DeadLetterExchange, queueName, cancellationToken: stoppingToken);

        await channel.QueueDeclareAsync(queueName, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = settings.DeadLetterExchange,
                ["x-dead-letter-routing-key"] = queueName,
            },
            cancellationToken: stoppingToken);
        await channel.QueueBindAsync(queueName, settings.Exchange, routingKey, cancellationToken: stoppingToken);
        await channel.BasicQosAsync(0, settings.PrefetchCount, global: false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, delivery) => HandleDeliveryAsync(channel, delivery, stoppingToken);
        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer, stoppingToken);
        return channel;
    }

    private async Task HandleDeliveryAsync(IChannel channel, BasicDeliverEventArgs delivery, CancellationToken stoppingToken)
    {
        var properties = delivery.BasicProperties;
        var body = delivery.Body.ToArray(); // the buffer is only valid during this callback
        var attempt = ReadAttempt(properties.Headers);
        var correlationId = CorrelationId.IsValid(properties.CorrelationId) ? properties.CorrelationId : null;
        var messageId = Guid.TryParse(properties.MessageId, out var id) ? id : Guid.Empty;

        using var scope = logger.BeginScope("CorrelationId:{CorrelationId}", correlationId ?? "none");

        TEvent? integrationEvent;
        try
        {
            integrationEvent = JsonSerializer.Deserialize<TEvent>(Encoding.UTF8.GetString(body), JsonOptions);
            if (integrationEvent is null || messageId == Guid.Empty) throw new JsonException("Empty payload or missing message id.");
        }
        catch (JsonException ex)
        {
            // A message that can never be read is "poison": retrying will not help.
            logger.LogError("Message {MessageId} on {Queue} is not a valid {Event}: {Error}. Dead-lettering it.",
                properties.MessageId, queueName, typeof(TEvent).Name, ex.Message);
            await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            return;
        }

        try
        {
            await using var handlerScope = scopeFactory.CreateAsyncScope();
            var handler = handlerScope.ServiceProvider.GetRequiredService<THandler>();
            await handler.HandleAsync(integrationEvent, new MessageContext(messageId, delivery.RoutingKey, correlationId, attempt), stoppingToken);
            await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
        }
        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
        {
            var settings = options.Value;
            if (attempt < settings.MaxRetries)
            {
                logger.LogWarning(ex, "Handling message {MessageId} failed (attempt {Attempt} of {Max}); retrying",
                    messageId, attempt + 1, settings.MaxRetries + 1);
                await Task.Delay(settings.RetryDelayMilliseconds * (attempt + 1), stoppingToken);
                await RequeueAsync(channel, delivery, body, attempt + 1, stoppingToken);
                await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
            }
            else
            {
                logger.LogError(ex, "Message {MessageId} failed {Count} times; moving it to {Queue}.dead-letter",
                    messageId, attempt + 1, queueName);
                await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
            }
        }
    }

    /// <summary>Puts a copy at the back of the same queue (default exchange routes by queue name).</summary>
    private Task RequeueAsync(IChannel channel, BasicDeliverEventArgs delivery, byte[] body, int attempt, CancellationToken cancellationToken)
    {
        var original = delivery.BasicProperties;
        var headers = original.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(original.Headers);
        headers[AttemptHeader] = attempt;

        var properties = new BasicProperties
        {
            MessageId = original.MessageId,
            CorrelationId = original.CorrelationId,
            Type = original.Type,
            ContentType = original.ContentType,
            ContentEncoding = original.ContentEncoding,
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = original.Timestamp,
            Headers = headers,
        };

        return channel.BasicPublishAsync(string.Empty, queueName, mandatory: false, properties, body, cancellationToken).AsTask();
    }

    internal static int ReadAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(AttemptHeader, out var value)) return 0;
        return value switch
        {
            int i => i,
            long l => (int)l,
            byte[] bytes when int.TryParse(Encoding.UTF8.GetString(bytes), out var parsed) => parsed,
            _ => 0,
        };
    }
}
