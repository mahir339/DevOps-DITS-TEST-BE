using DevOpsStore.BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Seeding;

/// <summary>One entry of the "Identity:SeedUsers" list.</summary>
public sealed class SeedUser
{
    public string Email { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    /// <summary>A secret: supply it through user-secrets or an environment variable, never appsettings.json.</summary>
    public string? Password { get; init; }

    public string[] Roles { get; init; } = [];
}

public sealed class IdentitySeedOptions
{
    public const string SectionName = "Identity";

    public List<SeedUser> SeedUsers { get; init; } = [];
}

/// <summary>
/// Creates the configured users at startup if they do not exist yet (existing users are never changed).
/// A user without a password is skipped with a warning.
/// </summary>
public sealed class UserSeeder(IServiceScopeFactory scopes, IOptions<IdentitySeedOptions> options, TimeProvider time, ILogger<UserSeeder> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        foreach (var seed in options.Value.SeedUsers)
        {
            if (string.IsNullOrWhiteSpace(seed.Email)) continue;
            if (string.IsNullOrEmpty(seed.Password))
            {
                logger.LogWarning("Seed user {Email} has no password configured; skipped", seed.Email);
                continue;
            }

            var unknownRoles = seed.Roles.Where(r => !Roles.All.Contains(r)).ToList();
            if (unknownRoles.Count > 0)
            {
                logger.LogWarning("Seed user {Email} has unknown roles {Roles}; skipped", seed.Email, unknownRoles);
                continue;
            }

            if (await users.GetByEmailAsync(seed.Email.Trim().ToLowerInvariant(), cancellationToken) is not null) continue;

            await users.AddAsync(new User(seed.Email, seed.DisplayName, hasher.Hash(seed.Password), seed.Roles, time.GetUtcNow()), cancellationToken);
            logger.LogInformation("Seed user {Email} created with roles {Roles}", seed.Email, seed.Roles);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
