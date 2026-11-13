namespace Ordering.Application.Abstractions.ExternalServices;

/// <summary>Calls customer-service. Throws DownstreamUnavailableException when it cannot be reached.</summary>
public interface ICustomerServiceClient
{
    /// <returns>The customer, or null if customer-service says it does not exist.</returns>
    Task<CustomerSnapshot?> GetCustomerAsync(int customerId, CancellationToken cancellationToken = default);
}

public sealed record CustomerSnapshot(int Id, string FirstName, string LastName, string Email);
