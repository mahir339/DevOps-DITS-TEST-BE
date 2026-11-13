using Customers.Application.Abstractions;
using Customers.Domain.Entities;
using DevOpsStore.BuildingBlocks.Results;

namespace Customers.Application.Customers;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default);

    Task<Result<CustomerResponse>> UpdateAsync(int id, CustomerRequest request, CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public sealed class CustomerService(ICustomerRepository repository, TimeProvider timeProvider) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken = default) =>
        (await repository.GetAllAsync(cancellationToken)).Select(CustomerResponse.FromEntity).ToList();

    public async Task<Result<CustomerResponse>> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var customer = await repository.GetByIdAsync(id, cancellationToken);
        return customer is null ? NotFound(id) : CustomerResponse.FromEntity(customer);
    }

    public async Task<Result<CustomerResponse>> CreateAsync(CustomerRequest request, CancellationToken cancellationToken = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return error;

        var customer = new Customer(
            request.FirstName.Trim(),
            request.LastName.Trim(),
            request.Email,
            request.City?.Trim() ?? string.Empty,
            timeProvider.GetUtcNow());

        if (await repository.EmailInUseAsync(customer.Email, null, cancellationToken)) return EmailTaken(customer.Email);

        return CustomerResponse.FromEntity(await repository.AddAsync(customer, cancellationToken));
    }

    public async Task<Result<CustomerResponse>> UpdateAsync(int id, CustomerRequest request, CancellationToken cancellationToken = default)
    {
        if (CustomerValidator.Validate(request) is { } error) return error;

        var customer = await repository.GetByIdAsync(id, cancellationToken);
        if (customer is null) return NotFound(id);

        customer.Update(request.FirstName.Trim(), request.LastName.Trim(), request.Email, request.City?.Trim() ?? string.Empty);
        if (await repository.EmailInUseAsync(customer.Email, id, cancellationToken)) return EmailTaken(customer.Email);

        await repository.UpdateAsync(customer, cancellationToken);
        return CustomerResponse.FromEntity(customer);
    }

    public async Task<Result> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        await repository.DeleteAsync(id, cancellationToken) ? Result.Success() : NotFound(id);

    private static Error NotFound(int id) => Error.NotFound($"Customer {id} was not found.");

    private static Error EmailTaken(string email) => Error.Conflict("Email already in use", $"Another customer already uses {email}.");
}
