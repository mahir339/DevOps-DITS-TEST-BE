using Customers.Domain.Entities;

namespace Customers.Application.Customers;

public sealed record CustomerRequest(string FirstName, string LastName, string Email, string? City);

public sealed record CustomerResponse(int Id, string FirstName, string LastName, string Email, string City, DateTimeOffset CreatedAt)
{
    public static CustomerResponse FromEntity(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.City, customer.CreatedAt);
}
