using DevOpsStore.BuildingBlocks.Web.Correlation;
using DevOpsStore.BuildingBlocks.Web.Health;
using DevOpsStore.BuildingBlocks.Web.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace DevOpsStore.BuildingBlocks.Web;

/// <summary>The standard setup every DevOps Test Store API uses. Call all three from Program.cs.</summary>
public static class ServiceDefaultsExtensions
{
    /// <summary>Health checks registered with this tag are part of GET /health/ready.</summary>
    public const string ReadyTag = "ready";

    /// <summary>
    /// Registers logging, problem details, health checks, CORS and the service identity.
    /// </summary>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder, string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        builder.Services.AddSingleton(new ServiceIdentity(serviceName));

        // Console logging. "Logging:Console:FormatterName" picks the format: "simple" (default, one line
        // of text per entry) or "json" (one JSON object per line, for log collectors).
        builder.Logging.ClearProviders();
        if (string.Equals(builder.Configuration["Logging:Console:FormatterName"], "json", StringComparison.OrdinalIgnoreCase))
        {
            builder.Logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
            });
        }
        else
        {
            builder.Logging.AddSimpleConsole(options =>
            {
                options.IncludeScopes = true;
                options.SingleLine = true;
                options.UseUtcTimestamp = true;
                options.TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z' ";
            });
        }

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            context.ProblemDetails.Extensions["service"] = serviceName;
            if (CorrelationId.Get(context.HttpContext) is { } correlationId)
                context.ProblemDetails.Extensions["correlationId"] = correlationId;
        });

        builder.Services.AddHealthChecks();
        builder.Services.AddDefaultCors(builder.Configuration);

        return builder;
    }

    /// <summary>Adds the correlation-id, request-logging, exception-handling and CORS middleware (in that order).</summary>
    public static IApplicationBuilder UseServiceDefaults(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors();
        return app;
    }

    /// <summary>
    /// Maps GET / (service info) and the health endpoints:
    /// <list type="bullet">
    /// <item>GET /health/live - the process is up. Runs no checks (use it for liveness probes).</item>
    /// <item>GET /health/ready - every check tagged "ready" (database, broker, cache...). 503 when one is unhealthy.</item>
    /// <item>GET /health - same as /health/live, kept for older probes.</item>
    /// </list>
    /// </summary>
    public static IEndpointRouteBuilder MapServiceDefaults(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (ServiceIdentity identity, IConfiguration config, IHostEnvironment env) => new ServiceInfo(
                identity.Name,
                config["App:Version"] ?? "1.0.0",
                env.EnvironmentName,
                Environment.MachineName,
                ServiceIdentity.BuildingBlocksVersion,
                DateTimeOffset.UtcNow))
            .WithName("GetServiceInfo")
            .WithTags("System")
            .AllowAnonymous();

        var live = new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthReportWriter.WriteAsync };
        var ready = new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains(ReadyTag),
            ResponseWriter = HealthReportWriter.WriteAsync,
            ResultStatusCodes =
            {
                [HealthStatus.Healthy] = StatusCodes.Status200OK,
                [HealthStatus.Degraded] = StatusCodes.Status200OK,
                [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
            },
        };

        endpoints.MapHealthChecks("/health", live).AllowAnonymous();
        endpoints.MapHealthChecks("/health/live", live).AllowAnonymous();
        endpoints.MapHealthChecks("/health/ready", ready).AllowAnonymous();

        return endpoints;
    }

    /// <summary>
    /// Registers the default CORS policy from the "Cors:AllowedOrigins" setting.
    /// An empty list or "*" allows any origin.
    /// </summary>
    public static IServiceCollection AddDefaultCors(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        return services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            if (origins.Length == 0 || origins.Contains("*"))
                policy.AllowAnyOrigin();
            else
                policy.WithOrigins(origins);

            policy.AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders(CorrelationId.HeaderName, "Retry-After");
        }));
    }
}
