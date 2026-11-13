using Customers.Domain.Entities;

namespace Customers.Infrastructure.Persistence;

/// <summary>Fictional sample customers (example.com addresses only, no real personal data), inserted by the initial migration.</summary>
internal static class SeedData
{
    private static readonly DateTimeOffset Seeded = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static IEnumerable<Customer> Customers() =>
    [
        new("Alex", "Rivera", "alex.rivera@example.com", "Austin", Seeded) { Id = 1 },
        new("Priya", "Shah", "priya.shah@example.com", "Denver", Seeded) { Id = 2 },
        new("Jordan", "Lee", "jordan.lee@example.com", "Seattle", Seeded) { Id = 3 },
        new("Sam", "Okafor", "sam.okafor@example.com", "Chicago", Seeded) { Id = 4 },
    ];
}
