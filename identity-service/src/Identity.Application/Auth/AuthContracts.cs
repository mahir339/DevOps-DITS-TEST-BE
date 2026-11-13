using Identity.Domain.Entities;

namespace Identity.Application.Auth;

public sealed record LoginRequest(string? Email, string? Password);

public sealed record UserResponse(int Id, string Email, string DisplayName, IReadOnlyList<string> Roles)
{
    public static UserResponse FromEntity(User user) => new(user.Id, user.Email, user.DisplayName, user.Roles);
}

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
