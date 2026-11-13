using System.ComponentModel.DataAnnotations;

namespace DevOpsStore.BuildingBlocks.Messaging;

/// <summary>
/// Settings from the "Messaging" configuration section. The broker address itself is the
/// connection string "ConnectionStrings:RabbitMq", an AMQP URI such as amqp://user:password@host:5672/vhost
/// (it contains a password, so treat it as a secret).
/// </summary>
public sealed class MessagingOptions : IValidatableObject
{
    public const string SectionName = "Messaging";
    public const string ConnectionStringName = "RabbitMq";

    /// <summary>Topic exchange every integration event is published to.</summary>
    [Required]
    public string Exchange { get; init; } = "devops-store.events";

    /// <summary>Exchange that receives messages which failed after all retries.</summary>
    [Required]
    public string DeadLetterExchange { get; init; } = "devops-store.dead-letter";

    /// <summary>How many unacknowledged messages one consumer may hold at a time.</summary>
    [Range(1, 1000)]
    public ushort PrefetchCount { get; init; } = 10;

    /// <summary>Attempts after the first failure before a message is dead-lettered.</summary>
    [Range(0, 20)]
    public int MaxRetries { get; init; } = 3;

    /// <summary>Delay before a failed message is retried (multiplied by the attempt number).</summary>
    [Range(0, 60000)]
    public int RetryDelayMilliseconds { get; init; } = 1000;

    /// <summary>Filled from ConnectionStrings:RabbitMq.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            yield return new ValidationResult(
                $"Connection string '{ConnectionStringName}' is not configured. " +
                $"Set ConnectionStrings:{ConnectionStringName} (environment variable ConnectionStrings__{ConnectionStringName}), " +
                "for example amqp://user:password@localhost:5672/",
                [nameof(ConnectionString)]);
        }
        else if (!Uri.TryCreate(ConnectionString, UriKind.Absolute, out var uri) || uri.Scheme is not ("amqp" or "amqps"))
        {
            yield return new ValidationResult(
                $"Connection string '{ConnectionStringName}' must be an amqp:// or amqps:// URI.",
                [nameof(ConnectionString)]);
        }
    }
}
