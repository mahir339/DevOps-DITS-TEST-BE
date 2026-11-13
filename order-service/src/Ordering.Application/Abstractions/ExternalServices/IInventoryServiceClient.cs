namespace Ordering.Application.Abstractions.ExternalServices;

/// <summary>Calls inventory-service. Throws DownstreamUnavailableException when it cannot be reached.</summary>
public interface IInventoryServiceClient
{
    Task<ReservationOutcome> ReserveAsync(int productId, int quantity, CancellationToken cancellationToken = default);

    Task ReleaseAsync(int productId, int quantity, CancellationToken cancellationToken = default);
}

public enum ReservationOutcome
{
    Reserved,
    InsufficientStock,
}
