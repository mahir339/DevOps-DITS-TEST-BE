namespace Ordering.Application.Abstractions.ExternalServices;

/// <summary>Checks whether order-service can reach each service it depends on.</summary>
public interface IDependencyHealthChecker
{
    Task<IReadOnlyList<DependencyStatus>> CheckAllAsync(CancellationToken cancellationToken = default);
}

public sealed record DependencyStatus(string Name, string? Url, bool Healthy, int? StatusCode, string? Error);
