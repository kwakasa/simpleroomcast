using AP2Win.Core;
using AP2Win.Core.Discovery;
using System.Net.Sockets;
using System.Text.Json;

namespace AP2Win.Cli;

internal static class CliApplication
{
    private const int CommandIndex = 0;
    private const int SelectorIndex = 1;
    private const int CommandOnlyArgumentCount = 1;
    private const int CommandWithSelectorArgumentCount = 2;
    private const int SuccessExitCode = 0;
    private const int GeneralFailureExitCode = 1;
    private const int InvalidArgumentsExitCode = 2;
    private const int NetworkFailureExitCode = 3;
    private const int NotImplementedExitCode = 4;
    private const int CancelledExitCode = 130;
    private static readonly TimeSpan DiscoveryDuration = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(2);

    public static async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || IsHelp(args[CommandIndex]))
        {
            PrintHelp();
            return args.Length == 0 ? InvalidArgumentsExitCode : SuccessExitCode;
        }

        using var cancellation = new CancellationTokenSource();
        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        try
        {
            return args[CommandIndex].ToLowerInvariant() switch
            {
                "list" when args.Length == CommandOnlyArgumentCount => await ListAsync(cancellation.Token),
                "probe" when args.Length == CommandWithSelectorArgumentCount =>
                    await ProbeAsync(args[SelectorIndex], cancellation.Token),
                "inspect" when args.Length == CommandWithSelectorArgumentCount =>
                    await InspectAsync(args[SelectorIndex], cancellation.Token),
                "capture" when args.Length == CommandWithSelectorArgumentCount => Capture(args[SelectorIndex]),
                _ => InvalidArguments()
            };
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("Cancelled.");
            return CancelledExitCode;
        }
        catch (SocketException exception)
        {
            Console.Error.WriteLine($"Network error: {exception.Message}");
            return NetworkFailureExitCode;
        }
    }

    private static async Task<int> ListAsync(CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        if (devices.Count == 0)
        {
            Console.WriteLine("No AirPlay devices found.");
            Console.WriteLine("Check that the speaker is awake and multicast is enabled on the LAN.");
            return GeneralFailureExitCode;
        }

        foreach (var device in devices)
        {
            Console.WriteLine($"{device.Name}  {device.Endpoint}");
            Console.WriteLine($"  id: {device.Id}");
            Console.WriteLine($"  service: {device.ServiceType}");
            PrintSelectedProperties(device);
        }

        return SuccessExitCode;
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
            ProbeTimeout,
            cancellationToken);
        Console.WriteLine($"Reachable: {(result.Reachable ? "yes" : "no")}");
        Console.WriteLine($"Elapsed: {result.Elapsed.TotalMilliseconds:0} ms");
        Console.WriteLine($"Detail: {result.Detail}");
        PrintSelectedProperties(device);
        return result.Reachable ? SuccessExitCode : GeneralFailureExitCode;
    }

    private static async Task<int> InspectAsync(string selector, CancellationToken cancellationToken)
    {
        var devices = await DiscoverAsync(cancellationToken);
        var (device, errorCode) = SelectDevice(devices, selector);
        if (device is null)
        {
            return errorCode;
        }

        var probe = await new DeviceProbe().ProbeAsync(device, ProbeTimeout, cancellationToken);
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
        return probe.Reachable ? SuccessExitCode : GeneralFailureExitCode;
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
            Console.Error.WriteLine($"No device matched '{selector}'. Run 'ap2win list' to see available devices.");
            return (null, GeneralFailureExitCode);
        }

        if (matches.Length > 1)
        {
            Console.Error.WriteLine($"'{selector}' matched multiple devices; use the exact device ID:");
            foreach (var match in matches)
            {
                Console.Error.WriteLine($"  {match.Name}: {match.Id}");
            }

            return (null, InvalidArgumentsExitCode);
        }

        return (matches.First(), SuccessExitCode);
    }

    private static int Capture(string selector)
    {
        Console.Error.WriteLine($"Capture for '{selector}' is not implemented yet.");
        Console.Error.WriteLine("Next milestone: establish an AirPlay 2 test-tone session, then add WASAPI loopback.");
        return NotImplementedExitCode;
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
        return InvalidArgumentsExitCode;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("AP2Win - experimental AirPlay 2 audio sender for Windows");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  ap2win list");
        Console.WriteLine("  ap2win probe <speaker-name-or-id>");
        Console.WriteLine("  ap2win inspect <speaker-name-or-id>");
        Console.WriteLine("  ap2win capture <speaker-name-or-id>");
    }
}
