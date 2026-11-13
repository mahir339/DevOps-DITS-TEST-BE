using System.Collections.Concurrent;
using Customers.Application.Abstractions;
using Customers.Domain.Entities;

namespace Customers.UnitTests.Fakes;

/// <summary>Test double for the EF Core repository, pre-filled with four example.com customers.</summary>
public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly ConcurrentDictionary<int, Customer> _customers = new();
    private int _nextId;

    public InMemoryCustomerRepository()
    {
        var now = DateTimeOffset.UtcNow;
        Store(new Customer("Alex", "Rivera", "alex.rivera@example.com", "Austin", now));
        Store(new Customer("Priya", "Shah", "priya.shah@example.com", "Denver", now));
        Store(new Customer("Jordan", "Lee", "jordan.lee@example.com", "Seattle", now));
        Store(new Customer("Sam", "Okafor", "sam.okafor@example.com", "Chicago", now));
    }

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<Customer>>(_customers.Values.OrderBy(c => c.Id).ToList());

    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_customers.GetValueOrDefault(id));

    public Task<bool> EmailInUseAsync(string normalizedEmail, int? exceptId, CancellationToken cancellationToken = default) =>
        Task.FromResult(_customers.Values.Any(c => c.Email == normalizedEmail && c.Id != exceptId));

    public Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken = default) =>
        Task.FromResult(Store(customer));

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_customers.TryRemove(id, out _));

    private Customer Store(Customer customer)
    {
        customer.Id = Interlocked.Increment(ref _nextId);
        _customers[customer.Id] = customer;
        return customer;
    }
}
