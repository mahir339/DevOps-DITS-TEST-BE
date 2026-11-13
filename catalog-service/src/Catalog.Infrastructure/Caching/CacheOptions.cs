using System.ComponentModel.DataAnnotations;

namespace Catalog.Infrastructure.Caching;

/// <summary>Settings from the "Cache" section. The Redis address is ConnectionStrings:Redis.</summary>
public sealed class CacheOptions : IValidatableObject
{
    public const string SectionName = "Cache";
    public const string ConnectionStringName = "Redis";

    /// <summary>When false the service never talks to Redis (every read hits PostgreSQL).</summary>
    public bool Enabled { get; init; } = true;

    [Range(1, 86400)]
    public int ProductTtlSeconds { get; init; } = 120;

    /// <summary>Prefix for every key, so several environments can share one Redis safely.</summary>
    [Required]
    public string KeyPrefix { get; init; } = "catalog:";

    /// <summary>Filled from ConnectionStrings:Redis, e.g. "localhost:6379" or "host:6380,password=...,ssl=true".</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Enabled && string.IsNullOrWhiteSpace(ConnectionString))
        {
            yield return new ValidationResult(
                $"Connection string '{ConnectionStringName}' is not configured. Set ConnectionStrings:{ConnectionStringName} " +
                $"(environment variable ConnectionStrings__{ConnectionStringName}) or disable caching with Cache:Enabled=false.",
                [nameof(ConnectionString)]);
        }
    }
}
