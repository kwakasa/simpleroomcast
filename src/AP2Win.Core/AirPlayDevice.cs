using System.Net;

namespace AP2Win.Core;

public sealed record AirPlayDevice(
    string Id,
    string Name,
    string ServiceType,
    string HostName,
    int Port,
    IReadOnlyList<IPAddress> Addresses,
    IReadOnlyDictionary<string, string> Properties)
{
    public string Endpoint => Addresses.Count > 0
        ? $"{Addresses.First()}:{Port}"
        : $"{HostName}:{Port}";
}
