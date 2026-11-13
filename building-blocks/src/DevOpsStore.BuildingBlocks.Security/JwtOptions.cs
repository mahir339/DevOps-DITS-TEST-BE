using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DevOpsStore.BuildingBlocks.Security;

/// <summary>
/// Settings from the "Jwt" configuration section. Every service that validates tokens and the
/// identity-service that issues them must use the SAME Issuer, Audience and SigningKey.
/// The signing key is a secret: never put it in appsettings.json.
/// </summary>
public sealed class JwtOptions : IValidatableObject
{
    public const string SectionName = "Jwt";

    /// <summary>HS256 needs at least 256 bits of key material.</summary>
    public const int MinimumKeyBytes = 32;

    [Required]
    public string Issuer { get; init; } = "devops-store-identity";

    [Required]
    public string Audience { get; init; } = "devops-store-api";

    /// <summary>Symmetric HS256 key (at least 32 bytes as UTF-8). Supply via a secret store or environment variable Jwt__SigningKey.</summary>
    public string SigningKey { get; init; } = string.Empty;

    /// <summary>Lifetime of tokens issued by identity-service.</summary>
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;

    /// <summary>Allowed difference between server clocks when checking expiry.</summary>
    [Range(0, 300)]
    public int ClockSkewSeconds { get; init; } = 30;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(SigningKey))
        {
            yield return new ValidationResult(
                "Jwt:SigningKey is not set. Provide it as a secret (environment variable Jwt__SigningKey).",
                [nameof(SigningKey)]);
        }
        else if (Encoding.UTF8.GetByteCount(SigningKey) < MinimumKeyBytes)
        {
            yield return new ValidationResult(
                $"Jwt:SigningKey must be at least {MinimumKeyBytes} bytes long.",
                [nameof(SigningKey)]);
        }
    }
}
