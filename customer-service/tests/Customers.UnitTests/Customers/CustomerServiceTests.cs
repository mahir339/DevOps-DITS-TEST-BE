using Customers.Application.Customers;
using Customers.UnitTests.Fakes;
using DevOpsStore.BuildingBlocks.Results;

namespace Customers.UnitTests.Customers;

public class CustomerServiceTests
{
    private readonly CustomerService _service = new(new InMemoryCustomerRepository(), TimeProvider.System);

    [Fact]
    public async Task GetAll_ReturnsSeededCustomers()
    {
        var customers = await _service.GetAllAsync();

        Assert.Equal(4, customers.Count);
        Assert.All(customers, c => Assert.EndsWith("@example.com", c.Email));
    }

    [Fact]
    public async Task Create_NormalizesEmailToLowerCase()
    {
        var result = await _service.CreateAsync(new CustomerRequest("Taylor", "Kim", "  Taylor.Kim@Example.com ", "Boston"));

        Assert.True(result.IsSuccess);
        Assert.Equal("taylor.kim@example.com", result.Value.Email);
        Assert.Equal(5, result.Value.Id);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("")]
    public async Task Create_InvalidEmail_ReturnsValidationError(string email)
    {
        var result = await _service.CreateAsync(new CustomerRequest("Taylor", "Kim", email, null));

        Assert.Equal(ErrorType.Validation, result.Error!.Type);
        Assert.Contains("email", result.Error.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task Update_KeepsOriginalCreatedDate()
    {
        var original = (await _service.GetByIdAsync(1)).Value;

        var updated = await _service.UpdateAsync(1, new CustomerRequest("Alexis", "Rivera", "alexis@example.com", "Austin"));

        Assert.True(updated.IsSuccess);
        Assert.Equal("Alexis", updated.Value.FirstName);
        Assert.Equal(original.CreatedAt, updated.Value.CreatedAt);
    }

    [Fact]
    public async Task Delete_UnknownCustomer_ReturnsNotFound()
    {
        var result = await _service.DeleteAsync(999);

        Assert.Equal(ErrorType.NotFound, result.Error!.Type);
    }

    [Fact]
    public async Task Create_DuplicateEmail_ReturnsConflict()
    {
        var result = await _service.CreateAsync(new CustomerRequest("Alex", "Other", "ALEX.RIVERA@example.com", null));

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Update_ToAnotherCustomersEmail_ReturnsConflict()
    {
        var result = await _service.UpdateAsync(2, new CustomerRequest("Priya", "Shah", "jordan.lee@example.com", null));

        Assert.Equal(ErrorType.Conflict, result.Error!.Type);
    }

    [Fact]
    public async Task Update_KeepingOwnEmail_Succeeds()
    {
        var result = await _service.UpdateAsync(2, new CustomerRequest("Priya", "Shah-Patel", "priya.shah@example.com", "Boulder"));

        Assert.True(result.IsSuccess);
    }
}
