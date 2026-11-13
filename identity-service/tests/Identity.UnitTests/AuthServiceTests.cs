using DevOpsStore.BuildingBlocks.Results;
using Identity.Application.Abstractions;
using Identity.Application.Auth;
using Identity.Domain.Entities;
using Identity.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;

namespace Identity.UnitTests;

public class AuthServiceTests
{
    private const string Password = "unit-test-password-1";
    private readonly FakeUsers _users = new();
    private readonly AspNetPasswordHasher _hasher = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _users.Items.Add(new User("Staff@Example.com", "Sam Staff", _hasher.Hash(Password), ["staff"], DateTimeOffset.UtcNow) { Id = 1 });
        _service = new AuthService(_users, _hasher, new FakeTokens(), TimeProvider.System, NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_CorrectPassword_ReturnsTokenAndUser()
    {
        var result = await _service.LoginAsync(new LoginRequest(" staff@example.COM ", Password));

        Assert.True(result.IsSuccess);
        Assert.Equal("token-for-1", result.Value.AccessToken);
        Assert.Equal(["staff"], result.Value.User.Roles);
    }

    [Theory]
    [InlineData("staff@example.com", "wrong")]
    [InlineData("nobody@example.com", Password)]
    public async Task Login_BadCredentials_ReturnsSameUnauthorizedError(string email, string password)
    {
        var result = await _service.LoginAsync(new LoginRequest(email, password));

        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
        Assert.Equal("Invalid email or password.", result.Error.Detail);
    }

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksEvenTheCorrectPassword()
    {
        for (var i = 0; i < User.MaxFailedLogins; i++)
            await _service.LoginAsync(new LoginRequest("staff@example.com", "wrong"));

        var result = await _service.LoginAsync(new LoginRequest("staff@example.com", Password));

        Assert.Equal(ErrorType.Unauthorized, result.Error!.Type);
    }

    [Fact]
    public async Task Login_MissingFields_ReturnsValidationError()
    {
        var result = await _service.LoginAsync(new LoginRequest("", null));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
    }

    private sealed class FakeTokens : ITokenService
    {
        public AccessToken Create(User user) => new($"token-for-{user.Id}", DateTimeOffset.UtcNow.AddHours(1));
    }

    private sealed class FakeUsers : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<IReadOnlyList<User>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<User>>(Items);

        public Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

        public Task<User?> GetByEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default) =>
            Task.FromResult(Items.FirstOrDefault(u => u.Email == normalizedEmail));

        public Task AddAsync(User user, CancellationToken cancellationToken = default)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
