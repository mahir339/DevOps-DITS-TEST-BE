namespace Ordering.Application.Common;

/// <summary>Thrown by the HTTP clients when another service cannot be reached or returns an unexpected error.</summary>
public sealed class DownstreamUnavailableException(string serviceName, Uri? baseAddress, string reason, Exception? innerException = null)
    : Exception($"{serviceName} at {baseAddress} is unavailable: {reason}", innerException)
{
    public string ServiceName { get; } = serviceName;
}
