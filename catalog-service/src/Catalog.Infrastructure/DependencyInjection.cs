using Catalog.Application.Abstractions;
using Catalog.Infrastructure.Caching;
using Catalog.Infrastructure.Persistence;
using DevOpsStore.BuildingBlocks.Persistence;
using DevOpsStore.BuildingBlocks.Web;
using Microsoft.Extensions.Caching.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Catalog.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseConnectionName = "CatalogDb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        // PostgreSQL: ConnectionStrings:CatalogDb (DevOpsStore.BuildingBlocks.Persistence package).
        builder.AddPostgresDbContext<CatalogDbContext>(DatabaseConnectionName);
        builder.Services.AddScoped<IProductRepository, EfProductRepository>();
        builder.Services.TryAddSingleton(TimeProvider.System);

        // Redis: ConnectionStrings:Redis + the "Cache" section. Validated at startup, not here,
        // so "--migrate" can run without Redis.
        builder.Services.AddOptions<CacheOptions>()
            .Bind(builder.Configuration.GetSection(CacheOptions.SectionName))
            .Configure<IConfiguration>((options, config) =>
                options.ConnectionString = config.GetConnectionString(CacheOptions.ConnectionStringName) ?? string.Empty)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var cacheEnabled = builder.Configuration.GetValue(CacheOptions.SectionName + ":Enabled", true);
        if (cacheEnabled)
        {
            builder.Services.AddStackExchangeRedisCache(_ => { });
            builder.Services.AddOptions<RedisCacheOptions>().Configure<IConfiguration>((options, config) =>
            {
                var redis = ConfigurationOptions.Parse(config.GetConnectionString(CacheOptions.ConnectionStringName) ?? string.Empty);
                // Fail fast: a dead cache must not make every request wait for the 5 s defaults.
                redis.ConnectTimeout = 2000;
                redis.SyncTimeout = 2000;
                redis.AsyncTimeout = 2000;
                redis.AbortOnConnectFail = false;
                options.ConfigurationOptions = redis;
                options.InstanceName = string.Empty; // keys are prefixed by Cache:KeyPrefix instead
            });
            builder.Services.AddScoped<IProductCache, RedisProductCache>();
            builder.Services.AddHealthChecks()
                .AddCheck<RedisHealthCheck>("redis", HealthStatus.Degraded, [ServiceDefaultsExtensions.ReadyTag]);
        }
        else
        {
            builder.Services.AddSingleton<IProductCache, NoProductCache>();
        }

        return builder;
    }
}
