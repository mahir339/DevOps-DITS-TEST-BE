using System.ComponentModel.DataAnnotations;

namespace DevOpsStore.BuildingBlocks.Persistence;

/// <summary>Settings from the "Database" configuration section.</summary>
public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>
    /// Apply pending EF Core migrations when the service starts. Convenient on a laptop,
    /// dangerous with several replicas, so it is off by default and only switched on in
    /// appsettings.Development.json. Elsewhere run migrations as a separate deployment step.
    /// </summary>
    public bool MigrateOnStartup { get; init; }

    /// <summary>
    /// Refuse to start when the database is missing migrations this build expects.
    /// Turn off only if you knowingly run new code against an old schema.
    /// </summary>
    public bool FailOnPendingMigrations { get; init; } = true;

    /// <summary>How many times a failed command is retried for transient errors (network blips, failovers).</summary>
    [Range(0, 10)]
    public int MaxRetryCount { get; init; } = 3;

    [Range(1, 600)]
    public int CommandTimeoutSeconds { get; init; } = 30;
}
