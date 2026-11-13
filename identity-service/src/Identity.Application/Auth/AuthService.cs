using DevOpsStore.BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Identity.Application.Auth;

public interface IAuthService
{
    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<Result<UserResponse>> GetUserAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default);
}

public sealed class AuthService(
    IUserRepository users,
    IPasswordHasher hasher,
    ITokenService tokens,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    // Same message for every failure, so callers cannot find out which emails exist.
    private static readonly Error InvalidCredentials = Error.Unauthorized("Invalid email or password.");

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var error = new ValidationBuilder()
            .AddIf(string.IsNullOrWhiteSpace(request.Email), "email", "Email is required.")
            .AddIf(string.IsNullOrEmpty(request.Password), "password", "Password is required.")
            .ToError();
        if (error is not null) return error;

        var now = timeProvider.GetUtcNow();
        var user = await users.GetByEmailAsync(request.Email!.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null) return InvalidCredentials;

        if (user.IsLockedOut(now))
        {
            logger.LogWarning("Login refused for locked-out user {UserId}", user.Id);
            return InvalidCredentials;
        }

        if (!hasher.Verify(user.PasswordHash, request.Password!))
        {
            user.RegisterFailedLogin(now);
            await users.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Failed login for user {UserId}", user.Id);
            return InvalidCredentials;
        }

        user.RegisterSuccessfulLogin(now);
        await users.SaveChangesAsync(cancellationToken);

        var token = tokens.Create(user);
        logger.LogInformation("User {UserId} signed in", user.Id);
        return new LoginResponse(token.Token, token.ExpiresAt, UserResponse.FromEntity(user));
    }

    public async Task<Result<UserResponse>> GetUserAsync(int id, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken);
        return user is null ? Error.NotFound($"User {id} was not found.") : UserResponse.FromEntity(user);
    }

    public async Task<IReadOnlyList<UserResponse>> GetUsersAsync(CancellationToken cancellationToken = default) =>
        (await users.GetAllAsync(cancellationToken)).Select(UserResponse.FromEntity).ToList();
}
