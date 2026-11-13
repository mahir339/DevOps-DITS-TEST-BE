using System.Text.RegularExpressions;

namespace Gateway.Api;

/// <summary>Settings from the "Gateway" section.</summary>
public sealed class GatewayOptions
{
    public const string SectionName = "Gateway";
    public const string LoginRateLimitPolicy = "login";

    /// <summary>Sign-in attempts allowed per client IP per minute.</summary>
    public int LoginPermitsPerMinute { get; init; } = 10;

    /// <summary>Regexes of internal paths that must never be reachable from outside (answered with 404).</summary>
    public string[] BlockedPathPatterns { get; init; } = [];
}

/// <summary>Returns 404 for internal service-to-service endpoints so browsers cannot call them.</summary>
public sealed class BlockedPathsMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private readonly Regex[] _blocked = (configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>()?.BlockedPathPatterns ?? [])
        .Select(p => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        .ToArray();

    internal bool IsBlocked(string? path) => path is not null && _blocked.Any(r => r.IsMatch(path));

    public Task InvokeAsync(HttpContext context)
    {
        if (!IsBlocked(context.Request.Path.Value)) return next(context);
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return Task.CompletedTask;
    }
}
