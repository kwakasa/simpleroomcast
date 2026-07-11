using System.Diagnostics;
using System.Net.Sockets;

namespace ArcStream.Core;

public sealed record ProbeResult(bool Reachable, TimeSpan Elapsed, string Detail);

public sealed class DeviceProbe
{
    public async Task<ProbeResult> ProbeAsync(
        AirPlayDevice device,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);

        try
        {
            using var client = new TcpClient();
            var host = device.Addresses.FirstOrDefault()?.ToString() ?? device.HostName;
            await client.ConnectAsync(host, device.Port, timeoutSource.Token);
            stopwatch.Stop();
            return new ProbeResult(true, stopwatch.Elapsed, "TCP connection established");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new ProbeResult(false, stopwatch.Elapsed, $"Timed out after {timeout.TotalSeconds:0.#} seconds");
        }
        catch (SocketException exception)
        {
            stopwatch.Stop();
            return new ProbeResult(false, stopwatch.Elapsed, exception.SocketErrorCode.ToString());
        }
    }
}
