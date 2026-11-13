using DevOpsStore.BuildingBlocks.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevOpsStore.BuildingBlocks.Persistence;

public static class PersistenceExtensions
{
    /// <summary>Command-line switch that applies migrations and exits instead of serving requests.</summary>
    public const string MigrateSwitch = "--migrate";

    /// <summary>
    /// Registers <typeparamref name="TContext"/> against PostgreSQL using the connection string
    /// "ConnectionStrings:{connectionName}" (environment variable ConnectionStrings__{connectionName})
    /// and adds a "postgres" readiness check. Throws at startup if the connection string is missing.
    /// </summary>
    public static IHostApplicationBuilder AddPostgresDbContext<TContext>(this IHostApplicationBuilder builder, string connectionName)
        where TContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(connectionName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Connection string '{connectionName}' is not configured. " +
                $"Set ConnectionStrings:{connectionName} (environment variable ConnectionStrings__{connectionName}).");
        }

        builder.Services.AddOptions<DatabaseOptions>()
            .Bind(builder.Configuration.GetSection(DatabaseOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        var database = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();

        builder.Services.AddDbContext<TContext>(options => options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.CommandTimeout(database.CommandTimeoutSeconds);
            if (database.MaxRetryCount > 0)
                npgsql.EnableRetryOnFailure(database.MaxRetryCount);
        }));

        builder.Services.AddHealthChecks()
            .AddDbContextCheck<TContext>("postgres", tags: [ServiceDefaultsExtensions.ReadyTag]);

        return builder;
    }

    /// <summary>
    /// Call after Build() and before Run(). Returns true when the process should exit
    /// (it was started with "--migrate"). Otherwise applies or checks migrations according to
    /// <see cref="DatabaseOptions"/>.
    /// </summary>
    public static async Task<bool> PrepareDatabaseAsync<TContext>(this IHost host, string[] args, CancellationToken cancellationToken = default)
        where TContext : DbContext
    {
        await using var scope = host.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var context = services.GetRequiredService<TContext>();
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DevOpsStore.BuildingBlocks.Persistence.Migrations");
        var name = typeof(TContext).Name;

        if (args.Contains(MigrateSwitch, StringComparer.OrdinalIgnoreCase))
        {
            await MigrateAsync(context, logger, name, cancellationToken);
            return true;
        }

        if (options.MigrateOnStartup)
        {
            await MigrateAsync(context, logger, name, cancellationToken);
            return false;
        }

        if (options.FailOnPendingMigrations)
        {
            var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (pending.Count > 0)
            {
                throw new InvalidOperationException(
                    $"{name}: the database is missing {pending.Count} migration(s): {string.Join(", ", pending)}. " +
                    $"Apply them before starting this version (run the service once with '{MigrateSwitch}', " +
                    "or use 'dotnet ef database update' / a migration bundle).");
            }
        }

        return false;
    }

    private static async Task MigrateAsync(DbContext context, ILogger logger, string name, CancellationToken cancellationToken)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count == 0)
        {
            logger.LogInformation("{Context}: database schema is up to date", name);
            return;
        }

        logger.LogInformation("{Context}: applying {Count} migration(s): {Migrations}", name, pending.Count, string.Join(", ", pending));
        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("{Context}: migrations applied", name);
    }
}
