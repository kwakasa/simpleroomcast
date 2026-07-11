using ArcStream.Core;
using ArcStream.Core.Discovery;
using System.Net.Sockets;
using System.Text.Json;

namespace ArcStream.Cli;

internal static class CliApplication
{
    private static readonly TimeSpan DiscoveryDuration = TimeSpan.FromSeconds(3);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            PrintHelp();
            return args.Length == 0 ? 2 : 0;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            return args[0].ToLowerInvariant() switch
            {
                "list" when args.Length == 1 => await ListAsync(cancellation.Token),
                "probe" when args.Length == 2 => await ProbeAsync(args[1], cancellation.Token),
                "inspect" when args.Length == 2 => await InspectAsync(args[1], cancellation.Token),
                "capture" when args.Length == 2 => Capture(args[1]),
                _ => InvalidArguments()
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return 130;
        }
        catch (SocketException exception)
        {
            Console.Error.WriteLine($"Network error: {exception.Message}");
            return 3;
        }
    }

    private static async Task<int> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        if (devices.Count == 0)
        {
            Console.WriteLine("No AirPlay devices found.");
            Console.WriteLine("Check that the speaker is awake and multicast is enabled on the LAN.");
            return 1;
        }

        foreach (var device in devices)
        {
            Console.WriteLine($"{device.Name}  {device.Endpoint}");
            Console.WriteLine($"  id: {device.Id}");
            Console.WriteLine($"  service: {device.ServiceType}");
            PrintSelectedProperties(device);
        }

        return 0;
    }

    private static async Task<int> ProbeAsync(string selector, CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        var (device, errorCode) = SelectDevice(devices, selector);
        if (device is null)
        {
            return errorCode;
        }

        Console.WriteLine($"Probing {device.Name} at {device.Endpoint}...");
        var result = await new DeviceProbe().ProbeAsync(
            device,
            TimeSpan.FromSeconds(2),
            cancellationToken);
        Console.WriteLine($"Reachable: {(result.Reachable ? "yes" : "no")}");
        Console.WriteLine($"Elapsed: {result.Elapsed.TotalMilliseconds:0} ms");
        Console.WriteLine($"Detail: {result.Detail}");
        PrintSelectedProperties(device);
        return result.Reachable ? 0 : 1;
    }

    private static async Task<int> InspectAsync(string selector, CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        var (device, errorCode) = SelectDevice(devices, selector);
        if (device is null)
        {
            return errorCode;
        }

        var probe = await new DeviceProbe().ProbeAsync(device, TimeSpan.FromSeconds(2), cancellationToken);
        var snapshot = new
        {
            capturedAtUtc = DateTimeOffset.UtcNow,
            device = new
            {
                device.Id,
                device.Name,
                device.ServiceType,
                device.HostName,
                device.Port,
                addresses = device.Addresses.Select(address => address.ToString()).ToArray(),
                properties = device.Properties
                    .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
            },
            probe = new
            {
                probe.Reachable,
                elapsedMilliseconds = Math.Round(probe.Elapsed.TotalMilliseconds),
                probe.Detail
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true }));
        return probe.Reachable ? 0 : 1;
    }

    private static (AirPlayDevice? Device, int ErrorCode) SelectDevice(
        IReadOnlyList<AirPlayDevice> devices,
        string selector)
    {
        var exactMatches = devices.Where(device =>
            device.Name.Equals(selector, StringComparison.OrdinalIgnoreCase) ||
            device.Id.Equals(selector, StringComparison.OrdinalIgnoreCase)).ToArray();
        var matches = exactMatches.Length > 0
            ? exactMatches
            : devices.Where(device => device.Name.Contains(selector, StringComparison.OrdinalIgnoreCase)).ToArray();

        if (matches.Length == 0)
        {
            Console.Error.WriteLine($"No device matched '{selector}'. Run 'arcstream list' to see available devices.");
            return (null, 1);
        }

        if (matches.Length > 1)
        {
            Console.Error.WriteLine($"'{selector}' matched multiple devices; use the exact device ID:");
            foreach (var match in matches)
            {
                Console.Error.WriteLine($"  {match.Name}: {match.Id}");
            }

            return (null, 2);
        }

        return (matches[0], 0);
    }

    private static int Capture(string selector)
    {
        Console.Error.WriteLine($"Capture for '{selector}' is not implemented yet.");
        Console.Error.WriteLine("Next milestone: establish an AirPlay 2 test-tone session, then add WASAPI loopback.");
        return 4;
    }

    private static Task<IReadOnlyList<AirPlayDevice>> DiscoverAsync(CancellationToken cancellationToken)
    {
        IDeviceDiscovery discovery = new MdnsDeviceDiscovery();
        return discovery.DiscoverAsync(DiscoveryDuration, cancellationToken);
    }

    private static void PrintSelectedProperties(AirPlayDevice device)
    {
        var selected = new[] { "manufacturer", "model", "srcvers", "features", "flags", "protovers" };
        foreach (var key in selected)
        {
            if (device.Properties.TryGetValue(key, out var value))
            {
                Console.WriteLine($"  {key}: {value}");
            }
        }
    }

    private static bool IsHelp(string value) => value is "-h" or "--help" or "help";

    private static int InvalidArguments()
    {
        Console.Error.WriteLine("Invalid arguments.");
        PrintHelp();
        return 2;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("ArcStream - experimental Windows audio sender for AirPlay 2");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  arcstream list");
        Console.WriteLine("  arcstream probe <speaker-name-or-id>");
        Console.WriteLine("  arcstream inspect <speaker-name-or-id>");
        Console.WriteLine("  arcstream capture <speaker-name-or-id>");
    }
}
