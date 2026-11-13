using Customers.Application.Abstractions;
using Customers.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Customers.Infrastructure.Persistence;

public sealed class EfCustomerRepository(CustomersDbContext db) : ICustomerRepository
{
    public async Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await db.Customers.AsNoTracking().OrderBy(c => c.Id).ToListAsync(cancellationToken);

    public Task<Customer?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Customers.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> EmailInUseAsync(string normalizedEmail, int? exceptId, CancellationToken cancellationToken = default) =>
        db.Customers.AnyAsync(c => c.Email == normalizedEmail && c.Id != exceptId, cancellationToken);

    public async Task<Customer> AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);
        return customer;
    }

    public Task UpdateAsync(Customer customer, CancellationToken cancellationToken = default) =>
        db.SaveChangesAsync(cancellationToken);

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default) =>
        await db.Customers.Where(c => c.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
