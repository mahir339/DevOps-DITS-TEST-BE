using Customers.Domain.Entities;

namespace Customers.Application.Abstractions;

public interface ICustomerRepository
{
    Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True when another customer (not <paramref name="exceptId"/>) already uses the email.</summary>
    Task<bool> EmailInUseAsync(string normalizedEmail, int? exceptId, CancellationToken cancellationToken = default);

    Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken = default);

    /// <summary>Saves changes made to a customer loaded with <see cref="GetByIdAsync"/>.</summary>
    Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
