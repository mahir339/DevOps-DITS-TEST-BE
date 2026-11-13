namespace Customers.Domain.Entities;

public sealed class Customer
{
    public Customer(string firstName, string lastName, string email, string city, DateTimeOffset createdAt)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = NormalizeEmail(email);
        City = city;
        CreatedAt = createdAt;
    }

    /// <summary>Assigned by the repository when the customer is stored.</summary>
    public int Id { get; set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public string Email { get; private set; }

    public string City { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public void Update(string firstName, string lastName, string email, string city)
    {
        FirstName = firstName;
        LastName = lastName;
        Email = NormalizeEmail(email);
        City = city;
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
