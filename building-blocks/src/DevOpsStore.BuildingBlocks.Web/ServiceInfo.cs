using System.Reflection;

namespace DevOpsStore.BuildingBlocks.Web;

/// <summary>Response of GET / on every service.</summary>
public sealed record ServiceInfo(
    string Service,
    string Version,
    string Environment,
    string Hostname,
    string BuildingBlocksVersion,
    DateTimeOffset Timestamp);

/// <summary>The name a service registered itself with in <c>AddServiceDefaults</c>.</summary>
public sealed record ServiceIdentity(string Name)
{
    /// <summary>Version of this building-blocks package the service was built against (without the "+commit" suffix).</summary>
    public static string BuildingBlocksVersion { get; } =
        (typeof(ServiceIdentity).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown")
        .Split('+')[0];
}
