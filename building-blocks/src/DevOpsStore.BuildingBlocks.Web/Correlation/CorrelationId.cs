using Microsoft.AspNetCore.Http;

namespace DevOpsStore.BuildingBlocks.Web.Correlation;

/// <summary>The id that ties together the logs of one request as it travels across services.</summary>
public static class CorrelationId
{
    public const string HeaderName = "X-Correlation-Id";

    internal const string ItemKey = "DevOpsStore.CorrelationId";

    private const int MaxLength = 64;

    /// <summary>The correlation id of the current request, or null outside a request.</summary>
    public static string? Get(HttpContext? context) =>
        context is not null && context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;

    /// <summary>Only short ids made of letters, digits, '-', '_' and '.' are accepted (prevents log injection).</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrEmpty(value)
        && value.Length <= MaxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.');

    public static string NewId() => Guid.NewGuid().ToString("N");
}
