using System.ComponentModel.DataAnnotations;

namespace Ordering.Infrastructure.Configuration;

/// <summary>
/// Base URLs of the services order-service calls, bound from the "Services" configuration section.
/// Override with environment variables, e.g. Services__CatalogUrl=http://catalog:8080.
/// Validated at startup: the service refuses to start if any URL is missing or invalid.
/// </summary>
public sealed class DownstreamServicesOptions
{
    public const string SectionName = "Services";

    [Required, Url]
    public string CatalogUrl { get; init; } = string.Empty;

    [Required, Url]
    public string CustomerUrl { get; init; } = string.Empty;

    [Required, Url]
    public string InventoryUrl { get; init; } = string.Empty;

    [Range(1, 60)]
    public int TimeoutSeconds { get; init; } = 5;
}
