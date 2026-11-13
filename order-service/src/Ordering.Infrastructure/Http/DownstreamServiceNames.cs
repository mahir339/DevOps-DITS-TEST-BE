namespace Ordering.Infrastructure.Http;

/// <summary>Named HttpClient registrations, one per downstream service.</summary>
public static class DownstreamServiceNames
{
    public const string Catalog = "catalog-service";
    public const string Customer = "customer-service";
    public const string Inventory = "inventory-service";

    public static readonly string[] All = [Customer, Catalog, Inventory];
}
