using DevOpsStore.BuildingBlocks.Messaging.Outbox;
using DevOpsStore.BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsStore.BuildingBlocks.Messaging;

public static class MessagingExtensions
{
    /// <summary>
    /// Registers the RabbitMQ connection, the publisher and a "rabbitmq" readiness check.
    /// Configuration is validated when the host starts (not here), so "--migrate" runs without a broker.
    /// </summary>
    public static IHostApplicationBuilder AddRabbitMqMessaging(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<MessagingOptions>()
            .Bind(builder.Configuration.GetSection(MessagingOptions.SectionName))
            .Configure<IConfiguration>((options, config) =>
                options.ConnectionString = config.GetConnectionString(MessagingOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.TryAddSingleton<RabbitMqConnectionProvider>();
        builder.Services.TryAddSingleton<IMessagePublisher, RabbitMqPublisher>();

        builder.Services.AddHealthChecks()
            .AddCheck<RabbitMqHealthCheck>("rabbitmq", tags: [ServiceDefaultsExtensions.ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Consumes <paramref name="routingKey"/> from the durable queue <paramref name="queueName"/>
    /// and hands each message to <typeparamref name="THandler"/> (resolved per message in a new scope).
    /// </summary>
    public static IHostApplicationBuilder AddEventConsumer<TEvent, THandler>(this IHostApplicationBuilder builder, string queueName, string routingKey)
        where THandler : class, IIntegrationEventHandler<TEvent>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);

        builder.Services.AddScoped<THandler>();
        builder.Services.AddSingleton<IHostedService>(sp => new RabbitMqConsumerService<TEvent, THandler>(
            queueName,
            routingKey,
            sp.GetRequiredService<RabbitMqConnectionProvider>(),
            sp.GetRequiredService<IServiceScopeFactory>(),
            sp.GetRequiredService<IOptions<MessagingOptions>>(),
            sp.GetRequiredService<ILogger<RabbitMqConsumerService<TEvent, THandler>>>()));
        return builder;
    }

    /// <summary>
    /// Runs the outbox dispatcher for <typeparamref name="TContext"/> (whose model must call AddOutbox())
    /// and adds an "outbox" readiness check that reports Degraded when messages pile up.
    /// </summary>
    public static IHostApplicationBuilder AddOutboxDispatcher<TContext>(this IHostApplicationBuilder builder)
        where TContext : DbContext
    {
        builder.Services.AddOptions<OutboxOptions>()
            .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddHostedService<OutboxDispatcher<TContext>>();
        builder.Services.AddHealthChecks()
            .AddCheck<OutboxHealthCheck<TContext>>("outbox", HealthStatus.Degraded, [ServiceDefaultsExtensions.ReadyTag]);
        return builder;
    }
}
