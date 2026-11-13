using System.ComponentModel.DataAnnotations;
using DevOpsStore.BuildingBlocks.Http;
using DevOpsStore.BuildingBlocks.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevOpsStore.BuildingBlocks.UnitTests;

public class SecurityTests
{
    private const string Key = "unit-test-signing-key-that-is-long-enough-123";

    private static List<ValidationResult> Validate(JwtOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void JwtOptions_MissingKey_IsInvalid()
    {
        var errors = Validate(new JwtOptions());

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("Jwt:SigningKey is not set"));
    }

    [Fact]
    public void JwtOptions_ShortKey_IsInvalid()
    {
        var errors = Validate(new JwtOptions { SigningKey = "too-short" });

        Assert.Contains(errors, e => e.ErrorMessage!.Contains("at least 32 bytes"));
    }

    [Fact]
    public void JwtOptions_LongKey_IsValid()
    {
        Assert.Empty(Validate(new JwtOptions { SigningKey = Key }));
    }

    [Fact]
    public async Task IssuedToken_ValidatesWithSameSettings_AndCarriesRoles()
    {
        var jwt = new JwtOptions { SigningKey = Key, AccessTokenMinutes = 15 };
        var issuer = new JwtTokenIssuer(Options.Create(jwt), TimeProvider.System);

        var token = issuer.Issue("42", "admin@example.com", "Ada Admin", [Roles.Admin, Roles.Staff]);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, SecurityExtensions.CreateValidationParameters(jwt));

        Assert.True(result.IsValid, result.Exception?.Message);
        Assert.Equal("42", result.ClaimsIdentity.FindFirst(AppClaims.Subject)?.Value);
        Assert.True(result.ClaimsIdentity.HasClaim(AppClaims.Role, Roles.Admin));
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(14));
    }

    [Fact]
    public async Task IssuedToken_IsRejected_WhenValidatorUsesDifferentKey()
    {
        var issuer = new JwtTokenIssuer(Options.Create(new JwtOptions { SigningKey = Key }), TimeProvider.System);
        var token = issuer.Issue("1", "a@example.com", "A", [Roles.Staff]);

        var otherKey = new JwtOptions { SigningKey = Key + "-different" };
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, SecurityExtensions.CreateValidationParameters(otherKey));

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task IssuedToken_IsRejected_WhenAudienceDiffers()
    {
        var issuer = new JwtTokenIssuer(Options.Create(new JwtOptions { SigningKey = Key }), TimeProvider.System);
        var token = issuer.Issue("1", "a@example.com", "A", [Roles.Staff]);

        var otherAudience = new JwtOptions { SigningKey = Key, Audience = "someone-else" };
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token.AccessToken, SecurityExtensions.CreateValidationParameters(otherAudience));

        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData("Bearer abc.def.ghi", "Bearer abc.def.ghi")]
    [InlineData("Basic dXNlcjpwYXNz", null)]
    [InlineData(null, null)]
    public async Task BearerPropagationHandler_ForwardsOnlyBearerTokens(string? incoming, string? expected)
    {
        var context = new DefaultHttpContext();
        if (incoming is not null) context.Request.Headers.Authorization = incoming;

        var inner = new CapturingHandler();
        var handler = new BearerTokenPropagationHandler(new HttpContextAccessor { HttpContext = context }) { InnerHandler = inner };
        using var client = new HttpClient(handler);

        await client.GetAsync("http://downstream.test/api");

        Assert.Equal(expected, inner.Request?.Headers.Authorization?.ToString());
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK));
        }
    }
}
