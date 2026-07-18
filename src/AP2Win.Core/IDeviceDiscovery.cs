namespace AP2Win.Core;

public interface IDeviceDiscovery
{
    Task<IReadOnlyList<AirPlayDevice>> DiscoverAsync(
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
