using Gateway.Api;
using Microsoft.Extensions.Configuration;

namespace Gateway.UnitTests;

/// <summary>Checks the shipped appsettings.json, so a bad edit to the routing table fails the build.</summary>
public class GatewayConfigTests
{
    private static readonly IConfiguration Config = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json")
        .Build();

    [Theory]
    [InlineData("/api/inventory/3/reserve", true)]
    [InlineData("/api/inventory/3/release/", true)]
    [InlineData("/API/INVENTORY/3/RESERVE", true)]
    [InlineData("/api/inventory/3", false)]
    [InlineData("/api/orders/1/cancel", false)]
    public void BlockedPaths_HideInternalInventoryEndpoints(string path, bool blocked)
    {
        var middleware = new BlockedPathsMiddleware(_ => Task.CompletedTask, Config);

        Assert.Equal(blocked, middleware.IsBlocked(path));
    }

    [Fact]
    public void EveryRoute_PointsToAnExistingCluster_AndHasAPolicy()
    {
        var clusters = Config.GetSection("ReverseProxy:Clusters").GetChildren().Select(c => c.Key).ToHashSet();
        var routes = Config.GetSection("ReverseProxy:Routes").GetChildren().ToList();

        Assert.NotEmpty(routes);
        Assert.All(routes, route =>
        {
            Assert.Contains(route["ClusterId"]!, clusters);
            Assert.False(string.IsNullOrEmpty(route["AuthorizationPolicy"]), $"Route {route.Key} has no AuthorizationPolicy");
        });
    }

    [Fact]
    public void LoginRoute_IsRateLimited()
    {
        Assert.Equal(GatewayOptions.LoginRateLimitPolicy, Config["ReverseProxy:Routes:auth-login:RateLimiterPolicy"]);
    }
}
