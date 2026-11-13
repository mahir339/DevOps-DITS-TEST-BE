using DevOpsStore.BuildingBlocks.Security;
using Identity.Application.Abstractions;
using Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Security;

/// <summary>PBKDF2 hashes from ASP.NET Core Identity (salted, versioned format).</summary>
public sealed class AspNetPasswordHasher : IPasswordHasher
{
    private readonly PasswordHasher<object> _hasher = new();
    private static readonly object User = new();

    public string Hash(string password) => _hasher.HashPassword(User, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(User, hash, password) != PasswordVerificationResult.Failed;
}

public sealed class JwtTokenService(JwtTokenIssuer issuer) : ITokenService
{
    public AccessToken Create(User user)
    {
        var token = issuer.Issue(user.Id.ToString(), user.Email, user.DisplayName, user.Roles);
        return new AccessToken(token.AccessToken, token.ExpiresAt);
    }
}
