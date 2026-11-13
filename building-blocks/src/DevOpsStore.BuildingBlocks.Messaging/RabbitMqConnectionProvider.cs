using DevOpsStore.BuildingBlocks.Web;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace DevOpsStore.BuildingBlocks.Messaging;

/// <summary>
/// One long-lived RabbitMQ connection per process (connections are expensive, channels are cheap).
/// Connects lazily and declares the exchanges. Automatic recovery is OFF on purpose: when the broker goes
/// away, publishers and consumers notice, and the next call here opens a fresh connection.
/// </summary>
public sealed class RabbitMqConnectionProvider(
    IOptions<MessagingOptions> options,
    ServiceIdentity identity,
    ILogger<RabbitMqConnectionProvider> logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IConnection? _connection;

    public bool IsConnected => _connection is { IsOpen: true };

    public async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true } open) return open;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true } existing) return existing;
            if (_connection is not null) await _connection.DisposeAsync();

            var settings = options.Value;
            var factory = new ConnectionFactory
            {
                Uri = new Uri(settings.ConnectionString),
                ClientProvidedName = $"{identity.Name}@{Environment.MachineName}",
                AutomaticRecoveryEnabled = false,
                TopologyRecoveryEnabled = false,
                RequestedHeartbeat = TimeSpan.FromSeconds(30),
            };

            var connection = await factory.CreateConnectionAsync(cancellationToken);
            await using (var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken))
            {
                await channel.ExchangeDeclareAsync(settings.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
                await channel.ExchangeDeclareAsync(settings.DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
            }

            logger.LogInformation("Connected to RabbitMQ at {Host}:{Port}", factory.HostName, factory.Port);
            _connection = connection;
            return connection;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
        _gate.Dispose();
    }
}
