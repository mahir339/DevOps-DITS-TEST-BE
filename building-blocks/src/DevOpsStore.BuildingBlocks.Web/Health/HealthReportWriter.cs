using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace DevOpsStore.BuildingBlocks.Web.Health;

/// <summary>JSON body of the health endpoints.</summary>
public sealed record HealthReportResponse(string Status, double TotalDurationMs, IReadOnlyList<HealthCheckResponse> Checks);

public sealed record HealthCheckResponse(string Name, string Status, double DurationMs, string? Description);

/// <summary>Writes a <see cref="HealthReport"/> as JSON instead of the default plain-text "Healthy".</summary>
public static class HealthReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static HealthReportResponse ToResponse(HealthReport report) => new(
        report.Status.ToString(),
        Math.Round(report.TotalDuration.TotalMilliseconds, 1),
        report.Entries
            .Select(e => new HealthCheckResponse(
                e.Key,
                e.Value.Status.ToString(),
                Math.Round(e.Value.Duration.TotalMilliseconds, 1),
                // Exception messages can contain host names; only the description is exposed.
                e.Value.Description))
            .ToList());

    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers.CacheControl = "no-store";
        return JsonSerializer.SerializeAsync(context.Response.Body, ToResponse(report), JsonOptions, context.RequestAborted);
    }
}
