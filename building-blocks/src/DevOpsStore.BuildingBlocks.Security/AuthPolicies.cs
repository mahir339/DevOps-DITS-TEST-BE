namespace DevOpsStore.BuildingBlocks.Security;

/// <summary>Role names carried in the "role" claim of access tokens.</summary>
public static class Roles
{
    public const string Admin = "admin";
    public const string Staff = "staff";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal) { Admin, Staff };
}

/// <summary>Authorization policy names used with RequireAuthorization(...).</summary>
public static class AuthPolicies
{
    /// <summary>Any signed-in staff member or admin.</summary>
    public const string Staff = "staff";

    /// <summary>Admins only.</summary>
    public const string Admin = "admin";
}

/// <summary>Claim types written by identity-service (short JWT names, inbound claim mapping is disabled).</summary>
public static class AppClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Name = "name";
    public const string Role = "role";
}
