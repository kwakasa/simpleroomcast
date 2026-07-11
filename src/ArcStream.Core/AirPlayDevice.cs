using System.Net;

namespace ArcStream.Core;

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
        ? $"{Addresses[0]}:{Port}"
        : $"{HostName}:{Port}";
}
