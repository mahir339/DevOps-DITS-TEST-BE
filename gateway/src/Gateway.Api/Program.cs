using System.Globalization;
using System.Threading.RateLimiting;
using DevOpsStore.BuildingBlocks.Security;
using DevOpsStore.BuildingBlocks.Web;
using Gateway.Api;
using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults("gateway");
// The gateway checks tokens itself (per-route AuthorizationPolicy in "ReverseProxy:Routes"),
// and every backend service checks them again.
builder.AddJwtAuthentication();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    // CORS is answered here. Dropping Origin stops the backends from adding a second set of CORS headers.
    .AddTransforms(context => context.AddRequestHeaderRemove("Origin"));

var gateway = builder.Configuration.GetSection(GatewayOptions.SectionName).Get<GatewayOptions>() ?? new GatewayOptions();
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // Per client IP. Behind a load balancer the IP is the balancer's unless forwarded headers are configured.
    options.AddPolicy(GatewayOptions.LoginRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = gateway.LoginPermitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    options.OnRejected = (context, _) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    };
});

var app = builder.Build();

app.UseServiceDefaults();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<BlockedPathsMiddleware>();

app.MapServiceDefaults();
app.MapReverseProxy();

app.Run();
