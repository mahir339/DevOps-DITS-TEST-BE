using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace DevOpsStore.BuildingBlocks.Security;

public static class SecurityExtensions
{
    /// <summary>
    /// Validates "Authorization: Bearer" JWTs issued by identity-service and registers the
    /// <see cref="AuthPolicies.Staff"/> and <see cref="AuthPolicies.Admin"/> policies.
    /// The service refuses to start when the "Jwt" section is missing or invalid.
    /// </summary>
    public static IHostApplicationBuilder AddJwtAuthentication(this IHostApplicationBuilder builder)
    {
        builder.Services.AddOptions<JwtOptions>()
            .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>, ILoggerFactory>((bearer, jwtOptions, loggerFactory) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false;
                bearer.RequireHttpsMetadata = false; // tokens are validated with a shared key, no metadata endpoint
                bearer.TokenValidationParameters = CreateValidationParameters(jwt);

                var logger = loggerFactory.CreateLogger("DevOpsStore.BuildingBlocks.Security.Jwt");
                bearer.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        // Logged at Information: expired tokens are normal, but a wrong key or issuer is a deployment mistake.
                        logger.LogInformation("JWT rejected: {Reason}", context.Exception.GetType().Name);
                        return Task.CompletedTask;
                    },
                };
            });

        builder.Services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Staff, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Staff, Roles.Admin))
            .AddPolicy(AuthPolicies.Admin, policy => policy.RequireAuthenticatedUser().RequireRole(Roles.Admin));

        return builder;
    }

    public static TokenValidationParameters CreateValidationParameters(JwtOptions jwt) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = jwt.Issuer,
        ValidateAudience = true,
        ValidAudience = jwt.Audience,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = CreateSigningKey(jwt.SigningKey),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ValidateLifetime = true,
        RequireExpirationTime = true,
        ClockSkew = TimeSpan.FromSeconds(jwt.ClockSkewSeconds),
        NameClaimType = AppClaims.Name,
        RoleClaimType = AppClaims.Role,
    };

    internal static SymmetricSecurityKey CreateSigningKey(string key) => new(Encoding.UTF8.GetBytes(key));
}
